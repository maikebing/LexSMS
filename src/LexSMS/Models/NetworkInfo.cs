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
        /// 错误消息（查询失败时）
        /// </summary>
        public string? ErrorMessage { get; set; }

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
        /// CS 域网络注册状态（用于语音和短信）- AT+CREG
        /// </summary>
        public NetworkRegistrationStatus CsRegistrationStatus { get; set; } = NetworkRegistrationStatus.Unknown;

        /// <summary>
        /// GPRS/PS 域网络注册状态（用于 GPRS 数据）- AT+CGREG
        /// </summary>
        public NetworkRegistrationStatus GprsRegistrationStatus { get; set; } = NetworkRegistrationStatus.Unknown;

        /// <summary>
        /// EPS/LTE 域网络注册状态（用于 4G 数据）- AT+CEREG
        /// </summary>
        public NetworkRegistrationStatus EpsRegistrationStatus { get; set; } = NetworkRegistrationStatus.Unknown;

        /// <summary>
        /// 综合网络注册状态（优先级：EPS > GPRS > CS）
        /// </summary>
        public NetworkRegistrationStatus RegistrationStatus
        {
            get
            {
                // 优先使用 EPS（4G），其次 GPRS（3G），最后 CS（2G）
                if (EpsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome ||
                    EpsRegistrationStatus == NetworkRegistrationStatus.RegisteredRoaming)
                    return EpsRegistrationStatus;

                if (GprsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome ||
                    GprsRegistrationStatus == NetworkRegistrationStatus.RegisteredRoaming)
                    return GprsRegistrationStatus;

                if (CsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome ||
                    CsRegistrationStatus == NetworkRegistrationStatus.RegisteredRoaming)
                    return CsRegistrationStatus;

                // 如果都未注册，优先返回搜索状态
                if (EpsRegistrationStatus == NetworkRegistrationStatus.Searching ||
                    GprsRegistrationStatus == NetworkRegistrationStatus.Searching ||
                    CsRegistrationStatus == NetworkRegistrationStatus.Searching)
                    return NetworkRegistrationStatus.Searching;

                // 如果有拒绝状态，返回拒绝
                if (EpsRegistrationStatus == NetworkRegistrationStatus.RegistrationDenied ||
                    GprsRegistrationStatus == NetworkRegistrationStatus.RegistrationDenied ||
                    CsRegistrationStatus == NetworkRegistrationStatus.RegistrationDenied)
                    return NetworkRegistrationStatus.RegistrationDenied;

                // 返回最有价值的状态
                if (EpsRegistrationStatus != NetworkRegistrationStatus.Unknown)
                    return EpsRegistrationStatus;
                if (GprsRegistrationStatus != NetworkRegistrationStatus.Unknown)
                    return GprsRegistrationStatus;
                if (CsRegistrationStatus != NetworkRegistrationStatus.Unknown)
                    return CsRegistrationStatus;

                return NetworkRegistrationStatus.Unknown;
            }
        }

        /// <summary>
        /// 运营商名称
        /// </summary>
        public string? OperatorName { get; set; }

        /// <summary>
        /// 网络接入类型
        /// </summary>
        public NetworkAccessType AccessType { get; set; }

        /// <summary>
        /// GPRS/PS 域附着状态（用于数据连接）
        /// </summary>
        public GprsAttachStatus GprsAttachStatus { get; set; } = GprsAttachStatus.Unknown;

        /// <summary>
        /// 是否已注册网络（任意域已注册即可）
        /// </summary>
        public bool IsRegistered =>
            RegistrationStatus == NetworkRegistrationStatus.RegisteredHome ||
            RegistrationStatus == NetworkRegistrationStatus.RegisteredRoaming;

        /// <summary>
        /// 是否已附着到 PS 域（可以进行数据通信）
        /// </summary>
        public bool IsAttached => GprsAttachStatus == GprsAttachStatus.Attached;

        /// <summary>
        /// 错误消息（查询失败时）
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// GPRS/PS 域附着状态枚举
    /// </summary>
    public enum GprsAttachStatus
    {
        /// <summary>未知状态</summary>
        Unknown = -1,
        /// <summary>未附着（不能进行数据通信）</summary>
        Detached = 0,
        /// <summary>已附着（可以进行数据通信）</summary>
        Attached = 1
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
