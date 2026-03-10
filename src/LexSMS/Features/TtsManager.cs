using System;
using System.Text;
using System.Threading.Tasks;
using LexSMS.Core;
using LexSMS.Exceptions;

namespace LexSMS.Features
{
    /// <summary>
    /// TTS（文字转语音）管理器
    /// 使用 AT+CTTS 指令实现文本朗读功能
    /// </summary>
    public class TtsManager
    {
        private readonly AtChannel _channel;

        public TtsManager(AtChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>
        /// 查询模块是否支持TTS功能
        /// </summary>
        /// <returns>支持返回 true，不支持返回 false</returns>
        public async Task<bool> IsSupportedAsync()
        {
            var resp = await _channel.SendCommandAsync("AT+CTTS=?");
            return resp.IsOk;
        }

        /// <summary>
        /// 播放文本（自动检测编码：纯ASCII使用混合编码，含Unicode字符使用UCS2编码）
        /// </summary>
        /// <param name="text">要朗读的文字（支持中文）</param>
        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("朗读文字不能为空", nameof(text));

            // 判断是否需要UCS2编码
            bool needsUcs2 = RequiresUcs2(text);

            if (needsUcs2)
            {
                // UCS2 编码方式: AT+CTTS=1,"<ucs2_hex>"
                string ucs2Hex = EncodeUcs2(text);
                var resp = await _channel.SendCommandAsync($"AT+CTTS=1,\"{ucs2Hex}\"", 30000);
                if (!resp.IsOk)
                    throw new AtCommandErrorException($"AT+CTTS=1", resp.RawResponse);
            }
            else
            {
                // 混合编码方式（ASCII + GBK）: AT+CTTS=2,"<text>"
                var resp = await _channel.SendCommandAsync($"AT+CTTS=2,\"{text}\"", 30000);
                if (!resp.IsOk)
                    throw new AtCommandErrorException($"AT+CTTS=2", resp.RawResponse);
            }
        }

        /// <summary>
        /// 使用UCS2编码播放文本（支持所有Unicode字符，包括中文）
        /// </summary>
        /// <param name="text">要朗读的文字</param>
        public async Task SpeakUcs2Async(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("朗读文字不能为空", nameof(text));

            string ucs2Hex = EncodeUcs2(text);
            var resp = await _channel.SendCommandAsync($"AT+CTTS=1,\"{ucs2Hex}\"", 30000);
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+CTTS=1", resp.RawResponse);
        }

        /// <summary>
        /// 使用混合编码播放文本（ASCII + GBK，适合英文与中文混合）
        /// </summary>
        /// <param name="text">要朗读的文字</param>
        public async Task SpeakMixedAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("朗读文字不能为空", nameof(text));

            var resp = await _channel.SendCommandAsync($"AT+CTTS=2,\"{text}\"", 30000);
            if (!resp.IsOk)
                throw new AtCommandErrorException("AT+CTTS=2", resp.RawResponse);
        }

        /// <summary>
        /// 停止当前TTS播放（AT+CTTS=0）
        /// </summary>
        public async Task StopAsync()
        {
            await _channel.SendCommandAsync("AT+CTTS=0", 5000);
        }

        /// <summary>
        /// 判断字符串是否包含非ASCII字符（需要UCS2编码）
        /// </summary>
        private static bool RequiresUcs2(string text)
        {
            foreach (char c in text)
            {
                if (c > 127) return true;
            }
            return false;
        }

        /// <summary>
        /// 将字符串编码为UCS2十六进制字符串
        /// </summary>
        private static string EncodeUcs2(string text)
        {
            byte[] bytes = Encoding.BigEndianUnicode.GetBytes(text);
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}
