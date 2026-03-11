using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Exceptions;
using LexSMS.Models;
using HttpMethod = LexSMS.Models.HttpMethod;

namespace LexSMS.Features
{
    /// <summary>
    /// HTTP客户端
    /// 通过A76XX模块发起HTTP/HTTPS请求
    /// </summary>
    public class ModemHttpClient
    {
        private readonly AtChannel _channel;

        /// <summary>调试日志输出委托</summary>
        internal Action<string>? DebugLogOutput { get; set; }

        /// <summary>警告日志输出委托</summary>
        internal Action<string>? WarningLogOutput { get; set; }

        public ModemHttpClient(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>
        /// 发起HTTP GET请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return await RequestAsync(url, HttpMethod.GET, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 发起HTTP POST请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="body">请求体</param>
        /// <param name="contentType">Content-Type</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<HttpResponse> PostAsync(string url, string body, string contentType = "application/json", CancellationToken cancellationToken = default)
        {
            return await RequestAsync(url, HttpMethod.POST, body, contentType, cancellationToken);
        }

        /// <summary>
        /// 读取HTTP响应头（AT+HTTPHEAD）
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<HttpResponse> GetHeadersAsync(string url, CancellationToken cancellationToken = default)
        {
            return await RequestAsync(url, HttpMethod.HEAD, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 将HTTP响应内容下载到本地文件（逐块 AT+HTTPREAD 模式）
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="localFilePath">本地磁盘文件路径</param>
        /// <param name="readChunkSize">单次读取块大小，默认 1024 字节</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<HttpResponse> DownloadFileAsync(string url, string localFilePath, int readChunkSize = 1024, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(localFilePath))
                throw new ArgumentException("本地文件路径不能为空", nameof(localFilePath));

            string fullPath = Path.GetFullPath(localFilePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            LogDebug($"开始下载文件到本地磁盘: {fullPath}");

            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            return await DownloadToStreamAsync(url, fileStream, readChunkSize, cancellationToken);
        }

        /// <summary>
        /// 将HTTP响应内容下载到可写入流（逐块 AT+HTTPREAD 模式，支持大文件）
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="destination">目标流（由调用方负责释放）</param>
        /// <param name="readChunkSize">单次读取块大小，默认 1024 字节</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<HttpResponse> DownloadToStreamAsync(string url, Stream destination, int readChunkSize = 1024, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL不能为空", nameof(url));

            ArgumentNullException.ThrowIfNull(destination);
            if (!destination.CanWrite)
                throw new ArgumentException("目标流不可写", nameof(destination));

            if (readChunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(readChunkSize), "读取块大小必须大于 0");

            cancellationToken.ThrowIfCancellationRequested();

            await InitializeHttpServiceAsync(cancellationToken);

            try
            {
                await ConfigureHttpSessionAsync(url, cancellationToken);
                var actionResult = await ExecuteHttpActionAsync(HttpMethod.GET, null, null, cancellationToken);

                LogDebug($"HTTP响应: 状态码={actionResult.StatusCode}, 内容长度={actionResult.ContentLength}");

                if (actionResult.ContentLength > 0)
                {
                    int offset = 0;
                    int contentLength = actionResult.ContentLength;

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int chunkSize = Math.Min(readChunkSize, contentLength - offset);
                        if (chunkSize <= 0)
                            break;

                        int timeoutMs = GetHttpReadTimeoutMs(chunkSize);
                        LogDebug($"AT+HTTPREAD={offset},{chunkSize} (超时={timeoutMs}ms)");

                        byte[] chunk = await _channel.SendCommandForBinaryPayloadAsync(
                            $"AT+HTTPREAD={offset},{chunkSize}", "+HTTPREAD:", timeoutMs, cancellationToken);

                        if (chunk.Length == 0)
                        {
                            LogDebug("HTTPREAD 返回 0 字节，下载完成");
                            break;
                        }

                        await destination.WriteAsync(chunk, 0, chunk.Length, cancellationToken);
                        offset += chunk.Length;
                        LogDebug($"已接收 {offset}/{contentLength} 字节");

                        if (offset >= contentLength)
                            break;
                    }
                }

                actionResult.Headers = await TryReadHeadersAsync(cancellationToken);
                return actionResult;
            }
            finally
            {
                await TerminateHttpServiceAsync();
            }
        }

        /// <summary>
        /// 将HTTP响应内容下载到字节数组缓冲区
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="readChunkSize">单次读取块大小，默认 1024 字节</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<byte[]> DownloadToBufferAsync(string url, int readChunkSize = 1024, CancellationToken cancellationToken = default)
        {
            using var memoryStream = await DownloadToMemoryStreamAsync(url, readChunkSize, cancellationToken);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// 将HTTP响应内容下载到内存流（Position 已重置到 0）
        /// </summary>
        /// <param name="url">文件URL</param>
        /// <param name="readChunkSize">单次读取块大小，默认 1024 字节</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<MemoryStream> DownloadToMemoryStreamAsync(string url, int readChunkSize = 1024, CancellationToken cancellationToken = default)
        {
            var memoryStream = new MemoryStream();
            await DownloadToStreamAsync(url, memoryStream, readChunkSize, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }

        /// <summary>
        /// 发起HTTP请求（完整版本）
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="method">HTTP方法</param>
        /// <param name="body">请求体（POST/PUT时使用）</param>
        /// <param name="contentType">Content-Type</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<HttpResponse> RequestAsync(
            string url,
            HttpMethod method = HttpMethod.GET,
            string? body = null,
            string contentType = "application/json",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL不能为空", nameof(url));

            await InitializeHttpServiceAsync(cancellationToken);

            try
            {
                await ConfigureHttpSessionAsync(url, cancellationToken);
                var result = await ExecuteHttpActionAsync(method, body, contentType, cancellationToken);

                if (result.ContentLength > 0 && method != HttpMethod.HEAD)
                    result.Body = await ReadHttpBodyAsTextAsync(result.ContentLength, cancellationToken);

                result.Headers = await TryReadHeadersAsync(cancellationToken);
                return result;
            }
            finally
            {
                await TerminateHttpServiceAsync();
            }
        }

        private async Task InitializeHttpServiceAsync(CancellationToken cancellationToken)
        {
            // 先终止可能残留的HTTP会话，再初始化，避免因上次异常退出导致 AT+HTTPINIT 返回 ERROR
            await _channel.SendCommandAsync("AT+HTTPTERM", 3000, cancellationToken);

            var initResp = await SendLoggedCommandAsync("AT+HTTPINIT", 5000, cancellationToken);
            if (!initResp.IsOk)
                throw new AtCommandErrorException("AT+HTTPINIT", initResp.RawResponse);
        }

        private async Task ConfigureHttpSessionAsync(string url, CancellationToken cancellationToken)
        {
            var urlResp = await SendLoggedCommandAsync($"AT+HTTPPARA=\"URL\",\"{url}\"", 5000, cancellationToken);
            if (!urlResp.IsOk)
                throw new AtCommandErrorException("AT+HTTPPARA URL", urlResp.RawResponse);
        }

        private async Task<HttpResponse> ExecuteHttpActionAsync(HttpMethod method, string? body, string? contentType, CancellationToken cancellationToken)
        {
            if ((method == HttpMethod.POST || method == HttpMethod.PUT) && !string.IsNullOrEmpty(body))
            {
                string ct = contentType ?? "application/json";
                await SendLoggedCommandAsync($"AT+HTTPPARA=\"CONTENT\",\"{ct}\"", 5000, cancellationToken);

                int bodyLen = Encoding.UTF8.GetByteCount(body);
                var dataPrompt = await SendLoggedCommandAsync($"AT+HTTPDATA={bodyLen},10000", 12000, cancellationToken);
                if (dataPrompt.RawResponse.Contains("DOWNLOAD"))
                {
                    _channel.SendRaw(body);
                    await Task.Delay(500, cancellationToken);
                }
            }

            int methodCode = (int)method;
            var actionResp = await SendLoggedCommandAsync($"AT+HTTPACTION={methodCode}", 60000, cancellationToken);
            if (!actionResp.IsOk)
                throw new AtCommandErrorException($"AT+HTTPACTION={methodCode}", actionResp.RawResponse);

            return await WaitForHttpActionResultAsync(60000, cancellationToken);
        }

        private async Task<string?> TryReadHeadersAsync(CancellationToken cancellationToken)
        {
            var headResp = await SendLoggedCommandAsync("AT+HTTPHEAD", 10000, cancellationToken);
            return headResp.IsOk ? ParseHttpHeadResponse(headResp) : null;
        }

        private async Task TerminateHttpServiceAsync()
        {
            await _channel.SendCommandAsync("AT+HTTPTERM");
        }

        private async Task<string?> ReadHttpBodyAsTextAsync(int contentLength, CancellationToken cancellationToken)
        {
            const int textReadChunkSize = 1024;
            var sb = new StringBuilder();
            int offset = 0;

            while (offset < contentLength)
            {
                int chunkSize = Math.Min(textReadChunkSize, contentLength - offset);
                int timeoutMs = GetHttpReadTimeoutMs(chunkSize);

                byte[] chunk = await _channel.SendCommandForBinaryPayloadAsync(
                    $"AT+HTTPREAD={offset},{chunkSize}", "+HTTPREAD:", timeoutMs, cancellationToken);

                if (chunk.Length == 0)
                    break;

                sb.Append(ResolveBodyEncoding(chunk));
                offset += chunk.Length;
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        private async Task<HttpResponse> WaitForHttpActionResultAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            var tcs = new TaskCompletionSource<HttpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnUrc(object? sender, string urc)
            {
                // +HTTPACTION: <method>,<status_code>,<data_len>
                if (urc.StartsWith("+HTTPACTION:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = urc.Substring(12).Trim();
                    string[] parts = data.Split(',');
                    if (parts.Length >= 3)
                    {
                        int statusCode = int.TryParse(parts[1].Trim(), out int sc) ? sc : 0;
                        int contentLen = int.TryParse(parts[2].Trim(), out int cl) ? cl : 0;
                        tcs.TrySetResult(new HttpResponse
                        {
                            StatusCode = statusCode,
                            ContentLength = contentLen
                        });
                    }
                }
            }

            _channel.UnsolicitedReceived += OnUrc;
            linkedCts.Token.Register(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    tcs.TrySetCanceled(cancellationToken);
                else
                    tcs.TrySetException(new AtCommandTimeoutException("HTTPACTION result"));
            });

            try
            {
                return await tcs.Task;
            }
            finally
            {
                _channel.UnsolicitedReceived -= OnUrc;
            }
        }

        private async Task<AtResponse> SendLoggedCommandAsync(string command, int timeoutMs, CancellationToken cancellationToken)
        {
            LogDebug($"-> {command}");
            var response = await _channel.SendCommandAsync(command, timeoutMs, cancellationToken);
            LogDebug($"<- {FormatRawResponse(response.RawResponse)}");
            return response;
        }

        private static int GetHttpReadTimeoutMs(int chunkSize)
        {
            int timeout = 60_000 + (int)((long)chunkSize * 30_000 / 1_024);
            return Math.Min(timeout, 600_000);
        }

        private static string ResolveBodyEncoding(byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        private static string FormatRawResponse(string raw)
        {
            return raw.Replace("\r\n", " | ").Replace("\r", "").Replace("\n", "");
        }

        private void LogDebug(string message) => DebugLogOutput?.Invoke(message);

        private void LogWarning(string message) => WarningLogOutput?.Invoke(message);

        private static string? ParseHttpHeadResponse(AtResponse resp)
        {
            var sb = new StringBuilder();
            bool reading = false;
            foreach (var line in resp.Lines)
            {
                if (line.StartsWith("+HTTPHEAD:", StringComparison.OrdinalIgnoreCase))
                {
                    reading = true;
                    continue;
                }
                if (reading && line != "OK")
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }
    }
}
