using System;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Events;
using LexSMS.Exceptions;
using LexSMS.Models;

namespace LexSMS.Features
{
    /// <summary>
    /// 通话管理器
    /// 实现拨打电话、接听电话、挂断电话、来电显示等功能
    /// </summary>
    public class CallManager
    {
        private readonly AtChannel _channel;
        private CallInfo _currentCall = new CallInfo { State = CallState.Idle };

        /// <summary>
        /// 来电事件
        /// </summary>
        public event EventHandler<IncomingCallEventArgs>? IncomingCall;

        /// <summary>
        /// 通话状态变更事件
        /// </summary>
        public event EventHandler<CallStateChangedEventArgs>? CallStateChanged;

        public CallManager(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _channel.UnsolicitedReceived += OnUnsolicitedReceived;
        }

        /// <summary>
        /// 初始化来电显示功能（启用CLIP）
        /// </summary>
        public async Task InitializeAsync()
        {
            // 启用来电显示 (CLIP - Calling Line Identification Presentation)
            var resp = await _channel.SendCommandAsync("AT+CLIP=1");
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+CLIP=1", resp.RawResponse);

            // 启用通话状态主动上报
            await _channel.SendCommandAsync("AT+CLCC");
        }

        /// <summary>
        /// 拨打电话
        /// </summary>
        /// <param name="phoneNumber">目标电话号码</param>
        public async Task<CallInfo> DialAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("电话号码不能为空", nameof(phoneNumber));

            // 国际号码需要ATD+<number>;
            string dialCmd = $"ATD{phoneNumber};";
            var resp = await _channel.SendCommandAsync(dialCmd, 30000);

            if (resp.IsError)
                throw new AtCommandErrorException(dialCmd, resp.RawResponse);

            _currentCall = new CallInfo
            {
                PhoneNumber = phoneNumber,
                State = CallState.Dialing,
                Direction = CallDirection.Outgoing,
                StartTime = DateTime.Now
            };

            OnCallStateChanged(_currentCall);
            return _currentCall;
        }

        /// <summary>
        /// 接听来电
        /// </summary>
        public async Task AnswerAsync()
        {
            var resp = await _channel.SendCommandAsync("ATA");
            if (resp.IsError)
                throw new AtCommandErrorException("ATA", resp.RawResponse);

            _currentCall.State = CallState.Active;
            if (_currentCall.StartTime == null)
                _currentCall.StartTime = DateTime.Now;

            OnCallStateChanged(_currentCall);
        }

        /// <summary>
        /// 挂断电话
        /// </summary>
        public async Task HangUpAsync()
        {
            var resp = await _channel.SendCommandAsync("ATH");
            if (resp.IsError)
                throw new AtCommandErrorException("ATH", resp.RawResponse);

            _currentCall.State = CallState.Disconnected;
            OnCallStateChanged(_currentCall);
            _currentCall = new CallInfo { State = CallState.Idle };
        }

        /// <summary>
        /// 拒接来电
        /// </summary>
        public async Task RejectAsync()
        {
            // 使用AT+CHUP拒接，或ATH
            var resp = await _channel.SendCommandAsync("AT+CHUP");
            if (resp.IsError)
                resp = await _channel.SendCommandAsync("ATH");

            _currentCall.State = CallState.Disconnected;
            OnCallStateChanged(_currentCall);
            _currentCall = new CallInfo { State = CallState.Idle };
        }

        /// <summary>
        /// 获取当前通话状态
        /// </summary>
        public async Task<CallInfo> GetCurrentCallAsync()
        {
            var resp = await _channel.SendCommandAsync("AT+CLCC");
            if (!resp.IsOk) return _currentCall;

            foreach (var line in resp.Lines)
            {
                if (line.StartsWith("+CLCC:", StringComparison.OrdinalIgnoreCase))
                {
                    // 解析 +CLCC: <idx>,<dir>,<stat>,<mode>,<mpty>[,<number>,<type>[,<alpha>]]
                    string data = line.Substring(6).Trim();
                    string[] parts = data.Split(',');
                    if (parts.Length >= 6)
                    {
                        int stat = int.TryParse(parts[2].Trim(), out int s) ? s : 0;
                        int dir = int.TryParse(parts[1].Trim(), out int d) ? d : 0;
                        string num = parts[5].Trim().Trim('"');

                        _currentCall.PhoneNumber = num;
                        _currentCall.Direction = dir == 0 ? CallDirection.Outgoing : CallDirection.Incoming;
                        _currentCall.State = stat switch
                        {
                            0 => CallState.Active,
                            1 => CallState.Held,
                            2 => CallState.Dialing,
                            3 => CallState.Ringing,
                            4 => CallState.Waiting,
                            _ => CallState.Idle
                        };
                    }
                    return _currentCall;
                }
            }

            // 没有活动通话
            return new CallInfo { State = CallState.Idle };
        }

        private void OnUnsolicitedReceived(object? sender, string urc)
        {
            // 来电显示: +CLIP: "<number>",<type>,...
            if (urc.StartsWith("+CLIP:", StringComparison.OrdinalIgnoreCase))
            {
                string data = urc.Substring(6).Trim();
                string[] parts = data.Split(',');
                if (parts.Length >= 1)
                {
                    string num = parts[0].Trim().Trim('"');
                    int numType = parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int t) ? t : 129;

                    _currentCall = new CallInfo
                    {
                        PhoneNumber = num,
                        State = CallState.Ringing,
                        Direction = CallDirection.Incoming
                    };
                    OnCallStateChanged(_currentCall);
                    IncomingCall?.Invoke(this, new IncomingCallEventArgs(num, numType));
                }
            }
            // 振铃: RING
            else if (urc == "RING")
            {
                if (_currentCall.State == CallState.Idle)
                {
                    _currentCall.State = CallState.Ringing;
                    _currentCall.Direction = CallDirection.Incoming;
                    OnCallStateChanged(_currentCall);
                }
            }
            // 通话结束: NO CARRIER
            else if (urc == "NO CARRIER" || urc == "BUSY" || urc == "NO ANSWER")
            {
                _currentCall.State = CallState.Disconnected;
                OnCallStateChanged(_currentCall);
                _currentCall = new CallInfo { State = CallState.Idle };
            }
        }

        private void OnCallStateChanged(CallInfo callInfo)
        {
            CallStateChanged?.Invoke(this, new CallStateChangedEventArgs(callInfo));
        }
    }
}
