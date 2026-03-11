using System;
using System.Globalization;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Exceptions;
using LexSMS.Helpers;
using LexSMS.Models;

namespace LexSMS.Features
{
    /// <summary>
    /// 定位管理器
    /// 实现A76XX基站定位功能
    /// </summary>
    public class LocationManager
    {
        private readonly AtChannel _channel;

        public LocationManager(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>
        /// 获取基站定位信息 (Cell-Based Location Service)
        /// 使用AT+CLBS命令
        /// </summary>
        /// <returns>基站定位结果</returns>
        public async Task<CellLocation> GetCellLocationAsync()
        {
            // AT+CLBS=1 : 获取基站定位（通过网络），部分模块会在 OK 后延迟上报 +CLBS
            var location = new CellLocation();
            var clbsData = await SendClbsCommandAndReadPayloadAsync("AT+CLBS=1", 600000, 5000);
            if (!string.IsNullOrWhiteSpace(clbsData))
            {
                ParseClbsResponse(clbsData, location);
            }

            return location;
        }

        /// <summary>
        /// 获取基站定位地址信息 (AT+CLBS=2)
        /// </summary>
        /// <returns>地址查询结果</returns>
        public async Task<CellLocation> GetCellAddressAsync()
        {
            var location = new CellLocation();
            var clbsData = await SendClbsCommandAndReadPayloadAsync("AT+CLBS=2", 60000, 5000);
            if (!string.IsNullOrWhiteSpace(clbsData))
            {
                ParseClbsAddressResponse(clbsData, location);
            }

            return location;
        }

        /// <summary>
        /// 获取完整的基站定位信息（合并经纬度、地址等）
        /// 依次执行 AT+CLBS=1（经纬度）、AT+CLBS=2（地址）、AT+CLBS=4（扩展信息）
        /// </summary>
        /// <param name="includeAddress">是否包含地址查询（AT+CLBS=2），默认 true</param>
        /// <param name="includeExtended">是否包含扩展查询（AT+CLBS=4），默认 false</param>
        /// <returns>包含所有查询信息的定位结果</returns>
        public async Task<CellLocation> GetCompleteCellLocationAsync(bool includeAddress = true, bool includeExtended = false)
        {
            var location = new CellLocation();

            // 获取经纬度 (AT+CLBS=1)
            var locationData = await SendClbsCommandAndReadPayloadAsync("AT+CLBS=1", 600000, 5000);
            if (!string.IsNullOrWhiteSpace(locationData))
            {
                ParseClbsResponse(locationData, location);
            }

            // 获取地址 (AT+CLBS=2)
            if (includeAddress)
            {
                var addressData = await SendClbsCommandAndReadPayloadAsync("AT+CLBS=2", 60000, 5000);
                if (!string.IsNullOrWhiteSpace(addressData))
                {
                    ParseClbsAddressResponse(addressData, location);
                }
            }

            // 获取扩展信息 (AT+CLBS=4)
            if (includeExtended)
            {
                var extendedData = await SendClbsCommandAndReadPayloadAsync("AT+CLBS=4", 60000, 5000);
                if (!string.IsNullOrWhiteSpace(extendedData))
                {
                    ParseClbsResponse(extendedData, location);
                }
            }

            return location;
        }

        /// <summary>
        /// 获取当前基站信息（MCC/MNC/LAC/CellID）
        /// 优先查询 4G/LTE 的 EPS 域（AT+CEREG），回退到 2G/3G 的 CS 域（AT+CREG）
        /// </summary>
        public async Task<CellLocation> GetCellInfoAsync()
        {
            var location = new CellLocation();

            // 同时启用 CREG（2G/3G）和 CEREG（4G/LTE）扩展格式
            await _channel.SendCommandAsync("AT+CREG=2");
            await _channel.SendCommandAsync("AT+CEREG=2");

            try
            {
                // 优先查询 4G/LTE EPS 域（AT+CEREG），A76XX 通常注册在 LTE 上
                bool ceregParsed = false;
                var ceregResp = await _channel.SendCommandAsync("AT+CEREG?");
                foreach (var line in ceregResp.Lines)
                {
                    if (line.StartsWith("+CEREG:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(7).Trim();
                        string[] parts = data.Split(',');
                        // +CEREG: <n>,<stat>[,<tac>,<ci>,<AcT>]
                        // TAC (Tracking Area Code) is the LTE equivalent of LAC
                        if (parts.Length >= 4)
                        {
                            if (int.TryParse(parts[2].Trim().Trim('"'), System.Globalization.NumberStyles.HexNumber, null, out int tac))
                            {
                                location.Lac = tac;  // Store TAC in the Lac field (same concept, different name for LTE)
                                ceregParsed = true;
                            }
                            if (int.TryParse(parts[3].Trim().Trim('"'), System.Globalization.NumberStyles.HexNumber, null, out int ci))
                            {
                                location.CellId = ci;
                                ceregParsed = true;
                            }
                        }
                        break;
                    }
                }

                // Fall back to 2G/3G CS domain only when CEREG returned no cell data at all
                if (!ceregParsed)
                {
                    var cregResp = await _channel.SendCommandAsync("AT+CREG?");
                    foreach (var line in cregResp.Lines)
                    {
                        if (line.StartsWith("+CREG:", StringComparison.OrdinalIgnoreCase))
                        {
                            string data = line.Substring(6).Trim();
                            string[] parts = data.Split(',');
                            // +CREG: <n>,<stat>[,<lac>,<ci>,<AcTStatus>]
                            if (parts.Length >= 4)
                            {
                                if (int.TryParse(parts[2].Trim().Trim('"'), System.Globalization.NumberStyles.HexNumber, null, out int lac))
                                    location.Lac = lac;
                                if (int.TryParse(parts[3].Trim().Trim('"'), System.Globalization.NumberStyles.HexNumber, null, out int ci))
                                    location.CellId = ci;
                            }
                            break;
                        }
                    }
                }

                // 获取MCC/MNC
                var opsResp = await _channel.SendCommandAsync("AT+COPS?");
                foreach (var line in opsResp.Lines)
                {
                    if (line.StartsWith("+COPS:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(6).Trim();
                        string[] parts = data.Split(',');
                        if (parts.Length >= 3)
                        {
                            string mccMnc = parts[2].Trim().Trim('"');
                            if (mccMnc.Length >= 5)
                            {
                                if (int.TryParse(mccMnc.Substring(0, 3), out int mcc))
                                {
                                    location.Mcc = mcc;
                                    location.CountryName = MccMncLookup.GetCountryName(mcc);
                                }
                                if (int.TryParse(mccMnc.Substring(3), out int mnc))
                                {
                                    location.Mnc = mnc;
                                    if (location.Mcc > 0)
                                    {
                                        location.OperatorName = MccMncLookup.GetOperatorName(location.Mcc, mnc);
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
            finally
            {
                // 使用完基站信息后恢复默认注册模式
                await _channel.SendCommandAsync("AT+CREG=0");
                await _channel.SendCommandAsync("AT+CEREG=0");
            }

            location.IsValid = location.Lac > 0 || location.CellId > 0;
            return location;
        }

        /// <summary>
        /// 获取 GPS 定位信息（AT+CGPSINFO）
        /// </summary>
        /// <returns>GPS 定位结果</returns>
        public async Task<GpsLocation> GetGpsLocationAsync()
        {
            var response = await _channel.SendCommandAsync("AT+CGPSINFO", 20000);
            var gpsLocation = new GpsLocation();

            if (!response.IsOk)
            {
                gpsLocation.ErrorMessage = response.ErrorMessage;
                return gpsLocation;
            }

            foreach (var line in response.Lines)
            {
                if (!line.StartsWith("+CGPSINFO:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = line.Substring(10).Trim();
                var parts = data.Split(',');
                if (parts.Length < 4)
                {
                    gpsLocation.ErrorMessage = "GPS 响应格式无效";
                    return gpsLocation;
                }

                if (!TryParseGpsCoordinate(parts[0], parts[1], true, out var latitude) ||
                    !TryParseGpsCoordinate(parts[2], parts[3], false, out var longitude))
                {
                    gpsLocation.ErrorMessage = "GPS 坐标无效或未定位";
                    return gpsLocation;
                }

                gpsLocation.Latitude = latitude;
                gpsLocation.Longitude = longitude;
                gpsLocation.IsValid = true;

                if (parts.Length >= 7 &&
                    double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var altitude))
                {
                    gpsLocation.AltitudeMeters = altitude;
                }

                if (parts.Length >= 6 && TryParseGpsUtcTime(parts[4], parts[5], out var utcTimestamp))
                {
                    gpsLocation.UtcTimestamp = utcTimestamp;
                }

                return gpsLocation;
            }

            gpsLocation.ErrorMessage = "未返回 GPS 定位信息";
            return gpsLocation;
        }

        private async Task<string?> SendClbsCommandAndReadPayloadAsync(string command, int commandTimeoutMs, int urcWaitMs)
        {
            var urcTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object? _, string line)
            {
                if (!line.StartsWith("+CLBS:", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                urcTcs.TrySetResult(line.Substring(6).Trim());
            }

            _channel.UnsolicitedReceived += Handler;
            try
            {
                var resp = await _channel.SendCommandAsync(command, commandTimeoutMs);

                foreach (var line in resp.Lines)
                {
                    if (line.StartsWith("+CLBS:", StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring(6).Trim();
                    }
                }

                if (!resp.IsOk)
                {
                    return null;
                }

                var completed = await Task.WhenAny(urcTcs.Task, Task.Delay(urcWaitMs));
                if (completed == urcTcs.Task)
                {
                    return await urcTcs.Task;
                }

                return null;
            }
            finally
            {
                _channel.UnsolicitedReceived -= Handler;
            }
        }

        private static void ParseClbsResponse(string data, CellLocation location)
        {
            // 格式1（定位结果）: <errcode>,<latitude>,<longitude>,<accuracy>
            // 格式2（基站信息）: <n>,<MCC>,<MNC>,<LAC>,<CI>
            string[] parts = data.Split(',');
            if (parts.Length < 2) return;

            if (!int.TryParse(parts[0].Trim(), out int code)) return;

            if (code == 0 && parts.Length >= 4)
            {
                // 定位成功
                if (double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat))
                    location.Latitude = lat;
                if (double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                    location.Longitude = lon;
                if (int.TryParse(parts[3].Trim(), out int acc))
                    location.AccuracyMeters = acc;
                location.IsValid = true;
            }
            else if (parts.Length >= 5)
            {
                // 基站信息
                if (int.TryParse(parts[1].Trim(), out int mcc)) location.Mcc = mcc;
                if (int.TryParse(parts[2].Trim(), out int mnc)) location.Mnc = mnc;
                if (int.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.HexNumber, null, out int lac)) location.Lac = lac;
                if (int.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.HexNumber, null, out int ci)) location.CellId = ci;
                location.IsValid = true;
            }
        }

        private static void ParseClbsAddressResponse(string data, CellLocation location)
        {
            var commaIndex = data.IndexOf(',');
            if (commaIndex <= 0)
            {
                return;
            }

            var codeText = data[..commaIndex].Trim();
            if (!int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) || code != 0)
            {
                return;
            }

            var rawAddress = data[(commaIndex + 1)..].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(rawAddress))
            {
                return;
            }

            // 尝试 UCS2 解码
            string decodedAddress;
            if (IsHexString(rawAddress))
            {
                try
                {
                    decodedAddress = PduHelper.DecodeUcs2(rawAddress);
                    // 如果解码结果包含乱码（控制字符过多），则使用原始值
                    if (ContainsTooManyControlChars(decodedAddress))
                    {
                        decodedAddress = rawAddress;
                    }
                }
                catch
                {
                    decodedAddress = rawAddress;
                }
            }
            else
            {
                decodedAddress = rawAddress;
            }

            location.Address = decodedAddress;
            location.IsValid = true;
        }

        private static bool IsHexString(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // 如果字符串长度是偶数且全是十六进制字符，可能是 UCS2 编码
            if (text.Length % 2 != 0 || text.Length < 4)
            {
                return false;
            }

            foreach (char c in text)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsTooManyControlChars(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int controlCharCount = 0;
            foreach (char c in text)
            {
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                {
                    controlCharCount++;
                }
            }

            // 如果控制字符超过 20%，认为解码失败
            return controlCharCount > text.Length * 0.2;
        }

        private static bool TryParseGpsCoordinate(string coordinate, string direction, bool isLatitude, out double decimalDegree)
        {
            decimalDegree = default;

            if (string.IsNullOrWhiteSpace(coordinate) || string.IsNullOrWhiteSpace(direction))
            {
                return false;
            }

            if (!double.TryParse(coordinate, NumberStyles.Float, CultureInfo.InvariantCulture, out var rawValue) || rawValue <= 0)
            {
                return false;
            }

            var degreeDigits = isLatitude ? 2 : 3;
            var coordinateText = coordinate.Trim();
            if (coordinateText.Length <= degreeDigits)
            {
                return false;
            }

            if (!int.TryParse(coordinateText[..degreeDigits], NumberStyles.Integer, CultureInfo.InvariantCulture, out var degreePart))
            {
                return false;
            }

            if (!double.TryParse(coordinateText[degreeDigits..], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutePart))
            {
                return false;
            }

            decimalDegree = degreePart + minutePart / 60d;
            var normalizedDirection = direction.Trim().ToUpperInvariant();
            if (normalizedDirection is "S" or "W")
            {
                decimalDegree *= -1;
            }

            return true;
        }

        private static bool TryParseGpsUtcTime(string datePart, string timePart, out DateTimeOffset utcTimestamp)
        {
            utcTimestamp = default;

            if (string.IsNullOrWhiteSpace(datePart) || datePart.Length < 6 ||
                string.IsNullOrWhiteSpace(timePart) || timePart.Length < 6)
            {
                return false;
            }

            var utcDateTimeText = $"{datePart[..6]} {timePart[..6]}";
            if (!DateTime.TryParseExact(
                    utcDateTimeText,
                    "ddMMyy HHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var utcDateTime))
            {
                return false;
            }

            utcTimestamp = new DateTimeOffset(utcDateTime, TimeSpan.Zero);
            return true;
        }
    }
}
