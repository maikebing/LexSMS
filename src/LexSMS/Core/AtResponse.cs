using System.Collections.Generic;

namespace LexSMS.Core
{
    /// <summary>
    /// AT命令响应
    /// </summary>
    public class AtResponse
    {
        /// <summary>
        /// 响应是否成功（包含 OK）
        /// </summary>
        public bool IsOk { get; set; }

        /// <summary>
        /// 是否包含错误
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// 所有响应行
        /// </summary>
        public List<string> Lines { get; set; } = new List<string>();

        /// <summary>
        /// 原始响应字符串
        /// </summary>
        public string RawResponse { get; set; } = string.Empty;

        /// <summary>
        /// 获取第一个有内容的响应行（排除 OK/ERROR）
        /// </summary>
        public string? FirstLine
        {
            get
            {
                foreach (var line in Lines)
                {
                    if (!string.IsNullOrWhiteSpace(line) &&
                        line != "OK" &&
                        line != "ERROR" &&
                        !line.StartsWith("+CME ERROR") &&
                        !line.StartsWith("+CMS ERROR"))
                    {
                        return line;
                    }
                }
                return null;
            }
        }
    }
}
