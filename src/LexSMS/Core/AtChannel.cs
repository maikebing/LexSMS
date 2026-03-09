using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LexSMS.Exceptions;

namespace LexSMS.Core
{
    /// <summary>
    /// AT命令通信信道，负责与A76XX模块的底层串口通信
    /// </summary>
    public class AtChannel : IDisposable
    {
        private readonly SerialPort _serialPort;
        private readonly SerialPortConfig _config;
        private readonly SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private readonly ConcurrentQueue<string> _unsolicitedQueue = new ConcurrentQueue<string>();
        private TaskCompletionSource<AtResponse>? _pendingResponse;
        private readonly List<string> _currentResponseLines = new List<string>();
        private bool _disposed;

        /// <summary>
        /// 主动上报消息事件（URC: Unsolicited Result Code）
        /// </summary>
        public event EventHandler<string>? UnsolicitedReceived;

        public AtChannel(SerialPortConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _serialPort = new SerialPort(config.PortName, config.BaudRate, config.Parity, config.DataBits, config.StopBits)
            {
                ReadTimeout = config.ReadTimeoutMs,
                WriteTimeout = 3000,
                Encoding = Encoding.ASCII,
                NewLine = "\r\n",
                DtrEnable = true,
                RtsEnable = true
            };
            _serialPort.DataReceived += OnDataReceived;
        }

        /// <summary>
        /// 打开串口连接
        /// </summary>
        public void Open()
        {
            if (_serialPort.IsOpen) return;
            _serialPort.Open();
        }

        /// <summary>
        /// 关闭串口连接
        /// </summary>
        public void Close()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        /// <summary>
        /// 串口是否已打开
        /// </summary>
        public bool IsOpen => _serialPort.IsOpen;

        /// <summary>
        /// 发送AT命令并等待响应
        /// </summary>
        /// <param name="command">AT命令</param>
        /// <param name="timeoutMs">超时毫秒数，0 表示使用配置默认值</param>
        public async Task<AtResponse> SendCommandAsync(string command, int timeoutMs = 0)
        {
            if (!_serialPort.IsOpen)
                throw new ModemException("串口未打开");

            int timeout = timeoutMs > 0 ? timeoutMs : _config.CommandTimeoutMs;

            await _commandLock.WaitAsync();
            try
            {
                _currentResponseLines.Clear();
                _pendingResponse = new TaskCompletionSource<AtResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

                string cmdToSend = command.EndsWith("\r") ? command : command + "\r";
                byte[] bytes = Encoding.ASCII.GetBytes(cmdToSend);
                _serialPort.Write(bytes, 0, bytes.Length);

                using var cts = new CancellationTokenSource(timeout);
                cts.Token.Register(() =>
                {
                    _pendingResponse?.TrySetException(new AtCommandTimeoutException(command));
                });

                return await _pendingResponse.Task;
            }
            finally
            {
                _pendingResponse = null;
                _commandLock.Release();
            }
        }

        /// <summary>
        /// 发送原始数据（用于PDU短信等需要原始字节的场景）
        /// </summary>
        public void SendRaw(string data)
        {
            if (!_serialPort.IsOpen)
                throw new ModemException("串口未打开");
            byte[] bytes = Encoding.ASCII.GetBytes(data);
            _serialPort.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// 发送Ctrl+Z（用于短信发送确认）
        /// </summary>
        public void SendCtrlZ()
        {
            if (!_serialPort.IsOpen)
                throw new ModemException("串口未打开");
            _serialPort.Write(new byte[] { 0x1A }, 0, 1);
        }

        /// <summary>
        /// 发送ESC（用于取消短信发送）
        /// </summary>
        public void SendEsc()
        {
            if (!_serialPort.IsOpen)
                throw new ModemException("串口未打开");
            _serialPort.Write(new byte[] { 0x1B }, 0, 1);
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadExisting();
                _receiveBuffer.Append(data);
                ProcessBuffer();
            }
            catch (Exception)
            {
                // 串口读取异常，忽略
            }
        }

        private void ProcessBuffer()
        {
            string buffer = _receiveBuffer.ToString();

            while (true)
            {
                int crlfIdx = buffer.IndexOf("\r\n", StringComparison.Ordinal);
                if (crlfIdx < 0) break;

                string line = buffer.Substring(0, crlfIdx).Trim();
                buffer = buffer.Substring(crlfIdx + 2);

                if (string.IsNullOrEmpty(line)) continue;

                ProcessLine(line);
            }

            _receiveBuffer.Clear();
            _receiveBuffer.Append(buffer);
        }

        private void ProcessLine(string line)
        {
            // 判断是否为终止符
            bool isTerminator = IsResponseTerminator(line);

            if (_pendingResponse != null)
            {
                _currentResponseLines.Add(line);

                if (isTerminator)
                {
                    var response = BuildResponse(_currentResponseLines);
                    _pendingResponse.TrySetResult(response);
                }
                // 检测需要输入数据的提示符 (> 提示符用于短信)
                else if (line.TrimEnd() == ">")
                {
                    var response = new AtResponse
                    {
                        IsOk = false,
                        IsError = false,
                        Lines = new List<string>(_currentResponseLines),
                        RawResponse = string.Join("\r\n", _currentResponseLines)
                    };
                    _pendingResponse.TrySetResult(response);
                }
            }
            else
            {
                // 主动上报消息
                _unsolicitedQueue.Enqueue(line);
                UnsolicitedReceived?.Invoke(this, line);
            }
        }

        private static bool IsResponseTerminator(string line)
        {
            return line == "OK" ||
                   line == "ERROR" ||
                   line.StartsWith("+CME ERROR:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("+CMS ERROR:", StringComparison.OrdinalIgnoreCase) ||
                   line == "NO CARRIER" ||
                   line == "BUSY" ||
                   line == "NO ANSWER" ||
                   line == "NO DIALTONE" ||
                   line == "CONNECT" ||
                   line.StartsWith("CONNECT ", StringComparison.OrdinalIgnoreCase);
        }

        private static AtResponse BuildResponse(List<string> lines)
        {
            var response = new AtResponse
            {
                Lines = new List<string>(lines),
                RawResponse = string.Join("\r\n", lines)
            };

            foreach (var line in lines)
            {
                if (line == "OK")
                {
                    response.IsOk = true;
                    break;
                }
                if (line == "ERROR" ||
                    line.StartsWith("+CME ERROR:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("+CMS ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    response.IsError = true;
                    break;
                }
            }

            return response;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _serialPort.DataReceived -= OnDataReceived;
                Close();
                _serialPort.Dispose();
                _commandLock.Dispose();
            }
        }
    }
}
