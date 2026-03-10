using System;

namespace LexSMS.Models
{
    /// <summary>
    /// 短信消息
    /// </summary>
    public class SmsMessage
    {
        /// <summary>
        /// 消息索引（存储在SIM卡或模块中的序号）
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 发件人/收件人号码
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// 消息内容
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// 消息状态
        /// </summary>
        public SmsStatus Status { get; set; }
    }

    /// <summary>
    /// 短信状态枚举
    /// </summary>
    public enum SmsStatus
    {
        /// <summary>未读</summary>
        ReceivedUnread,
        /// <summary>已读</summary>
        ReceivedRead,
        /// <summary>待发送草稿</summary>
        StoredUnsent,
        /// <summary>已发送</summary>
        StoredSent,
        /// <summary>全部</summary>
        All
    }
}
