using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Events;
using LexSMS.Exceptions;
using LexSMS.Features;
using LexSMS.Models;

namespace LexSMS
{
    /// <summary>
    /// A76XX 4G模块封装库
    /// 提供拨打电话、来电显示、接听电话、收发短信、HTTP请求、MQTT连接、基站定位和TTS语音合成等功能
    /// </summary>
    public class A76XXModem : IDisposable
    {
        private readonly AtChannel _channel;
        private readonly CallManager _callManager;
        private readonly SmsManager _smsManager;
        private readonly ModemHttpClient _httpClient;
        private readonly ModemMqttClient _mqttClient;
        private readonly LocationManager _locationManager;
        private readonly StatusManager _statusManager;
        private readonly TtsManager _ttsManager;
        private readonly TcpIpClient _tcpIpClient;
        private bool _disposed;

        /// <summary>
        /// 日志输出委托
        /// </summary>
        public Action<string>? LogOutput { get; set; }

        /// <summary>
        /// 是否启用详细日志（包括调试信息）
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = true;

        #region 事件

        /// <summary>
        /// 来电事件
        /// </summary>
        public event EventHandler<IncomingCallEventArgs>? IncomingCall
        {
            add => _callManager.IncomingCall += value;
            remove => _callManager.IncomingCall -= value;
        }

        /// <summary>
        /// 通话状态变更事件
        /// </summary>
        public event EventHandler<CallStateChangedEventArgs>? CallStateChanged
        {
            add => _callManager.CallStateChanged += value;
            remove => _callManager.CallStateChanged -= value;
        }

        /// <summary>
        /// 收到新短信事件
        /// </summary>
        public event EventHandler<SmsReceivedEventArgs>? SmsReceived
        {
            add => _smsManager.SmsReceived += value;
            remove => _smsManager.SmsReceived -= value;
        }

        /// <summary>
        /// MQTT消息接收事件
        /// </summary>
        public event EventHandler<MqttMessageReceivedEventArgs>? MqttMessageReceived
        {
            add => _mqttClient.MessageReceived += value;
            remove => _mqttClient.MessageReceived -= value;
        }

        /// <summary>
        /// MQTT连接状态变更事件
        /// </summary>
        public event EventHandler<MqttConnectionStateChangedEventArgs>? MqttConnectionStateChanged
        {
            add => _mqttClient.ConnectionStateChanged += value;
            remove => _mqttClient.ConnectionStateChanged -= value;
        }

        /// <summary>
        /// TCP/UDP数据接收事件
        /// </summary>
        public event EventHandler<TcpDataReceivedEventArgs>? TcpDataReceived
        {
            add => _tcpIpClient.DataReceived += value;
            remove => _tcpIpClient.DataReceived -= value;
        }

        /// <summary>
        /// TCP连接关闭事件
        /// </summary>
        public event EventHandler<TcpConnectionClosedEventArgs>? TcpConnectionClosed
        {
            add => _tcpIpClient.ConnectionClosed += value;
            remove => _tcpIpClient.ConnectionClosed -= value;
        }

        /// <summary>
        /// 底层AT命令主动上报事件
        /// </summary>
        public event EventHandler<string>? UnsolicitedMessageReceived
        {
            add => _channel.UnsolicitedReceived += value;
            remove => _channel.UnsolicitedReceived -= value;
        }

        #endregion

        /// <summary>
        /// 创建A76XX模块实例
        /// </summary>
        /// <param name="config">串口配置</param>
        public A76XXModem(SerialPortConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            _channel = new AtChannel(config);
            _callManager = new CallManager(_channel);
            _smsManager = new SmsManager(_channel);
            _httpClient = new ModemHttpClient(_channel)
            {
                DebugLogOutput = LogDebug,
                WarningLogOutput = LogWarning
            };
            _mqttClient = new ModemMqttClient(_channel);
            _locationManager = new LocationManager(_channel);
            _statusManager = new StatusManager(_channel);
            _ttsManager = new TtsManager(_channel);
            _tcpIpClient = new TcpIpClient(_channel);
        }

