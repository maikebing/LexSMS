using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Events;
using LexSMS.Exceptions;

namespace LexSMS.Features
{
    /// <summary>
    /// TCP/IP 和 UDP 套接字客户端
    /// 实现 A76XX 模块的 TCP/UDP 网络通信功能（AT+NETOPEN/AT+CIPOPEN/AT+CIPSEND 指令集）
    /// </summary>
    public class TcpIpClient
    {
        private readonly AtChannel _channel;

        // 用于解析 UDP 接收来源（RECV FROM: 行先于 +IPD 行到达）
        private string? _pendingRecvFrom;

        /// <summary>
        /// 收到 TCP/UDP 数据事件
        /// </summary>
        public event EventHandler<TcpDataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// 连接关闭事件
        /// </summary>
        public event EventHandler<TcpConnectionClosedEventArgs>? ConnectionClosed;

        public TcpIpClient(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _channel.UnsolicitedReceived += OnUnsolicitedReceived;
        }

        /// <summary>
        /// 检查网络是否已打开（AT+NETOPEN?）
        /// </summary>
        /// <returns>true 表示网络已打开，false 表示未打开</returns>
        public async Task<bool> IsNetworkOpenAsync()
        {
            try
            {
                var resp = await _channel.SendCommandAsync("AT+NETOPEN?", 5000);
                if (!resp.IsOk)
                    return false;

                // 解析返回值，格式：+NETOPEN: <state>
                // state: 0=未打开, 1=已打开
                foreach (var line in resp.RawResponse.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("+NETOPEN:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmed.Substring(9).Trim().Split(',');
                        if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out int state))
                        {
                            return state == 1;
                        }
                    }
                }
                return false;
            }
            catch
            {
                // 查询失败时返回 false
                return false;
            }
        }

        /// <summary>
        /// 激活网络连接（AT+NETOPEN）
        /// 前提：GPRS 已附着（AT+CGATT? 返回 1）
        /// 建议先调用 IsNetworkOpenAsync() 检查网络状态，避免重复打开
        /// </summary>
        /// <returns>成功返回 true</returns>
        public async Task<bool> OpenNetworkAsync()
        {
            var resp = await _channel.SendCommandAsync("AT+NETOPEN", 30000);
            // 某些情况下网络已打开，返回 ERROR，也视为可继续
            return resp.IsOk;
        }

        /// <summary>
        /// 关闭网络连接（AT+NETCLOSE）
        /// </summary>
        public async Task<bool> CloseNetworkAsync()
        {
            var resp = await _channel.SendCommandAsync("AT+NETCLOSE", 15000);
            return resp.IsOk;
        }

        /// <summary>
        /// 建立 TCP 连接（AT+CIPOPEN）
        /// </summary>
        /// <param name="connectionIndex">连接索引（0-9）</param>
        /// <param name="remoteHost">远端服务器 IP 或域名</param>
        /// <param name="remotePort">远端端口</param>
        public async Task ConnectTcpAsync(int connectionIndex, string remoteHost, int remotePort)
        {
            if (string.IsNullOrWhiteSpace(remoteHost))
                throw new ArgumentException("服务器地址不能为空", nameof(remoteHost));

            var resp = await _channel.SendCommandAsync(
                $"AT+CIPOPEN={connectionIndex},\"TCP\",\"{remoteHost}\",{remotePort}", 30000);
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+CIPOPEN (TCP)", resp.RawResponse);
        }

        /// <summary>
        /// 打开 UDP 本地端口（AT+CIPOPEN）
        /// </summary>
        /// <param name="connectionIndex">连接索引（0-9）</param>
        /// <param name="localPort">本地 UDP 端口</param>
        public async Task OpenUdpAsync(int connectionIndex, int localPort)
        {
            var resp = await _channel.SendCommandAsync(
                $"AT+CIPOPEN={connectionIndex},\"UDP\",,,{localPort}", 30000);
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+CIPOPEN (UDP)", resp.RawResponse);
        }

        /// <summary>
        /// 发送 TCP 数据（AT+CIPSEND）
        /// </summary>
        /// <param name="connectionIndex">连接索引</param>
        /// <param name="data">要发送的数据</param>
        public async Task SendAsync(int connectionIndex, string data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            int dataLen = System.Text.Encoding.UTF8.GetByteCount(data);

            // 发送 AT+CIPSEND 并等待 > 提示符
            var promptResp = await _channel.SendCommandAsync(
                $"AT+CIPSEND={connectionIndex},{dataLen}", 10000);
            if (!promptResp.RawResponse.Contains(">"))
            {
                if (promptResp.IsError)
                    throw new AtCommandErrorException($"AT+CIPSEND={connectionIndex}", promptResp.RawResponse);
                throw new ModemException("未收到数据输入提示符");
            }

            // 发送数据并等待 SEND OK
            await SendDataAndWaitAsync(data);
        }

        /// <summary>
        /// 发送 UDP 数据（AT+CIPSEND，指定目标地址）
        /// </summary>
        /// <param name="connectionIndex">连接索引</param>
        /// <param name="remoteHost">目标 IP 或域名</param>
        /// <param name="remotePort">目标端口</param>
        /// <param name="data">要发送的数据</param>
        public async Task SendUdpAsync(int connectionIndex, string remoteHost, int remotePort, string data)
        {
            if (string.IsNullOrWhiteSpace(remoteHost))
                throw new ArgumentException("目标地址不能为空", nameof(remoteHost));
            if (data == null) throw new ArgumentNullException(nameof(data));

            int dataLen = System.Text.Encoding.UTF8.GetByteCount(data);

            // UDP 发送格式：AT+CIPSEND=<index>,<length>,"<remote_ip>",<remote_port>
            var promptResp = await _channel.SendCommandAsync(
                $"AT+CIPSEND={connectionIndex},{dataLen},\"{remoteHost}\",{remotePort}", 10000);
            if (!promptResp.RawResponse.Contains(">"))
            {
                if (promptResp.IsError)
                    throw new AtCommandErrorException($"AT+CIPSEND={connectionIndex} (UDP)", promptResp.RawResponse);
                throw new ModemException("未收到数据输入提示符");
            }

            await SendDataAndWaitAsync(data);
        }

        /// <summary>
        /// 关闭指定连接（AT+CIPCLOSE）
        /// </summary>
        /// <param name="connectionIndex">连接索引</param>
        public async Task<bool> CloseConnectionAsync(int connectionIndex)
        {
            var resp = await _channel.SendCommandAsync($"AT+CIPCLOSE={connectionIndex}", 10000);
            return resp.IsOk;
        }

        /// <summary>
        /// 发送数据并等待 SEND OK / SEND FAIL
        /// </summary>
        private async Task SendDataAndWaitAsync(string data)
        {
            var tcs = new TaskCompletionSource<bool>();
            var timeout = TimeSpan.FromSeconds(30);

            void handler(object? sender, string urc)
            {
                if (urc.Equals("SEND OK", StringComparison.OrdinalIgnoreCase))
                    tcs.TrySetResult(true);
                else if (urc.Equals("SEND FAIL", StringComparison.OrdinalIgnoreCase) ||
                         urc.StartsWith("+CME ERROR:", StringComparison.OrdinalIgnoreCase) ||
                         urc.StartsWith("+CMS ERROR:", StringComparison.OrdinalIgnoreCase))
                    tcs.TrySetResult(false);
            }

            _channel.UnsolicitedReceived += handler;

            try
            {
                _channel.SendRaw(data);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
                if (completed != tcs.Task)
                    throw new TimeoutException($"TCP 数据发送超时（{timeout.TotalSeconds}秒）");

                bool success = await tcs.Task;
                if (!success)
                    throw new ModemException("TCP 数据发送失败（SEND FAIL）");
            }
            finally
            {
                _channel.UnsolicitedReceived -= handler;
            }
        }

        private void OnUnsolicitedReceived(object? sender, string urc)
        {
            // UDP 接收来源行（出现在 +IPD 之前）
            // 格式：RECV FROM: <ip>:<port>
            if (urc.StartsWith("RECV FROM:", StringComparison.OrdinalIgnoreCase))
            {
                // "RECV FROM:" is 10 chars; only assign if there's content after it
                _pendingRecvFrom = urc.Length > 10 ? urc.Substring(10).Trim() : string.Empty;
                return;
            }

            // 接收到数据
            // 格式：+IPD<length>:<data>  或  +IPD<connect_index>,<length>:<data>
            if (urc.StartsWith("+IPD", StringComparison.OrdinalIgnoreCase))
            {
                int colonIdx = urc.IndexOf(':');
                // Must have ':' and at least one character between "+IPD" and ':'
                if (colonIdx < 5) return;

                string header = urc.Substring(4, colonIdx - 4); // between "+IPD" and ":"
                string data = urc.Length > colonIdx + 1 ? urc.Substring(colonIdx + 1) : string.Empty;

                int connectionIndex = 0;
                int dataLength = 0;

                // Two formats: "+IPD<len>:<data>" or "+IPD<idx>,<len>:<data>"
                int commaIdx = header.IndexOf(',');
                if (commaIdx >= 0)
                {
                    int.TryParse(header.Substring(0, commaIdx).Trim(), out connectionIndex);
                    int.TryParse(header.Substring(commaIdx + 1).Trim(), out dataLength);
                }
                else
                {
                    int.TryParse(header.Trim(), out dataLength);
                }

                // Parse remote address/port from RECV FROM if available
                string remoteAddress = string.Empty;
                int remotePort = 0;
                if (_pendingRecvFrom != null)
                {
                    // Format: <ip>:<port>
                    int lastColon = _pendingRecvFrom.LastIndexOf(':');
                    if (lastColon >= 0)
                    {
                        remoteAddress = _pendingRecvFrom.Substring(0, lastColon).Trim();
                        int.TryParse(_pendingRecvFrom.Substring(lastColon + 1).Trim(), out remotePort);
                    }
                    _pendingRecvFrom = null;
                }

                DataReceived?.Invoke(this,
                    new TcpDataReceivedEventArgs(connectionIndex, remoteAddress, remotePort, data, dataLength));
                return;
            }

            // 连接关闭通知
            // 格式：+CIPCLOSE: <connect_index>,<reason>
            // "+CIPCLOSE:" is 10 chars
            if (urc.StartsWith("+CIPCLOSE:", StringComparison.OrdinalIgnoreCase) && urc.Length > 10)
            {
                string rest = urc.Substring(10).Trim();
                string[] parts = rest.Split(',');
                int idx = int.TryParse(parts[0].Trim(), out int i) ? i : 0;
                int reason = parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int r) ? r : 0;
                ConnectionClosed?.Invoke(this, new TcpConnectionClosedEventArgs(idx, reason));
            }
        }
    }
}
