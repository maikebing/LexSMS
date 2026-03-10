using System.Collections.Generic;

namespace LexSMS.Models
{
    /// <summary>
    /// HTTP响应
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// HTTP状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 响应体内容
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// 响应体长度（字节）
        /// </summary>
        public int ContentLength { get; set; }

        /// <summary>
        /// 是否成功（2xx状态码）
        /// </summary>
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    }

    /// <summary>
    /// HTTP请求方法枚举
    /// </summary>
    public enum HttpMethod
    {
        GET = 0,
        POST = 1,
        HEAD = 2,
        DELETE = 3,
        PUT = 4
    }
}
