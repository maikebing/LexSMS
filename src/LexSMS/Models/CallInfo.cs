using System;

namespace LexSMS.Models
{
    /// <summary>
    /// 通话信息
    /// </summary>
    public class CallInfo
    {
        /// <summary>
        /// 通话状态
        /// </summary>
        public CallState State { get; set; }

        /// <summary>
        /// 来电/拨出号码
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// 通话开始时间
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 通话方向
        /// </summary>
        public CallDirection Direction { get; set; }

        /// <summary>
        /// 通话持续时间
        /// </summary>
        public TimeSpan Duration =>
            StartTime.HasValue && State == CallState.Active
                ? DateTime.Now - StartTime.Value
                : TimeSpan.Zero;
    }

    /// <summary>
    /// 通话状态枚举
    /// </summary>
    public enum CallState
    {
        /// <summary>无通话</summary>
        Idle,
        /// <summary>拨出中（振铃中）</summary>
        Dialing,
        /// <summary>来电振铃</summary>
        Ringing,
        /// <summary>通话中</summary>
        Active,
        /// <summary>通话保持</summary>
        Held,
        /// <summary>通话等待</summary>
        Waiting,
        /// <summary>挂断</summary>
        Disconnected
    }

    /// <summary>
    /// 通话方向枚举
    /// </summary>
    public enum CallDirection
    {
        /// <summary>拨出</summary>
        Outgoing,
        /// <summary>来电</summary>
        Incoming
    }
}
