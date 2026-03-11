using System;
using System.Globalization;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Exceptions;
using LexSMS.Models;

namespace LexSMS.Features
{
    /// <summary>
    /// 状态管理器
    /// 查询A76XX模块状态、SIM卡状态和网络信息
    /// </summary>
    public class StatusManager
    {
        private readonly AtChannel _channel;

        public StatusManager(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>
        /// 获取模块信息（制造商、型号、固件版本、IMEI）
        /// </summary>
        public async Task<ModuleInfo> GetModuleInfoAsync()
        {
            var info = new ModuleInfo();

            // 制造商
            var mfgResp = await _channel.SendCommandAsync("AT+CGMI");
            if (mfgResp.IsOk)
                info.Manufacturer = mfgResp.FirstLine?.Trim();
            else if (mfgResp.IsError)
                info.ErrorMessage = mfgResp.ErrorMessage;

            // 型号
            var modelResp = await _channel.SendCommandAsync("AT+CGMM");
            if (modelResp.IsOk)
                info.Model = modelResp.FirstLine?.Trim();
            else if (modelResp.IsError && info.ErrorMessage == null)
                info.ErrorMessage = modelResp.ErrorMessage;

            // 固件版本
            var fwResp = await _channel.SendCommandAsync("AT+CGMR");
            if (fwResp.IsOk)
                info.FirmwareVersion = fwResp.FirstLine?.Trim();
            else if (fwResp.IsError && info.ErrorMessage == null)
                info.ErrorMessage = fwResp.ErrorMessage;

            // IMEI
            var imeiResp = await _channel.SendCommandAsync("AT+CGSN");
            if (imeiResp.IsOk)
                info.Imei = imeiResp.FirstLine?.Trim();
            else if (imeiResp.IsError && info.ErrorMessage == null)
                info.ErrorMessage = imeiResp.ErrorMessage;

            // 电池状态
            var battResp = await _channel.SendCommandAsync("AT+CBC");
            if (battResp.IsOk)
            {
                foreach (var line in battResp.Lines)
                {
                    if (line.StartsWith("+CBC:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(5).Trim();
                        string[] parts = data.Split(',');
                        // +CBC: <bcs>,<bcl>,<voltage>
                        if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int bcl))
                            info.BatteryPercent = bcl;
                        if (parts.Length >= 3 && int.TryParse(parts[2].Trim(), out int volt))
                            info.VoltageMillivolts = volt;
                        break;
                    }
                }
            }
            else if (battResp.IsError && info.ErrorMessage == null)
            {
                info.ErrorMessage = battResp.ErrorMessage;
            }

            // 根据是否有错误消息设置状态
            info.Status = info.ErrorMessage != null ? ModuleStatus.Error : ModuleStatus.Ready;
            return info;
        }

