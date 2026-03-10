namespace LexSMS.Models
{
    /// <summary>
    /// SIM卡状态信息
    /// </summary>
    public class SimInfo
    {
        /// <summary>
        /// SIM卡状态
        /// </summary>
        public SimStatus Status { get; set; }

        /// <summary>
        /// 国际移动用户识别码 (IMSI)
        /// </summary>
        public string? Imsi { get; set; }

        /// <summary>
        /// 集成电路卡识别码 (ICCID)
        /// </summary>
        public string? Iccid { get; set; }

        /// <summary>
        /// 电话号码 (MSISDN)
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// 错误消息（查询失败时）
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// SIM卡状态枚举
    /// </summary>
    public enum SimStatus
    {
        /// <summary>SIM卡就绪</summary>
        Ready,
        /// <summary>需要PIN码</summary>
        PinRequired,
        /// <summary>需要PUK码</summary>
        PukRequired,
        /// <summary>需要手机PIN码</summary>
        PhonePinRequired,
        /// <summary>SIM卡未就绪</summary>
        NotReady,
        /// <summary>SIM卡不存在或未检测到</summary>
        Absent,
        /// <summary>未知状态</summary>
        Unknown
    }
}