        /// <summary>
        /// 使用串口名称和波特率创建A76XX模块实例
        /// </summary>
        /// <param name="portName">串口名称，例如 COM3 或 /dev/ttyUSB0</param>
        /// <param name="baudRate">波特率，默认 115200</param>
        public A76XXModem(string portName, int baudRate = 115200)
            : this(new SerialPortConfig { PortName = portName, BaudRate = baudRate })
        {
        }

        #region 连接管理

        /// <summary>
        /// 打开串口并初始化模块
        /// </summary>
        public async Task OpenAsync()
        {
            Log("正在打开串口连接...");
            _channel.Open();
            Log("串口连接成功，等待模块就绪...");
            await Task.Delay(500);

            // 关闭命令回显（ATE0），确保响应解析不受回显影响（doc §2.14）
            var ateResp = await _channel.SendCommandAsync("ATE0", 3000);
            if (!ateResp.IsOk)
                LogWarning("ATE0 失败，模块可能仍处于命令回显模式，这可能导致响应解析异常");

            // 测试模块响应
            LogDebug("正在测试模块响应...");
            var ping = await _statusManager.PingAsync();
            if (!ping)
            {
                LogWarning("模块未响应AT命令");
            }
            else
            {
                LogDebug("模块响应正常");
            }

            // 获取并输出模块信息
            try
            {
                LogDebug("正在获取模块信息...");
                var moduleInfo = await _statusManager.GetModuleInfoAsync();

                if (moduleInfo.ErrorMessage != null)
                {
                    LogError($"获取模块信息失败: {moduleInfo.ErrorMessage}");
                }
                else
                {
                    Log($"模块制造商: {moduleInfo.Manufacturer}");
                    Log($"模块型号: {moduleInfo.Model}");
                    Log($"固件版本: {moduleInfo.FirmwareVersion}");
                    Log($"IMEI: {moduleInfo.Imei}");
                }
            }
            catch (Exception ex)
            {
                LogError($"获取模块信息失败: {ex.Message}");
            }

            // 获取并输出SIM卡信息
            try
            {
                LogDebug("正在获取SIM卡信息...");
                var simInfo = await _statusManager.GetSimInfoAsync();

                if (simInfo.ErrorMessage != null)
                {
                    LogError($"获取SIM卡信息失败: {simInfo.ErrorMessage}");
                    Log($"SIM卡状态: {simInfo.Status}");
                }
                else
                {
                    Log($"SIM卡状态: {simInfo.Status}");
                    Log($"IMSI: {simInfo.Imsi}");
                    Log($"ICCID: {simInfo.Iccid}");
                    var phoneNumber = simInfo.PhoneNumber ?? "未知";
                    Log($"电话号码: {phoneNumber}");

                    if (simInfo.Status != SimStatus.Ready)
                    {
                        LogWarning($"SIM卡未就绪，当前状态: {simInfo.Status}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"获取SIM卡信息失败: {ex.Message}");
            }

            // 检查网络注册状态
            Log("正在检查网络注册状态...");
            NetworkInfo? networkInfo = null;
            int retryCount = 0;
            const int maxRetries = 20;
            string? lastErrorMessage = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    networkInfo = await _statusManager.GetNetworkInfoAsync();

                    if (networkInfo.ErrorMessage != null)
                    {
                        lastErrorMessage = networkInfo.ErrorMessage;
                        LogWarning($"获取网络信息失败: {networkInfo.ErrorMessage}");
                    }
                    else
                    {
                        lastErrorMessage = null;
                        LogDebug($"网络注册状态: {networkInfo.RegistrationStatus}");
                        var operatorName = networkInfo.OperatorName ?? "未知";
                        Log($"运营商: {operatorName}");
                        Log($"网络类型: {networkInfo.AccessType}");

                        if (networkInfo.IsRegistered)
                        {
                            Log("网络注册成功");
                            break;
                        }
                        else if (networkInfo.RegistrationStatus == NetworkRegistrationStatus.RegistrationDenied)
                        {
                            LogError("网络注册被拒绝");
                            throw new ModemException("网络注册被拒绝，请检查SIM卡和网络设置");
                        }
                        else if (networkInfo.RegistrationStatus == NetworkRegistrationStatus.Searching)
                        {
                            LogDebug($"正在搜索网络... ({retryCount + 1}/{maxRetries})");
                        }
                        else
                        {
                            LogDebug($"网络未注册，等待重试... ({retryCount + 1}/{maxRetries})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"获取网络信息失败: {ex.Message}");
                    lastErrorMessage = ex.Message;
                }

                retryCount++;
                await Task.Delay(1000);
            }

            if (networkInfo == null || !networkInfo.IsRegistered)
            {
                LogError("超过最大重试次数，网络注册失败");
                string errorDetail = lastErrorMessage != null 
                    ? $"无法注册到网络，请检查SIM卡和信号强度。详细错误: {lastErrorMessage}"
                    : "无法注册到网络，请检查SIM卡和信号强度";
                throw new ModemException(errorDetail);
            }

            // 获取并输出信号强度
            try
            {
                LogDebug("正在获取信号强度...");
                var signalInfo = await _statusManager.GetSignalInfoAsync();

                if (signalInfo.ErrorMessage != null)
                {
                    LogWarning($"获取信号强度失败: {signalInfo.ErrorMessage}");
                }
                else
                {
                    Log($"信号强度: {signalInfo.RssiDbm} dBm (RSSI: {signalInfo.Rssi}, 等级: {signalInfo.SignalLevel})");
                    LogDebug($"误码率: {signalInfo.Ber}");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"获取信号强度失败: {ex.Message}");
            }

            // 初始化基本设置
            LogDebug("正在初始化通话管理器...");
            await _callManager.InitializeAsync();
            LogDebug("通话管理器初始化完成");

            LogDebug("正在初始化短信管理器...");
            await _smsManager.InitializeAsync();
            LogDebug("短信管理器初始化完成");

            Log("模块初始化完成，准备就绪");
        }

        /// <summary>
        /// 输出日志
        /// </summary>
        private void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (LogOutput == null) return;

            var levelStr = level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                _ => "INFO "
            };

            // 如果未启用详细日志，跳过调试级别的消息
            if (!EnableVerboseLogging && level == LogLevel.Debug)
                return;

            LogOutput?.Invoke($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{levelStr}] {message}");
        }

        /// <summary>
        /// 输出调试日志
        /// </summary>
        private void LogDebug(string message) => Log(message, LogLevel.Debug);

        /// <summary>
        /// 输出警告日志
        /// </summary>
        private void LogWarning(string message) => Log(message, LogLevel.Warning);

        /// <summary>
        /// 输出错误日志
        /// </summary>
        private void LogError(string message) => Log(message, LogLevel.Error);

        /// <summary>
        /// 日志级别枚举
        /// </summary>
        private enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 关闭串口连接
        /// </summary>
        public void Close()
        {
            _channel.Close();
        }

        /// <summary>
        /// 串口是否已打开
        /// </summary>
        public bool IsOpen => _channel.IsOpen;

        #endregion

        #region 通话功能

        /// <summary>
        /// 拨打电话
        /// </summary>
        /// <param name="phoneNumber">目标电话号码（例如 +8613800138000 或 13800138000）</param>
        /// <returns>通话信息</returns>
        public Task<CallInfo> DialAsync(string phoneNumber)
            => _callManager.DialAsync(phoneNumber);

        /// <summary>
        /// 接听来电
        /// </summary>
        public Task AnswerCallAsync()
            => _callManager.AnswerAsync();

        /// <summary>
        /// 挂断电话
        /// </summary>
        public Task HangUpAsync()
            => _callManager.HangUpAsync();

        /// <summary>
        /// 拒接来电
        /// </summary>
        public Task RejectCallAsync()
            => _callManager.RejectAsync();

        /// <summary>
        /// 获取当前通话状态
        /// </summary>
        public Task<CallInfo> GetCurrentCallAsync()
            => _callManager.GetCurrentCallAsync();

        #endregion

        #region 短信功能

        /// <summary>
        /// 发送短信（自动识别中英文，支持Unicode字符）
        /// </summary>
        /// <param name="phoneNumber">目标手机号码</param>
        /// <param name="message">短信内容（支持中文）</param>
        public Task SendSmsAsync(string phoneNumber, string message)
            => _smsManager.SendSmsAsync(phoneNumber, message);

        /// <summary>
        /// 读取指定索引的短信
        /// </summary>
        /// <param name="index">短信索引</param>
        public Task<SmsMessage?> ReadSmsAsync(int index)
            => _smsManager.ReadSmsAsync(index);

        /// <summary>
        /// 列出所有短信
        /// </summary>
        /// <param name="status">短信状态过滤</param>
        public Task<System.Collections.Generic.List<SmsMessage>> ListSmsAsync(SmsStatus status = SmsStatus.All)
            => _smsManager.ListSmsAsync(status);

        /// <summary>
        /// 删除指定短信
        /// </summary>
        public Task DeleteSmsAsync(int index)
            => _smsManager.DeleteSmsAsync(index);

        /// <summary>
        /// 删除所有短信
        /// </summary>
        public Task DeleteAllSmsAsync()
            => _smsManager.DeleteAllSmsAsync();

        #endregion

        #region HTTP功能

        /// <summary>
        /// 通过模块发起HTTP GET请求
        /// </summary>
        /// <param name="url">请求URL（支持HTTP/HTTPS）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>HTTP响应</returns>
        public Task<HttpResponse> HttpGetAsync(string url, CancellationToken cancellationToken = default)
            => _httpClient.GetAsync(url, cancellationToken);

        /// <summary>
        /// 通过模块发起HTTP POST请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="body">请求体内容</param>
        /// <param name="contentType">Content-Type，默认 application/json</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>HTTP响应</returns>
        public Task<HttpResponse> HttpPostAsync(string url, string body, string contentType = "application/json", CancellationToken cancellationToken = default)
            => _httpClient.PostAsync(url, body, contentType, cancellationToken);

        /// <summary>
        /// 读取HTTP响应头（使用AT+HTTPHEAD）
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>HTTP响应（Headers属性包含响应头内容）</returns>
        public Task<HttpResponse> HttpGetHeadersAsync(string url, CancellationToken cancellationToken = default)
            => _httpClient.GetHeadersAsync(url, cancellationToken);

        /// <summary>
        /// 通过模块发起HTTP请求（完整版本）
        /// </summary>
        public Task<HttpResponse> HttpRequestAsync(string url, Models.HttpMethod method = Models.HttpMethod.GET,
            string? body = null, string contentType = "application/json", CancellationToken cancellationToken = default)
            => _httpClient.RequestAsync(url, method, body, contentType, cancellationToken);

        /// <summary>
        /// 通过模块下载HTTP文件到本地磁盘
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="localFilePath">本地磁盘文件路径</param>
        /// <param name="readChunkSize">单次读取块大小，默认 1024 字节</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<HttpResponse> HttpDownloadFileAsync(string url, string localFilePath, int readChunkSize = 1024, CancellationToken cancellationToken = default)
            => _httpClient.DownloadFileAsync(url, localFilePath, readChunkSize, cancellationToken);

        /// <summary>
        /// 通过模块下载HTTP内容到可写入流（支持大文件）
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="destination">目标流（由调用方负责释放）</param>
        /// <param name="readChunkSize">单次读取块大小，默认 1024 字节</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<HttpResponse> HttpDownloadToStreamAsync(string url, Stream destination, int readChunkSize = 1024, CancellationToken cancellationToken = default)
            => _httpClient.DownloadToStreamAsync(url, destination, readChunkSize, cancellationToken);

        /// <summary>
        /// 通过模块下载HTTP内容到内存流（Position 已重置到 0）
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="readChunkSize">单次读取块大小，默认 1024 字节</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<MemoryStream> HttpDownloadToMemoryStreamAsync(string url, int readChunkSize = 1024, CancellationToken cancellationToken = default)
            => _httpClient.DownloadToMemoryStreamAsync(url, readChunkSize, cancellationToken);

        /// <summary>
        /// 通过模块下载HTTP内容到字节数组缓冲区
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="readChunkSize">单次读取块大小，默认 1024 字节</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<byte[]> HttpDownloadToBufferAsync(string url, int readChunkSize = 1024, CancellationToken cancellationToken = default)
            => _httpClient.DownloadToBufferAsync(url, readChunkSize, cancellationToken);

        #endregion

        #region MQTT功能

        /// <summary>
        /// 连接MQTT Broker
        /// </summary>
        /// <param name="config">MQTT连接配置</param>
        public Task MqttConnectAsync(MqttConfig config)
            => _mqttClient.ConnectAsync(config);

        /// <summary>
        /// 断开MQTT连接
        /// </summary>
        public Task MqttDisconnectAsync()
            => _mqttClient.DisconnectAsync();

        /// <summary>
        /// 发布MQTT消息
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="payload">消息内容</param>
        /// <param name="qos">QoS等级（0, 1, 2）</param>
        /// <param name="retain">是否保留消息</param>
        public Task MqttPublishAsync(string topic, string payload, int qos = 0, bool retain = false)
            => _mqttClient.PublishAsync(topic, payload, qos, retain);

        /// <summary>
        /// 订阅MQTT主题
        /// </summary>
        /// <param name="topic">主题（支持 + 和 # 通配符）</param>
        /// <param name="qos">QoS等级</param>
        public Task MqttSubscribeAsync(string topic, int qos = 0)
            => _mqttClient.SubscribeAsync(topic, qos);

        /// <summary>
        /// 取消订阅MQTT主题
        /// </summary>
        public Task MqttUnsubscribeAsync(string topic)
            => _mqttClient.UnsubscribeAsync(topic);

        /// <summary>
        /// MQTT是否已连接
        /// </summary>
        public bool IsMqttConnected => _mqttClient.IsConnected;

        #endregion

        #region 定位功能

        /// <summary>
        /// 获取基站定位信息（通过运营商网络定位）
        /// </summary>
        /// <returns>定位结果，包含经纬度和精度</returns>
        public Task<CellLocation> GetCellLocationAsync()
            => _locationManager.GetCellLocationAsync();

        /// <summary>
        /// 获取 GPS 定位信息（AT+CGPSINFO）
        /// </summary>
        /// <returns>GPS 定位结果，包含经纬度、UTC 时间和海拔</returns>
        public Task<GpsLocation> GetGpsLocationAsync()
            => _locationManager.GetGpsLocationAsync();

        /// <summary>
        /// 获取基站定位地址信息（AT+CLBS=2）
        /// </summary>
        /// <returns>定位地址结果</returns>
        public Task<CellLocation> GetCellAddressAsync()
            => _locationManager.GetCellAddressAsync();

        /// <summary>
        /// 获取完整的基站定位信息（合并经纬度、地址等）
        /// 依次执行 AT+CLBS=1（经纬度）、AT+CLBS=2（地址）、AT+CLBS=4（扩展信息）
        /// </summary>
        /// <param name="includeAddress">是否包含地址查询（AT+CLBS=2），默认 true</param>
        /// <param name="includeExtended">是否包含扩展查询（AT+CLBS=4），默认 false</param>
        /// <returns>包含所有查询信息的定位结果</returns>
        public Task<CellLocation> GetCompleteCellLocationAsync(bool includeAddress = true, bool includeExtended = false)
            => _locationManager.GetCompleteCellLocationAsync(includeAddress, includeExtended);

        /// <summary>
        /// 获取当前基站信息（MCC/MNC/LAC/CellID）
        /// </summary>
        public Task<CellLocation> GetCellInfoAsync()
            => _locationManager.GetCellInfoAsync();

        #endregion

        #region 状态查询

        /// <summary>
        /// 获取模块信息（制造商、型号、固件版本、IMEI）
        /// </summary>
        public Task<ModuleInfo> GetModuleInfoAsync()
            => _statusManager.GetModuleInfoAsync();

        /// <summary>
        /// 获取SIM卡状态信息（IMSI、ICCID、电话号码等）
        /// </summary>
        public Task<SimInfo> GetSimInfoAsync()
            => _statusManager.GetSimInfoAsync();

        /// <summary>
        /// 获取信号强度信息
        /// </summary>
        public Task<SignalInfo> GetSignalInfoAsync()
            => _statusManager.GetSignalInfoAsync();

        /// <summary>
        /// 获取网络注册状态和运营商信息
        /// </summary>
        public Task<NetworkInfo> GetNetworkInfoAsync()
            => _statusManager.GetNetworkInfoAsync();

        /// <summary>
        /// 测试模块是否响应（发送AT命令）
        /// </summary>
        public Task<bool> PingAsync()
            => _statusManager.PingAsync();

        /// <summary>
        /// 查询 GPRS/PS 域附着状态
        /// </summary>
        public Task<GprsAttachStatus> GetGprsAttachStatusAsync()
            => _statusManager.GetGprsAttachStatusAsync();

        /// <summary>
        /// 设置 GPRS/PS 域附着状态
        /// </summary>
        /// <param name="attach">true=附着，false=分离</param>
        public Task<bool> SetGprsAttachAsync(bool attach)
            => _statusManager.SetGprsAttachAsync(attach);

        /// <summary>
        /// 查询模块分配的 IP 地址（AT+CGPADDR）
        /// </summary>
        /// <returns>IP 地址字符串，查询失败返回 null</returns>
        public Task<string?> GetIpAddressAsync()
            => _statusManager.GetIpAddressAsync();

        /// <summary>
        /// 查询模块当前时间（AT+CCLK?）
        /// </summary>
        /// <returns>模块时钟时间，解析失败返回 null</returns>
        public Task<DateTimeOffset?> GetModuleClockAsync()
            => _statusManager.GetModuleClockAsync();

        /// <summary>
        /// 执行 NTP 网络时间同步（AT+CNTP）
        /// </summary>
        /// <returns>同步结果，包含状态码和错误信息</returns>
        public Task<NtpSyncResult> SyncNetworkTimeAsync()
            => _statusManager.SyncNetworkTimeAsync();

        /// <summary>
        /// 重置模块
        /// </summary>
        public Task ResetAsync()
            => _statusManager.ResetAsync();

        /// <summary>
        /// 发送原始 AT 命令（用于调试和高级功能）
        /// </summary>
        /// <param name="command">AT 命令</param>
        /// <param name="timeoutMs">超时时间（毫秒），0 使用默认值</param>
        public Task<AtResponse> SendRawCommandAsync(string command, int timeoutMs = 0)
            => _channel.SendCommandAsync(command, timeoutMs);

        #endregion

        #region TTS语音合成功能

        /// <summary>
        /// 查询模块是否支持TTS功能
        /// </summary>
        public Task<bool> IsTtsSupportedAsync()
            => _ttsManager.IsSupportedAsync();

        /// <summary>
        /// 播放文字（自动判断编码：含中文使用UCS2，纯ASCII使用混合编码）
        /// </summary>
        /// <param name="text">要朗读的文字（支持中英文混合）</param>
        public Task TtsSpeakAsync(string text)
            => _ttsManager.SpeakAsync(text);

        /// <summary>
        /// 使用UCS2编码播放文字（支持所有Unicode字符，包括中文）
        /// </summary>
        /// <param name="text">要朗读的文字</param>
        public Task TtsSpeakUcs2Async(string text)
            => _ttsManager.SpeakUcs2Async(text);

        /// <summary>
        /// 使用混合编码播放文字（ASCII + GBK，适合英文与中文混合）
        /// </summary>
        /// <param name="text">要朗读的文字</param>
        public Task TtsSpeakMixedAsync(string text)
            => _ttsManager.SpeakMixedAsync(text);

        /// <summary>
        /// 停止TTS播放（AT+CTTS=0）
        /// </summary>
        public Task TtsStopAsync()
            => _ttsManager.StopAsync();

        #endregion

        #region TCP/IP 和 UDP 功能

        /// <summary>
        /// 检查网络是否已打开（AT+NETOPEN?）
        /// </summary>
        /// <returns>true 表示网络已打开，false 表示未打开</returns>
        public Task<bool> TcpIsNetworkOpenAsync()
            => _tcpIpClient.IsNetworkOpenAsync();

        /// <summary>
        /// 激活网络连接（AT+NETOPEN）
        /// 前提：GPRS 已附着（GetGprsAttachStatusAsync 返回 Attached）
        /// 建议先调用 TcpIsNetworkOpenAsync() 检查网络状态，避免重复打开
        /// </summary>
        public Task<bool> TcpOpenNetworkAsync()
            => _tcpIpClient.OpenNetworkAsync();

        /// <summary>
        /// 关闭网络连接（AT+NETCLOSE）
        /// </summary>
        public Task<bool> TcpCloseNetworkAsync()
            => _tcpIpClient.CloseNetworkAsync();

        /// <summary>
        /// 建立 TCP 连接（AT+CIPOPEN）
        /// </summary>
        /// <param name="connectionIndex">连接索引（0-9）</param>
        /// <param name="remoteHost">远端服务器 IP 或域名</param>
        /// <param name="remotePort">远端端口</param>
        public Task TcpConnectAsync(int connectionIndex, string remoteHost, int remotePort)
            => _tcpIpClient.ConnectTcpAsync(connectionIndex, remoteHost, remotePort);

        /// <summary>
        /// 打开 UDP 本地端口（AT+CIPOPEN）
        /// </summary>
        /// <param name="connectionIndex">连接索引（0-9）</param>
        /// <param name="localPort">本地 UDP 端口</param>
        public Task TcpOpenUdpAsync(int connectionIndex, int localPort)
            => _tcpIpClient.OpenUdpAsync(connectionIndex, localPort);

        /// <summary>
        /// 发送 TCP 数据（AT+CIPSEND）
        /// </summary>
        /// <param name="connectionIndex">连接索引</param>
        /// <param name="data">要发送的数据</param>
        public Task TcpSendAsync(int connectionIndex, string data)
            => _tcpIpClient.SendAsync(connectionIndex, data);

        /// <summary>
        /// 发送 UDP 数据（AT+CIPSEND，指定目标地址）
        /// </summary>
        /// <param name="connectionIndex">连接索引</param>
        /// <param name="remoteHost">目标 IP 或域名</param>
        /// <param name="remotePort">目标端口</param>
        /// <param name="data">要发送的数据</param>
        public Task TcpSendUdpAsync(int connectionIndex, string remoteHost, int remotePort, string data)
            => _tcpIpClient.SendUdpAsync(connectionIndex, remoteHost, remotePort, data);

        /// <summary>
        /// 关闭指定 TCP/UDP 连接（AT+CIPCLOSE）
        /// </summary>
        /// <param name="connectionIndex">连接索引</param>
        public Task<bool> TcpCloseConnectionAsync(int connectionIndex)
            => _tcpIpClient.CloseConnectionAsync(connectionIndex);

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _channel.Dispose();
            }
        }
    }
}