        /// <summary>
        /// 获取SIM卡状态信息
        /// </summary>
        public async Task<SimInfo> GetSimInfoAsync()
        {
            var info = new SimInfo();

            // SIM卡状态
            var pinResp = await _channel.SendCommandAsync("AT+CPIN?");
            if (pinResp.IsOk)
            {
                foreach (var line in pinResp.Lines)
                {
                    if (line.StartsWith("+CPIN:", StringComparison.OrdinalIgnoreCase))
                    {
                        string status = line.Substring(6).Trim();
                        info.Status = status switch
                        {
                            "READY" => SimStatus.Ready,
                            "SIM PIN" => SimStatus.PinRequired,
                            "SIM PUK" => SimStatus.PukRequired,
                            "PH-SIM PIN" => SimStatus.PhonePinRequired,
                            _ => SimStatus.Unknown
                        };
                        break;
                    }
                }
            }
            else if (pinResp.IsError)
            {
                // SIM卡错误（如未插入）
                info.Status = SimStatus.Absent;
                info.ErrorMessage = pinResp.ErrorMessage;
            }
            else
            {
                info.Status = SimStatus.Absent;
            }

            if (info.Status != SimStatus.Ready) return info;

            // IMSI
            var imsiResp = await _channel.SendCommandAsync("AT+CIMI");
            if (imsiResp.IsOk)
                info.Imsi = imsiResp.FirstLine?.Trim();

            // ICCID - A76XX uses AT+CICCID, response: +ICCID: <iccid>
            var iccidResp = await _channel.SendCommandAsync("AT+CICCID");
            if (iccidResp.IsOk)
            {
                foreach (var line in iccidResp.Lines)
                {
                    if (line.StartsWith("+ICCID:", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Iccid = line.Substring(7).Trim().Trim('"');
                        break;
                    }
                    else if (!string.IsNullOrWhiteSpace(line) && line != "OK")
                    {
                        info.Iccid = line.Trim();
                        break;
                    }
                }
            }

            // 电话号码（并非所有SIM卡都支持）
            var numResp = await _channel.SendCommandAsync("AT+CNUM");
            if (numResp.IsOk)
            {
                foreach (var line in numResp.Lines)
                {
                    if (line.StartsWith("+CNUM:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(6).Trim();
                        string[] parts = data.Split(',');
                        if (parts.Length >= 2)
                            info.PhoneNumber = parts[1].Trim().Trim('"');
                        break;
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// 获取信号强度信息
        /// </summary>
        public async Task<SignalInfo> GetSignalInfoAsync()
        {
            var info = new SignalInfo { Rssi = 99, Ber = 99 };

            var resp = await _channel.SendCommandAsync("AT+CSQ");
            if (resp.IsError)
            {
                info.ErrorMessage = resp.ErrorMessage;
                return info;
            }

            if (!resp.IsOk) return info;

            foreach (var line in resp.Lines)
            {
                if (line.StartsWith("+CSQ:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = line.Substring(5).Trim();
                    string[] parts = data.Split(',');
                    if (parts.Length >= 2)
                    {
                        if (int.TryParse(parts[0].Trim(), out int rssi)) info.Rssi = rssi;
                        if (int.TryParse(parts[1].Trim(), out int ber)) info.Ber = ber;
                    }
                    break;
                }
            }

            return info;
        }

        /// <summary>
        /// 获取网络注册信息和运营商信息
        /// </summary>
        public async Task<NetworkInfo> GetNetworkInfoAsync()
        {
            var info = new NetworkInfo();

            // CS 域网络注册状态 (AT+CREG) - 用于语音和短信
            var cregResp = await _channel.SendCommandAsync("AT+CREG?");
            if (cregResp.IsOk)
            {
                foreach (var line in cregResp.Lines)
                {
                    // 响应格式：+CREG: <n>,<stat>[,<lac>,<ci>]
                    // 或：+CREG: <stat>
                    if (line.StartsWith("+CREG:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(6).Trim();
                        string[] parts = data.Split(',');
                        int statPart = parts.Length >= 2 ? 1 : 0;

                        if (int.TryParse(parts[statPart].Trim(), out int stat))
                        {
                            info.CsRegistrationStatus = (NetworkRegistrationStatus)stat;
                        }
                        break;
                    }
                }
            }
            else if (cregResp.IsError)
            {
                info.ErrorMessage = cregResp.ErrorMessage;
            }

            // GPRS/PS 域网络注册状态 (AT+CGREG) - 用于 GPRS 数据
            var cgregResp = await _channel.SendCommandAsync("AT+CGREG?");
            if (cgregResp.IsOk)
            {
                foreach (var line in cgregResp.Lines)
                {
                    // 响应格式：+CGREG: <n>,<stat>[,<lac>,<ci>,<AcT>]
                    if (line.StartsWith("+CGREG:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(7).Trim();
                        string[] parts = data.Split(',');
                        int statPart = parts.Length >= 2 ? 1 : 0;

                        if (int.TryParse(parts[statPart].Trim(), out int stat))
                        {
                            info.GprsRegistrationStatus = (NetworkRegistrationStatus)stat;
                        }
                        break;
                    }
                }
            }
            else if (cgregResp.IsError && info.ErrorMessage == null)
            {
                info.ErrorMessage = cgregResp.ErrorMessage;
            }

            // EPS/LTE 域网络注册状态 (AT+CEREG) - 用于 4G/LTE 数据
            var ceregResp = await _channel.SendCommandAsync("AT+CEREG?");
            if (ceregResp.IsOk)
            {
                foreach (var line in ceregResp.Lines)
                {
                    // 响应格式：+CEREG: <n>,<stat>[,<tac>,<ci>,<AcT>]
                    if (line.StartsWith("+CEREG:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(7).Trim();
                        string[] parts = data.Split(',');
                        int statPart = parts.Length >= 2 ? 1 : 0;

                        if (int.TryParse(parts[statPart].Trim(), out int stat))
                        {
                            info.EpsRegistrationStatus = (NetworkRegistrationStatus)stat;
                        }
                        break;
                    }
                }
            }
            // CEREG 可能不被所有模块支持，所以不记录错误

            // 运营商信息
            var opsResp = await _channel.SendCommandAsync("AT+COPS?");
            if (opsResp.IsOk)
            {
                foreach (var line in opsResp.Lines)
                {
                    if (line.StartsWith("+COPS:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(6).Trim();
                        string[] parts = data.Split(',');
                        if (parts.Length >= 3)
                        {
                            info.OperatorName = parts[2].Trim().Trim('"');
                        }
                        if (parts.Length >= 4 && int.TryParse(parts[3].Trim(), out int act))
                        {
                            info.AccessType = act switch
                            {
                                0 => NetworkAccessType.GSM,
                                1 => NetworkAccessType.GSMCompact,
                                2 => NetworkAccessType.WCDMA,
                                3 => NetworkAccessType.EDGE,
                                4 => NetworkAccessType.HSDPA,
                                5 => NetworkAccessType.HSUPA,
                                6 => NetworkAccessType.HSPA,
                                7 => NetworkAccessType.LTE,
                                11 => NetworkAccessType.LTE,
                                12 => NetworkAccessType.LTE_CA,
                                _ => NetworkAccessType.Unknown
                            };
                        }
                        break;
                    }
                }
            }
            else if (opsResp.IsError && info.ErrorMessage == null)
            {
                info.ErrorMessage = opsResp.ErrorMessage;
            }

            // GPRS 附着状态
            var attachResp = await _channel.SendCommandAsync("AT+CGATT?");
            if (attachResp.IsOk)
            {
                foreach (var line in attachResp.Lines)
                {
                    if (line.StartsWith("+CGATT:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(7).Trim();
                        if (int.TryParse(data, out int state))
                        {
                            info.GprsAttachStatus = (GprsAttachStatus)state;
                        }
                        break;
                    }
                }
            }
            else if (attachResp.IsError && info.ErrorMessage == null)
            {
                info.ErrorMessage = attachResp.ErrorMessage;
            }

            return info;
        }

        /// <summary>
        /// 发送AT测试命令（测试模块是否响应）
        /// </summary>
        public async Task<bool> PingAsync()
        {
            var resp = await _channel.SendCommandAsync("AT", 3000);
            return resp.IsOk;
        }

        /// <summary>
        /// 设置 GPRS/PS 域附着状态
        /// </summary>
        /// <param name="attach">true=附着，false=分离</param>
        public async Task<bool> SetGprsAttachAsync(bool attach)
        {
            int state = attach ? 1 : 0;
            var resp = await _channel.SendCommandAsync($"AT+CGATT={state}", 75000); // GPRS 附着可能需要较长时间
            return resp.IsOk;
        }

        /// <summary>
        /// 查询 GPRS/PS 域附着状态
        /// </summary>
        public async Task<GprsAttachStatus> GetGprsAttachStatusAsync()
        {
            var resp = await _channel.SendCommandAsync("AT+CGATT?");
            if (!resp.IsOk) return GprsAttachStatus.Unknown;

            foreach (var line in resp.Lines)
            {
                if (line.StartsWith("+CGATT:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = line.Substring(7).Trim();
                    if (int.TryParse(data, out int state))
                    {
                        return (GprsAttachStatus)state;
                    }
                }
            }

            return GprsAttachStatus.Unknown;
        }

        /// <summary>
        /// 查询模块分配的 IP 地址 (AT+CGPADDR)
        /// </summary>
        /// <returns>IP 地址字符串，查询失败返回 null</returns>
        public async Task<string?> GetIpAddressAsync()
        {
            var resp = await _channel.SendCommandAsync("AT+CGPADDR");
            if (!resp.IsOk) return null;

            foreach (var line in resp.Lines)
            {
                // 响应格式: +CGPADDR: <cid>,<addr>
                if (line.StartsWith("+CGPADDR:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = line.Substring(9).Trim();
                    int commaIdx = data.IndexOf(',');
                    if (commaIdx >= 0)
                    {
                        string addr = data.Substring(commaIdx + 1).Trim().Trim('"');
                        if (!string.IsNullOrEmpty(addr))
                            return addr;
                    }
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// 重置模块
        /// </summary>
        public async Task ResetAsync()
        {
            await _channel.SendCommandAsync("AT+CRESET", 5000);
        }

        /// <summary>
        /// 查询模块当前时间（AT+CCLK?）
        /// </summary>
        /// <returns>模块时钟时间，解析失败返回 null</returns>
        public async Task<DateTimeOffset?> GetModuleClockAsync()
        {
            var resp = await _channel.SendCommandAsync("AT+CCLK?");
            if (!resp.IsOk)
            {
                return null;
            }

            foreach (var line in resp.Lines)
            {
                if (!line.StartsWith("+CCLK:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rawClock = line.Substring(6).Trim().Trim('"');
                if (TryParseModuleClock(rawClock, out var clock))
                {
                    return clock;
                }
            }

            return null;
        }

        /// <summary>
        /// 执行 NTP 网络时间同步（AT+CNTP）
        /// </summary>
        /// <returns>同步结果，包含状态码和错误信息</returns>
        public async Task<NtpSyncResult> SyncNetworkTimeAsync()
        {
            // CNTP 执行可能涉及网络交互，预留更长超时
            var resp = await _channel.SendCommandAsync("AT+CNTP", 65000);
            var result = new NtpSyncResult();

            if (resp.IsError)
            {
                result.ErrorMessage = resp.ErrorMessage;
                return result;
            }

            if (!resp.IsOk)
            {
                result.ErrorMessage = "AT+CNTP 未返回成功响应";
                return result;
            }

            foreach (var line in resp.Lines)
            {
                if (!line.StartsWith("+CNTP:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = line.Substring(6).Trim();
                if (!int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                {
                    result.ErrorMessage = $"无法解析 CNTP 返回码: {data}";
                    return result;
                }

                result.RawCode = code;
                result.Status = MapNtpStatus(code);
                return result;
            }

            result.ErrorMessage = "未收到 +CNTP 返回行";
            return result;
        }

        private static NtpSyncStatus MapNtpStatus(int code)
        {
            return code switch
            {
                0 or 1 => NtpSyncStatus.Success,
                61 => NtpSyncStatus.NetworkError,
                62 => NtpSyncStatus.DnsResolveError,
                63 => NtpSyncStatus.ConnectionError,
                64 => NtpSyncStatus.ServerResponseError,
                65 => NtpSyncStatus.ServerResponseTimeout,
                _ => NtpSyncStatus.Unknown
            };
        }

        private static bool TryParseModuleClock(string value, out DateTimeOffset clock)
        {
            clock = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split(',', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!DateTime.TryParseExact(parts[0], "yy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datePart))
            {
                return false;
            }

            if (parts[1].Length < 8)
            {
                return false;
            }

            var timePart = parts[1][..8];
            if (!TimeSpan.TryParseExact(timePart, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out var timeOfDay))
            {
                return false;
            }

            var offsetPart = parts[1].Length > 8 ? parts[1][8..] : "+00";
            if (!TryParseQuarterHourOffset(offsetPart, out var offset))
            {
                return false;
            }

            var localDateTime = new DateTime(datePart.Year, datePart.Month, datePart.Day,
                timeOfDay.Hours, timeOfDay.Minutes, timeOfDay.Seconds, DateTimeKind.Unspecified);
            clock = new DateTimeOffset(localDateTime, offset);
            return true;
        }

        private static bool TryParseQuarterHourOffset(string value, out TimeSpan offset)
        {
            offset = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var sign = value[0];
            if (sign != '+' && sign != '-')
            {
                return false;
            }

            if (!int.TryParse(value[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quarterHours))
            {
                return false;
            }

            var totalMinutes = quarterHours * 15;
            if (sign == '-')
            {
                totalMinutes *= -1;
            }

            offset = TimeSpan.FromMinutes(totalMinutes);
            return true;
        }
    }
}
