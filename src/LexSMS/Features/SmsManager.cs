using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Events;
using LexSMS.Exceptions;
using LexSMS.Helpers;
using LexSMS.Models;

namespace LexSMS.Features
{
    /// <summary>
    /// 短信管理器
    /// 实现中英文短信的发送和接收
    /// </summary>
    public class SmsManager
    {
        private readonly AtChannel _channel;

        /// <summary>
        /// 收到新短信事件
        /// </summary>
        public event EventHandler<SmsReceivedEventArgs>? SmsReceived;

        public SmsManager(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _channel.UnsolicitedReceived += OnUnsolicitedReceived;
        }

        /// <summary>
        /// 初始化短信功能
        /// </summary>
        public async Task InitializeAsync()
        {
            // 使用PDU模式（支持中文）
            var resp = await _channel.SendCommandAsync("AT+CMGF=0");
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+CMGF=0", resp.RawResponse);

            // 启用新短信主动上报，并将短信存储到SIM卡
            // AT+CNMI=2,1,0,0,0: 直接推送消息存储通知
            resp = await _channel.SendCommandAsync("AT+CNMI=2,1,0,0,0");
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+CNMI=2,1,0,0,0", resp.RawResponse);

            // 选择短信存储: SIM卡
            await _channel.SendCommandAsync("AT+CPMS=\"SM\",\"SM\",\"SM\"");
        }

        /// <summary>
        /// 发送短信（自动检测编码方式，支持中英文）
        /// </summary>
        /// <param name="phoneNumber">目标手机号码</param>
        /// <param name="message">短信内容</param>
        public async Task SendSmsAsync(string phoneNumber, string message)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("手机号码不能为空", nameof(phoneNumber));
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (message.Length > 70 && PduHelper.RequiresUcs2Encoding(message))
                throw new ArgumentException("UCS2编码短信不超过70个字符");
            if (message.Length > 160 && !PduHelper.RequiresUcs2Encoding(message))
                throw new ArgumentException("GSM7-bit编码短信不超过160个字符");

            var (pdu, tpduLength) = PduHelper.BuildSmsPdu(phoneNumber, message);

            // 发送AT+CMGS命令
            var promptResp = await _channel.SendCommandAsync($"AT+CMGS={tpduLength}", 10000);

            // 等待 > 提示符
            if (!promptResp.RawResponse.Contains(">"))
            {
                if (promptResp.IsError)
                    throw new AtCommandErrorException($"AT+CMGS={tpduLength}", promptResp.RawResponse);
                throw new ModemException("未收到短信输入提示符");
            }

            // 发送PDU数据 + Ctrl+Z
            _channel.SendRaw(pdu);
            _channel.SendCtrlZ();

            // 等待响应（短信发送可能需要30秒）
            await Task.Delay(500);
            var sendResp = await _channel.SendCommandAsync("", 30000);

            if (sendResp.IsError)
                throw new AtCommandErrorException("SMS Send", sendResp.RawResponse);
        }

