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
    /// 使用 AT+CMQTT* 指令集（A76XX 原生MQTT协议栈）
    /// </summary>
    public class ModemMqttClient
    {
        private readonly AtChannel _channel;
        private bool _isConnected;
        // A76XX支持多个客户端，默认使用索引0
        private const int ClientIndex = 0;

        // 用于组装多行 +CMQTTRX* 消息
        private string? _rxTopic;
        private System.Text.StringBuilder? _rxPayload;
        private bool _rxReadingTopic;
        private bool _rxReadingPayload;

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

            // 1. 启动 MQTT 协议栈: AT+CMQTTSTART
            var startResp = await _channel.SendCommandAsync("AT+CMQTTSTART", 10000);
            // 如果已经启动，模块可能返回 ERROR，忽略此错误继续

            // 2. 分配客户端 ID: AT+CMQTTACCQ=<index>,"<clientid>",<qos>
            string clientId = string.IsNullOrEmpty(config.ClientId) ? "A76XX_Client" : config.ClientId;
            var accqResp = await _channel.SendCommandAsync(
                $"AT+CMQTTACCQ={ClientIndex},\"{clientId}\",0", 10000);
            if (!accqResp.IsOk)
                throw new AtCommandErrorException("AT+CMQTTACCQ", accqResp.RawResponse);

            // 3. 连接到 MQTT 服务器: AT+CMQTTCONNECT=<index>,"tcp://<host>:<port>",<keepalive>,<clean_session>
            string protocol = config.UseSsl ? "ssl" : "tcp";
            string url = $"{protocol}://{config.BrokerAddress}:{config.Port}";
            int cleanSession = config.CleanSession ? 1 : 0;

            string connCmd = $"AT+CMQTTCONNECT={ClientIndex},\"{url}\",{config.KeepAliveSeconds},{cleanSession}";
            // 如果有用户名密码，附加到命令
            if (!string.IsNullOrEmpty(config.Username))
            {
                string pwd = config.Password ?? string.Empty;
                connCmd += $",\"{config.Username}\",\"{pwd}\"";
            }

            var connResp = await _channel.SendCommandAsync(connCmd, 30000);
            if (!connResp.IsOk)
                throw new AtCommandErrorException("AT+CMQTTCONNECT", connResp.RawResponse);

            // 等待连接确认 URC: +CMQTTCONNECT: <index>,<result>
            // result=0 表示成功
            var connected = await WaitForUrcAsync("+CMQTTCONNECT:", 30000);
            if (connected != null)
            {
                // +CMQTTCONNECT: 0,0 — 第二个参数为错误码，0=成功
                string data = connected.Substring(connected.IndexOf(':') + 1).Trim();
                string[] parts = data.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int errCode) && errCode != 0)
                    throw new ModemException($"MQTT连接失败，错误码: {errCode}");
            }

            _isConnected = true;
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(true));
        }

        /// <summary>
        /// 断开MQTT连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            // AT+CMQTTDISC=<index>,<timeout>
            await _channel.SendCommandAsync($"AT+CMQTTDISC={ClientIndex},120", 10000);
            // 释放客户端资源: AT+CMQTTREL=<index>
            await _channel.SendCommandAsync($"AT+CMQTTREL={ClientIndex}", 5000);
            // 停止 MQTT 协议栈: AT+CMQTTSTOP
            await _channel.SendCommandAsync("AT+CMQTTSTOP", 5000);

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

            // 1. 设置发布主题: AT+CMQTTTOPIC=<index>,<topic_length>
            var topicResp = await _channel.SendCommandAsync(
                $"AT+CMQTTTOPIC={ClientIndex},{topic.Length}", 5000);
            if (!topicResp.RawResponse.Contains(">"))
                throw new ModemException("未收到主题输入提示符");

            _channel.SendRaw(topic);
            await Task.Delay(100);

            // 2. 设置发布内容: AT+CMQTTPAYLOAD=<index>,<payload_length>
            int payloadLen = System.Text.Encoding.UTF8.GetByteCount(payload);
            var payloadResp = await _channel.SendCommandAsync(
                $"AT+CMQTTPAYLOAD={ClientIndex},{payloadLen}", 5000);
            if (!payloadResp.RawResponse.Contains(">"))
                throw new ModemException("未收到消息内容输入提示符");

            _channel.SendRaw(payload);
            await Task.Delay(100);

            // 3. 执行发布: AT+CMQTTPUB=<index>,<qos>,<timeout>
            int retainFlag = retain ? 1 : 0;
            var pubResp = await _channel.SendCommandAsync(
                $"AT+CMQTTPUB={ClientIndex},{qos},60,{retainFlag}", 30000);
            if (!pubResp.IsOk)
                throw new AtCommandErrorException("AT+CMQTTPUB", pubResp.RawResponse);

            // 等待发布完成 URC: +CMQTTPUB: <index>,<result>
            await WaitForUrcAsync("+CMQTTPUB:", 30000);
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

            // 1. 设置订阅主题: AT+CMQTTSUBTOPIC=<index>,<topic_length>,<qos>
            var subTopicResp = await _channel.SendCommandAsync(
                $"AT+CMQTTSUBTOPIC={ClientIndex},{topic.Length},{qos}", 5000);
            if (!subTopicResp.RawResponse.Contains(">"))
                throw new ModemException("未收到订阅主题输入提示符");

            _channel.SendRaw(topic);
            await Task.Delay(100);

            // 2. 执行订阅: AT+CMQTTSUB=<index>
            var subResp = await _channel.SendCommandAsync($"AT+CMQTTSUB={ClientIndex}", 10000);
            if (!subResp.IsOk)
                throw new AtCommandErrorException("AT+CMQTTSUB", subResp.RawResponse);
        }

        /// <summary>
        /// 取消订阅MQTT主题
        /// </summary>
        public async Task UnsubscribeAsync(string topic)
        {
            if (!_isConnected)
                throw new ModemException("MQTT未连接，请先调用ConnectAsync");

            // 1. 设置取消订阅主题长度: AT+CMQTTSUBTOPIC=<index>,<topic_length>,<qos>
            var subTopicResp = await _channel.SendCommandAsync(
                $"AT+CMQTTSUBTOPIC={ClientIndex},{topic.Length},0", 5000);
            if (!subTopicResp.RawResponse.Contains(">"))
                throw new ModemException("未收到取消订阅主题输入提示符");

            _channel.SendRaw(topic);
            await Task.Delay(100);

            // 2. 执行取消订阅: AT+CMQTTUNSUB=<index>
            var unsubResp = await _channel.SendCommandAsync($"AT+CMQTTUNSUB={ClientIndex}", 10000);
            if (!unsubResp.IsOk)
                throw new AtCommandErrorException("AT+CMQTTUNSUB", unsubResp.RawResponse);
        }

        /// <summary>
        /// 等待指定前缀的 URC 消息
        /// </summary>
        private Task<string?> WaitForUrcAsync(string prefix, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<string?>();
            var cts = new CancellationTokenSource(timeoutMs);

            void handler(object? sender, string urc)
            {
                if (urc.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    tcs.TrySetResult(urc);
            }

            _channel.UnsolicitedReceived += handler;
            cts.Token.Register(() => tcs.TrySetResult(null));

            return tcs.Task.ContinueWith(t =>
            {
                _channel.UnsolicitedReceived -= handler;
                cts.Dispose();
                return t.Result;
            });
        }

        private void OnUnsolicitedReceived(object? sender, string urc)
        {
            // MQTT 消息接收流程 (多行URC):
            // +CMQTTRXSTART: 0,<topic_len>,<payload_len>
            // +CMQTTRXTOPIC: 0,<topic_len>
            // <topic>
            // +CMQTTRXPAYLOAD: 0,<payload_len>
            // <payload>
            // +CMQTTRXEND: 0

            if (urc.StartsWith("+CMQTTRXSTART:", StringComparison.OrdinalIgnoreCase))
            {
                // 新消息开始，重置缓冲
                _rxTopic = null;
                _rxPayload = new System.Text.StringBuilder();
                _rxReadingTopic = false;
                _rxReadingPayload = false;
            }
            else if (urc.StartsWith("+CMQTTRXTOPIC:", StringComparison.OrdinalIgnoreCase))
            {
                // 之后的行是主题内容，直到下一个 URC 标记
                _rxReadingTopic = true;
                _rxReadingPayload = false;
                _rxTopic = null;
            }
            else if (urc.StartsWith("+CMQTTRXPAYLOAD:", StringComparison.OrdinalIgnoreCase))
            {
                // 之后的行是消息内容，直到下一个 URC 标记
                _rxReadingTopic = false;
                _rxReadingPayload = true;
                _rxPayload?.Clear();
            }
            else if (urc.StartsWith("+CMQTTRXEND:", StringComparison.OrdinalIgnoreCase))
            {
                // 消息接收完成，触发事件
                _rxReadingTopic = false;
                _rxReadingPayload = false;
                if (_rxTopic != null)
                {
                    var msg = new MqttMessage
                    {
                        Topic = _rxTopic,
                        Payload = _rxPayload?.ToString() ?? string.Empty
                    };
                    MessageReceived?.Invoke(this, new MqttMessageReceivedEventArgs(msg));
                }
                _rxTopic = null;
                _rxPayload = null;
            }
            else if (_rxReadingTopic)
            {
                // 累积主题内容（主题通常是单行，但保持健壮性）
                if (_rxTopic == null)
                    _rxTopic = urc;
                else
                    _rxTopic += urc;
            }
            else if (_rxReadingPayload && _rxPayload != null)
            {
                // 累积消息内容（消息可能跨越多行，直接拼接不加换行符以保持原始数据）
                _rxPayload.Append(urc);
            }
            // MQTT 连接丢失: +CMQTTCONNLOST: <index>,<reason>
            else if (urc.StartsWith("+CMQTTCONNLOST:", StringComparison.OrdinalIgnoreCase))
            {
                _isConnected = false;
                ConnectionStateChanged?.Invoke(this,
                    new MqttConnectionStateChangedEventArgs(false, "连接丢失"));
            }
            // MQTT 连接断开: +CMQTTNOCONN: <index>
            else if (urc.StartsWith("+CMQTTNOCONN:", StringComparison.OrdinalIgnoreCase))
            {
                _isConnected = false;
                ConnectionStateChanged?.Invoke(this,
                    new MqttConnectionStateChangedEventArgs(false, "连接断开"));
            }
        }
    }
}
