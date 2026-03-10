namespace LexSMS.Models
{
    /// <summary>
    /// 信号质量信息
    /// </summary>
    public class SignalInfo
    {
        /// <summary>
        /// 信号强度值 (RSSI)，范围 0-31，99 表示未知
        /// </summary>
        public int Rssi { get; set; }

        /// <summary>
        /// 误码率，范围 0-7，99 表示未知
        /// </summary>
        public int Ber { get; set; }

        /// <summary>
        /// 信号强度（dBm）
        /// </summary>
        public int RssiDbm => Rssi == 99 ? -999 : -113 + Rssi * 2;

        /// <summary>
        /// 信号强度描述
        /// </summary>
        public string SignalLevel
        {
            get
            {
                if (Rssi == 99) return "未知";
                if (Rssi >= 20) return "强";
                if (Rssi >= 15) return "良";
                if (Rssi >= 10) return "中";
                if (Rssi >= 5) return "弱";
                return "极弱";
            }
        }
    }

    /// <summary>
    /// 网络注册信息
    /// </summary>
    public class NetworkInfo
    {
        /// <summary>
        /// 网络注册状态
        /// </summary>
        public NetworkRegistrationStatus RegistrationStatus { get; set; }

        /// <summary>
        /// 运营商名称
        /// </summary>
        public string? OperatorName { get; set; }

        /// <summary>
        /// 网络接入类型
        /// </summary>
        public NetworkAccessType AccessType { get; set; }

        /// <summary>
        /// 是否已注册网络
        /// </summary>
        public bool IsRegistered =>
            RegistrationStatus == NetworkRegistrationStatus.RegisteredHome ||
            RegistrationStatus == NetworkRegistrationStatus.RegisteredRoaming;
    }

    /// <summary>
    /// 网络注册状态枚举
    /// </summary>
    public enum NetworkRegistrationStatus
    {
        NotRegistered = 0,
        RegisteredHome = 1,
        Searching = 2,
        RegistrationDenied = 3,
        Unknown = 4,
        RegisteredRoaming = 5,
        RegisteredHomeSms = 6,
        RegisteredRoamingSms = 7
    }

    /// <summary>
    /// 网络接入类型枚举
    /// </summary>
    public enum NetworkAccessType
    {
        GSM = 0,
        GSMCompact = 1,
        WCDMA = 2,
        EDGE = 3,
        HSDPA = 4,
        HSUPA = 5,
        HSPA = 6,
        LTE = 7,
        LTE_CA = 25,
        NR = 31,
        Unknown = 99
    }
}
