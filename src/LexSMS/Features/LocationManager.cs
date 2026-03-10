using System;
using System.Globalization;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Exceptions;
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
            // AT+CLBS=1 : 获取基站定位（通过网络），文档格式为 AT+CLBS=<mode>
            var resp = await _channel.SendCommandAsync("AT+CLBS=1", 60000);

            var location = new CellLocation();

            foreach (var line in resp.Lines)
            {
                if (line.StartsWith("+CLBS:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = line.Substring(6).Trim();
                    ParseClbsResponse(data, location);
                    break;
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
                                    location.Mcc = mcc;
                                if (int.TryParse(mccMnc.Substring(3), out int mnc))
                                    location.Mnc = mnc;
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
    }
}
