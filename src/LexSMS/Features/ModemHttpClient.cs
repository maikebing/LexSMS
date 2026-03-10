using System;
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

        public ModemHttpClient(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>
        /// 发起HTTP GET请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <returns>HTTP响应</returns>
        public async Task<HttpResponse> GetAsync(string url)
        {
            return await RequestAsync(url, HttpMethod.GET);
        }

        /// <summary>
        /// 发起HTTP POST请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="body">请求体</param>
        /// <param name="contentType">Content-Type</param>
        /// <returns>HTTP响应</returns>
        public async Task<HttpResponse> PostAsync(string url, string body, string contentType = "application/json")
        {
            return await RequestAsync(url, HttpMethod.POST, body, contentType);
        }

        /// <summary>
        /// 读取HTTP响应头（AT+HTTPHEAD）
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <returns>包含响应头信息的HTTP响应</returns>
        public async Task<HttpResponse> GetHeadersAsync(string url)
        {
            return await RequestAsync(url, HttpMethod.HEAD);
        }

        /// <summary>
        /// 发起HTTP请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="method">HTTP方法</param>
        /// <param name="body">请求体（POST/PUT时使用）</param>
        /// <param name="contentType">Content-Type</param>
        public async Task<HttpResponse> RequestAsync(
            string url,
            HttpMethod method = HttpMethod.GET,
            string? body = null,
            string contentType = "application/json")
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL不能为空", nameof(url));

            // 初始化HTTP服务
            var initResp = await _channel.SendCommandAsync("AT+HTTPINIT");
            if (!initResp.IsOk)
                throw new AtCommandErrorException("AT+HTTPINIT", initResp.RawResponse);

            try
            {
                // 设置URL
                var urlResp = await _channel.SendCommandAsync($"AT+HTTPPARA=\"URL\",\"{url}\"");
                if (!urlResp.IsOk)
                    throw new AtCommandErrorException($"AT+HTTPPARA URL", urlResp.RawResponse);

                // 设置CID（数据连接ID）
                await _channel.SendCommandAsync("AT+HTTPPARA=\"CID\",1");

                // 设置Content-Type（POST时）
                if (method == HttpMethod.POST || method == HttpMethod.PUT)
                {
                    await _channel.SendCommandAsync($"AT+HTTPPARA=\"CONTENT\",\"{contentType}\"");

                    if (!string.IsNullOrEmpty(body))
                    {
                        int bodyLen = System.Text.Encoding.UTF8.GetByteCount(body);
                        // 设置POST数据
                        var dataPrompt = await _channel.SendCommandAsync($"AT+HTTPDATA={bodyLen},10000", 12000);
                        if (dataPrompt.RawResponse.Contains("DOWNLOAD"))
                        {
                            _channel.SendRaw(body);
                            await Task.Delay(500);
                        }
                    }
                }

                // 执行HTTP动作
                int methodCode = (int)method;
                var actionResp = await _channel.SendCommandAsync($"AT+HTTPACTION={methodCode}", 60000);
                if (!actionResp.IsOk)
                    throw new AtCommandErrorException($"AT+HTTPACTION={methodCode}", actionResp.RawResponse);

                // 等待HTTP动作完成
                var result = await WaitForHttpActionResultAsync(60000);

                // 读取响应内容（HEAD 请求不含响应体）
                if (result.ContentLength > 0 && method != HttpMethod.HEAD)
                {
                    var readResp = await _channel.SendCommandAsync($"AT+HTTPREAD=0,{Math.Min(result.ContentLength, 4096)}", 30000);
                    result.Body = ParseHttpReadResponse(readResp);
                }

                // 读取响应头（AT+HTTPHEAD）
                var headResp = await _channel.SendCommandAsync("AT+HTTPHEAD", 10000);
                if (headResp.IsOk)
                    result.Headers = ParseHttpHeadResponse(headResp);

                return result;
            }
            finally
            {
                // 终止HTTP服务，释放资源
                await _channel.SendCommandAsync("AT+HTTPTERM");
            }
        }

        private async Task<HttpResponse> WaitForHttpActionResultAsync(int timeoutMs)
        {
            var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            var tcs = new System.Threading.Tasks.TaskCompletionSource<HttpResponse>();

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
            cts.Token.Register(() => tcs.TrySetException(new AtCommandTimeoutException("HTTPACTION result")));

            try
            {
                return await tcs.Task;
            }
            finally
            {
                _channel.UnsolicitedReceived -= OnUrc;
            }
        }

        private static string? ParseHttpReadResponse(AtResponse resp)
        {
            // 响应格式:
            // +HTTPREAD: <data_len>
            // <data>
            // OK
            var sb = new System.Text.StringBuilder();
            bool reading = false;
            foreach (var line in resp.Lines)
            {
                if (line.StartsWith("+HTTPREAD:", StringComparison.OrdinalIgnoreCase))
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

        private static string? ParseHttpHeadResponse(AtResponse resp)
        {
            // 响应格式:
            // +HTTPHEAD: <data_len>
            // <headers>
            // OK
            var sb = new System.Text.StringBuilder();
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
