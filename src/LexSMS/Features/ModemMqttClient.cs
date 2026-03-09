using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Events;
using LexSMS.Exceptions;
using LexSMS.Models;

namespace LexSMS.Features
{
    /// <summary>
    /// MQTT客户端
    /// 通过A76XX模块实现MQTT连接、发布和订阅
    /// </summary>
    public class ModemMqttClient
    {
        private readonly AtChannel _channel;
        private bool _isConnected;

        /// <summary>
        /// MQTT消息接收事件
        /// </summary>
        public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

        /// <summary>
        /// MQTT连接状态变更事件
        /// </summary>
        public event EventHandler<MqttConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _isConnected;

        public ModemMqttClient(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _channel.UnsolicitedReceived += OnUnsolicitedReceived;
        }

        /// <summary>
        /// 连接MQTT Broker
        /// </summary>
        /// <param name="config">MQTT配置</param>
        public async Task ConnectAsync(MqttConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.BrokerAddress))
                throw new ArgumentException("Broker地址不能为空", nameof(config));

            // 配置MQTT URL: tcp://<host>:<port>
            string protocol = config.UseSsl ? "ssl" : "tcp";
            string url = $"{protocol}://{config.BrokerAddress}:{config.Port}";

            var urlResp = await _channel.SendCommandAsync($"AT+SMCONF=\"URL\",\"{url}\"");
            if (!urlResp.IsOk)
                throw new AtCommandErrorException("AT+SMCONF URL", urlResp.RawResponse);

            // 配置Keep-Alive
            await _channel.SendCommandAsync($"AT+SMCONF=\"KEEPTIME\",{config.KeepAliveSeconds}");

            // 配置Client ID
            if (!string.IsNullOrEmpty(config.ClientId))
                await _channel.SendCommandAsync($"AT+SMCONF=\"CLIENTID\",\"{config.ClientId}\"");

            // 配置用户名密码
            if (!string.IsNullOrEmpty(config.Username))
            {
                await _channel.SendCommandAsync($"AT+SMCONF=\"USERNAME\",\"{config.Username}\"");
                if (!string.IsNullOrEmpty(config.Password))
                    await _channel.SendCommandAsync($"AT+SMCONF=\"PASSWORD\",\"{config.Password}\"");
            }

            // 配置清除会话
            await _channel.SendCommandAsync($"AT+SMCONF=\"CLEANSS\",{(config.CleanSession ? 1 : 0)}");

            // 建立MQTT连接
            var connResp = await _channel.SendCommandAsync("AT+SMCONN", 30000);
            if (!connResp.IsOk)
                throw new AtCommandErrorException("AT+SMCONN", connResp.RawResponse);

            _isConnected = true;
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(true));
        }

        /// <summary>
        /// 断开MQTT连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            var resp = await _channel.SendCommandAsync("AT+SMDISC");
            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, "主动断开"));
        }

        /// <summary>
        /// 发布MQTT消息
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="payload">消息内容</param>
        /// <param name="qos">QoS等级（0, 1, 2）</param>
        /// <param name="retain">是否保留消息</param>
        public async Task PublishAsync(string topic, string payload, int qos = 0, bool retain = false)
        {
            if (!_isConnected)
                throw new ModemException("MQTT未连接，请先调用ConnectAsync");
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("主题不能为空", nameof(topic));
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            int retainFlag = retain ? 1 : 0;
            int payloadLen = payload.Length;

            // AT+SMPUB=<topic>,<content_length>,<qos>,<retain>
            var promptResp = await _channel.SendCommandAsync(
                $"AT+SMPUB=\"{topic}\",{payloadLen},{qos},{retainFlag}", 10000);

            if (promptResp.IsError)
                throw new AtCommandErrorException("AT+SMPUB", promptResp.RawResponse);

            // 发送消息内容（不含结尾换行符，长度已在命令中指定）
            _channel.SendRaw(payload);

            // 等待发送完成
            await Task.Delay(1000);
        }

        /// <summary>
        /// 订阅MQTT主题
        /// </summary>
        /// <param name="topic">订阅主题（支持通配符 +, #）</param>
        /// <param name="qos">QoS等级</param>
        public async Task SubscribeAsync(string topic, int qos = 0)
        {
            if (!_isConnected)
                throw new ModemException("MQTT未连接，请先调用ConnectAsync");
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("主题不能为空", nameof(topic));

            var resp = await _channel.SendCommandAsync($"AT+SMSUB=\"{topic}\",{qos}", 10000);
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+SMSUB", resp.RawResponse);
        }

        /// <summary>
        /// 取消订阅MQTT主题
        /// </summary>
        public async Task UnsubscribeAsync(string topic)
        {
            if (!_isConnected)
                throw new ModemException("MQTT未连接，请先调用ConnectAsync");

            var resp = await _channel.SendCommandAsync($"AT+SMUNSUB=\"{topic}\"", 10000);
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+SMUNSUB", resp.RawResponse);
        }

        private void OnUnsolicitedReceived(object? sender, string urc)
        {
            // MQTT消息接收: +SMSUB: "<topic>",<len>\r\n<payload>
            if (urc.StartsWith("+SMSUB:", StringComparison.OrdinalIgnoreCase))
            {
                string data = urc.Substring(7).Trim();
                // 格式: "<topic>",<len>
                int commaIdx = data.LastIndexOf(',');
                if (commaIdx > 0)
                {
                    string topic = data.Substring(0, commaIdx).Trim().Trim('"');
                    // payload在下一个URC中到达，或者拼接在同一行
                    // 简化处理：通知调用方
                    MessageReceived?.Invoke(this, new MqttMessageReceivedEventArgs(
                        new MqttMessage { Topic = topic, Payload = string.Empty }));
                }
            }
            // MQTT断开连接通知
            else if (urc.StartsWith("+SMSTATE:", StringComparison.OrdinalIgnoreCase))
            {
                string data = urc.Substring(9).Trim();
                if (data == "0")
                {
                    _isConnected = false;
                    ConnectionStateChanged?.Invoke(this,
                        new MqttConnectionStateChangedEventArgs(false, "网络断开"));
                }
                else if (data == "1")
                {
                    _isConnected = true;
                    ConnectionStateChanged?.Invoke(this,
                        new MqttConnectionStateChangedEventArgs(true));
                }
            }
        }
    }
}
