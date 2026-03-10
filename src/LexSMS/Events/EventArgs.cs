using System;
using LexSMS.Models;

namespace LexSMS.Events
{
    /// <summary>
    /// 来电事件参数
    /// </summary>
    public class IncomingCallEventArgs : EventArgs
    {
        /// <summary>
        /// 来电号码
        /// </summary>
        public string PhoneNumber { get; }

        /// <summary>
        /// 号码类型（129=普通，145=国际号码）
        /// </summary>
        public int NumberType { get; }

        public IncomingCallEventArgs(string phoneNumber, int numberType = 129)
        {
            PhoneNumber = phoneNumber;
            NumberType = numberType;
        }
    }

    /// <summary>
    /// 收到短信事件参数
    /// </summary>
    public class SmsReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// 短信存储位置索引
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 短信内容（如已直接推送）
        /// </summary>
        public SmsMessage? Message { get; }

        public SmsReceivedEventArgs(int index, SmsMessage? message = null)
        {
            Index = index;
            Message = message;
        }
    }

    /// <summary>
    /// MQTT消息接收事件参数
    /// </summary>
    public class MqttMessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// 收到的MQTT消息
        /// </summary>
        public MqttMessage Message { get; }

        public MqttMessageReceivedEventArgs(MqttMessage message)
        {
            Message = message;
        }
    }

    /// <summary>
    /// 通话状态变更事件参数
    /// </summary>
    public class CallStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 新的通话状态
        /// </summary>
        public CallInfo CallInfo { get; }

        public CallStateChangedEventArgs(CallInfo callInfo)
        {
            CallInfo = callInfo;
        }
    }

    /// <summary>
    /// MQTT连接状态变更事件参数
    /// </summary>
    public class MqttConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// 状态描述
        /// </summary>
        public string? Reason { get; }

        public MqttConnectionStateChangedEventArgs(bool isConnected, string? reason = null)
        {
            IsConnected = isConnected;
            Reason = reason;
        }
    }
}
