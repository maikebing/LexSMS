namespace LexSMS.Models
{
    /// <summary>
    /// 网络时间同步结果（AT+CNTP）
    /// </summary>
    public class NtpSyncResult
    {
        /// <summary>
        /// 同步结果码
        /// </summary>
        public NtpSyncStatus Status { get; set; } = NtpSyncStatus.Unknown;

        /// <summary>
        /// 原始返回码（例如 +CNTP: 0 中的 0）
        /// </summary>
        public int? RawCode { get; set; }

        /// <summary>
        /// 错误消息（命令失败或无法解析时）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 是否同步成功
        /// </summary>
        public bool IsSuccess => Status == NtpSyncStatus.Success;
    }

    /// <summary>
    /// NTP 网络时间同步状态码
    /// </summary>
    public enum NtpSyncStatus
    {
        /// <summary>同步成功</summary>
        Success = 1,
        /// <summary>网络错误</summary>
        NetworkError = 61,
        /// <summary>DNS 解析错误</summary>
        DnsResolveError = 62,
        /// <summary>连接错误</summary>
        ConnectionError = 63,
        /// <summary>服务响应错误</summary>
        ServerResponseError = 64,
        /// <summary>服务响应超时</summary>
        ServerResponseTimeout = 65,
        /// <summary>未知状态</summary>
        Unknown = 0
    }
}