        /// <summary>
        /// 读取指定索引的短信
        /// </summary>
        /// <param name="index">短信在存储中的索引</param>
        public async Task<SmsMessage?> ReadSmsAsync(int index)
        {
            // 切换到PDU模式
            await _channel.SendCommandAsync("AT+CMGF=0");

            var resp = await _channel.SendCommandAsync($"AT+CMGR={index}");
            if (!resp.IsOk) return null;

            // 解析响应
            // +CMGR: <stat>,,[len]
            // <pdu>
            string? pduLine = null;
            SmsStatus status = SmsStatus.ReceivedUnread;

            for (int i = 0; i < resp.Lines.Count; i++)
            {
                var line = resp.Lines[i];
                if (line.StartsWith("+CMGR:", StringComparison.OrdinalIgnoreCase))
                {
                    // 解析状态
                    string data = line.Substring(6).Trim();
                    string[] parts = data.Split(',');
                    if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int stat))
                    {
                        status = stat switch
                        {
                            0 => SmsStatus.ReceivedUnread,
                            1 => SmsStatus.ReceivedRead,
                            2 => SmsStatus.StoredUnsent,
                            3 => SmsStatus.StoredSent,
                            _ => SmsStatus.ReceivedRead
                        };
                    }
                    // 下一行是PDU
                    if (i + 1 < resp.Lines.Count)
                    {
                        pduLine = resp.Lines[i + 1].Trim();
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(pduLine)) return null;

            try
            {
                var (phoneNumber, message, timestamp) = PduHelper.DecodePdu(pduLine);
                return new SmsMessage
                {
                    Index = index,
                    PhoneNumber = phoneNumber,
                    Content = message,
                    Timestamp = timestamp,
                    Status = status
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 列出指定状态的所有短信
        /// </summary>
        /// <param name="status">短信状态过滤，默认列出所有</param>
        public async Task<List<SmsMessage>> ListSmsAsync(SmsStatus status = SmsStatus.All)
        {
            await _channel.SendCommandAsync("AT+CMGF=0");

            int statusCode = status switch
            {
                SmsStatus.ReceivedUnread => 0,
                SmsStatus.ReceivedRead => 1,
                SmsStatus.StoredUnsent => 2,
                SmsStatus.StoredSent => 3,
                SmsStatus.All => 4,
                _ => 4
            };

            var resp = await _channel.SendCommandAsync($"AT+CMGL={statusCode}", 30000);
            var messages = new List<SmsMessage>();

            if (!resp.IsOk) return messages;

            for (int i = 0; i < resp.Lines.Count; i++)
            {
                var line = resp.Lines[i];
                if (line.StartsWith("+CMGL:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = line.Substring(6).Trim();
                    string[] parts = data.Split(',');
                    if (parts.Length < 2) continue;

                    if (!int.TryParse(parts[0].Trim(), out int idx)) continue;
                    if (!int.TryParse(parts[1].Trim(), out int stat)) stat = 1;

                    SmsStatus msgStatus = stat switch
                    {
                        0 => SmsStatus.ReceivedUnread,
                        1 => SmsStatus.ReceivedRead,
                        2 => SmsStatus.StoredUnsent,
                        3 => SmsStatus.StoredSent,
                        _ => SmsStatus.ReceivedRead
                    };

                    // 下一行是PDU
                    if (i + 1 < resp.Lines.Count)
                    {
                        string pduLine = resp.Lines[i + 1].Trim();
                        i++;
                        try
                        {
                            var (phoneNumber, message, timestamp) = PduHelper.DecodePdu(pduLine);
                            messages.Add(new SmsMessage
                            {
                                Index = idx,
                                PhoneNumber = phoneNumber,
                                Content = message,
                                Timestamp = timestamp,
                                Status = msgStatus
                            });
                        }
                        catch (Exception)
                        {
                            // 跳过无法解析的短信
                        }
                    }
                }
            }

            return messages;
        }

        /// <summary>
        /// 删除指定索引的短信
        /// </summary>
        public async Task DeleteSmsAsync(int index)
        {
            var resp = await _channel.SendCommandAsync($"AT+CMGD={index}");
            if (!resp.IsOk)
                throw new AtCommandErrorException($"AT+CMGD={index}", resp.RawResponse);
        }

        /// <summary>
        /// 删除所有短信
        /// </summary>
        public async Task DeleteAllSmsAsync()
        {
            // 删除所有短信 (flag=4: 删除所有)
            var resp = await _channel.SendCommandAsync("AT+CMGD=1,4");
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+CMGD=1,4", resp.RawResponse);
        }

        private void OnUnsolicitedReceived(object? sender, string urc)
        {
            // 新短信通知: +CMTI: "SM",<index>
            if (urc.StartsWith("+CMTI:", StringComparison.OrdinalIgnoreCase))
            {
                string data = urc.Substring(6).Trim();
                string[] parts = data.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int idx))
                {
                    SmsReceived?.Invoke(this, new SmsReceivedEventArgs(idx));
                }
            }
            // 直接推送短信内容: +CMT: ...
            else if (urc.StartsWith("+CMT:", StringComparison.OrdinalIgnoreCase))
            {
                SmsReceived?.Invoke(this, new SmsReceivedEventArgs(-1));
            }
        }
    }
}
