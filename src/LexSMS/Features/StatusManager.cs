using System;
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

            // 型号
            var modelResp = await _channel.SendCommandAsync("AT+CGMM");
            if (modelResp.IsOk)
                info.Model = modelResp.FirstLine?.Trim();

            // 固件版本
            var fwResp = await _channel.SendCommandAsync("AT+CGMR");
            if (fwResp.IsOk)
                info.FirmwareVersion = fwResp.FirstLine?.Trim();

            // IMEI
            var imeiResp = await _channel.SendCommandAsync("AT+CGSN");
            if (imeiResp.IsOk)
                info.Imei = imeiResp.FirstLine?.Trim();

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

            info.Status = ModuleStatus.Ready;
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
            else
            {
                info.Status = SimStatus.Absent;
            }

            if (info.Status != SimStatus.Ready) return info;

            // IMSI
            var imsiResp = await _channel.SendCommandAsync("AT+CIMI");
            if (imsiResp.IsOk)
                info.Imsi = imsiResp.FirstLine?.Trim();

            // ICCID
            var iccidResp = await _channel.SendCommandAsync("AT+CCID");
            if (iccidResp.IsOk)
            {
                foreach (var line in iccidResp.Lines)
                {
                    if (line.StartsWith("+CCID:", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Iccid = line.Substring(6).Trim().Trim('"');
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

            // 网络注册状态
            var regResp = await _channel.SendCommandAsync("AT+CREG?");
            if (regResp.IsOk)
            {
                foreach (var line in regResp.Lines)
                {
                    if (line.StartsWith("+CREG:", StringComparison.OrdinalIgnoreCase))
                    {
                        string data = line.Substring(6).Trim();
                        string[] parts = data.Split(',');
                        int statPart = parts.Length >= 2 ? 1 : 0;
                        if (int.TryParse(parts[statPart].Trim(), out int stat))
                        {
                            info.RegistrationStatus = (NetworkRegistrationStatus)stat;
                        }
                        break;
                    }
                }
            }

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
        /// 重置模块
        /// </summary>
        public async Task ResetAsync()
        {
            await _channel.SendCommandAsync("AT+CRESET", 5000);
        }
    }
}
