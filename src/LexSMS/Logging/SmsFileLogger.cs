using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LexSMS.Models;

namespace LexSMS.Logging
{
    /// <summary>
    /// 短信文件日志记录器
    /// 将短信交互记录到专门的日志文件中，支持按日期和联系人分类
    /// </summary>
    public class SmsFileLogger
    {
        private readonly string _baseDirectory;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly bool _enabled;

        /// <summary>
        /// 初始化短信文件日志记录器
        /// </summary>
        /// <param name="baseDirectory">日志文件基础目录</param>
        /// <param name="enabled">是否启用日志记录</param>
        public SmsFileLogger(string baseDirectory, bool enabled = true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
            _baseDirectory = baseDirectory;
            _enabled = enabled;

            if (_enabled && !Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
            }
        }

        /// <summary>
        /// 记录接收的短信
        /// </summary>
        public async Task LogReceivedSmsAsync(SmsMessage message, CancellationToken cancellationToken = default)
        {
            if (!_enabled || message == null) return;

            var timestamp = message.Timestamp ?? DateTime.Now;
            var phoneNumber = SanitizePhoneNumber(message.PhoneNumber);
            var content = message.Content ?? string.Empty;

            var logEntry = new StringBuilder();
            logEntry.AppendLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] 接收短信");
            logEntry.AppendLine($"发件人: {phoneNumber}");
            logEntry.AppendLine($"索引: {message.Index}");
            logEntry.AppendLine($"状态: {message.Status}");
            logEntry.AppendLine($"内容: {content}");
            logEntry.AppendLine($"内容长度: {content.Length} 字符");
            logEntry.AppendLine(new string('-', 80));
            logEntry.AppendLine();

            await WriteLogEntryAsync(phoneNumber, timestamp, logEntry.ToString(), cancellationToken);
        }

        /// <summary>
        /// 记录发送的短信
        /// </summary>
        public async Task LogSentSmsAsync(string phoneNumber, string content, bool success, string? errorMessage = null, CancellationToken cancellationToken = default)
        {
            if (!_enabled) return;

            ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

            var timestamp = DateTime.Now;
            var sanitizedNumber = SanitizePhoneNumber(phoneNumber);

            var logEntry = new StringBuilder();
            logEntry.AppendLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] 发送短信");
            logEntry.AppendLine($"收件人: {sanitizedNumber}");
            logEntry.AppendLine($"结果: {(success ? "成功" : "失败")}");
            if (!success && !string.IsNullOrWhiteSpace(errorMessage))
            {
                logEntry.AppendLine($"错误: {errorMessage}");
            }
            logEntry.AppendLine($"内容: {content ?? "(空)"}");
            logEntry.AppendLine($"内容长度: {content?.Length ?? 0} 字符");
            logEntry.AppendLine(new string('-', 80));
            logEntry.AppendLine();

            await WriteLogEntryAsync(sanitizedNumber, timestamp, logEntry.ToString(), cancellationToken);
        }

        /// <summary>
        /// 记录长短信合并事件
        /// </summary>
        public async Task LogLongSmsEventAsync(string phoneNumber, int totalParts, int receivedParts, bool isComplete, CancellationToken cancellationToken = default)
        {
            if (!_enabled) return;

            var timestamp = DateTime.Now;
            var sanitizedNumber = SanitizePhoneNumber(phoneNumber);

            var logEntry = new StringBuilder();
            logEntry.AppendLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] 长短信处理");
            logEntry.AppendLine($"发件人: {sanitizedNumber}");
            logEntry.AppendLine($"总段数: {totalParts}");
            logEntry.AppendLine($"已接收: {receivedParts}");
            logEntry.AppendLine($"状态: {(isComplete ? "合并完成" : "等待其他分段")}");
            logEntry.AppendLine(new string('-', 80));
            logEntry.AppendLine();

            await WriteLogEntryAsync(sanitizedNumber, timestamp, logEntry.ToString(), cancellationToken);
        }

        /// <summary>
        /// 获取指定联系人的日志文件路径列表
        /// </summary>
        public string[] GetLogFilesForContact(string phoneNumber)
        {
            if (!_enabled) return Array.Empty<string>();

            var sanitizedNumber = SanitizePhoneNumber(phoneNumber);
            var contactDir = Path.Combine(_baseDirectory, "by_contact", sanitizedNumber);

            return Directory.Exists(contactDir)
                ? Directory.GetFiles(contactDir, "*.log", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
        }

        /// <summary>
        /// 获取指定日期的日志文件路径
        /// </summary>
        public string? GetLogFileForDate(DateTime date)
        {
            if (!_enabled) return null;

            var dateDir = Path.Combine(_baseDirectory, "by_date", date.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            var fileName = $"sms_{date:yyyy-MM-dd}.log";
            var filePath = Path.Combine(dateDir, fileName);

            return File.Exists(filePath) ? filePath : null;
        }

        /// <summary>
        /// 读取日志文件内容
        /// </summary>
        public async Task<string?> ReadLogFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!_enabled || !File.Exists(filePath)) return null;

            try
            {
                return await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        private async Task WriteLogEntryAsync(string phoneNumber, DateTime timestamp, string logEntry, CancellationToken cancellationToken)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                // 按日期存储
                await WriteToDateLogAsync(timestamp, logEntry, cancellationToken);

                // 按联系人存储
                await WriteToContactLogAsync(phoneNumber, timestamp, logEntry, cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task WriteToDateLogAsync(DateTime timestamp, string logEntry, CancellationToken cancellationToken)
        {
            var dateDir = Path.Combine(_baseDirectory, "by_date", timestamp.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(dateDir);

            var fileName = $"sms_{timestamp:yyyy-MM-dd}.log";
            var filePath = Path.Combine(dateDir, fileName);

            await File.AppendAllTextAsync(filePath, logEntry, Encoding.UTF8, cancellationToken);
        }

        private async Task WriteToContactLogAsync(string phoneNumber, DateTime timestamp, string logEntry, CancellationToken cancellationToken)
        {
            var contactDir = Path.Combine(_baseDirectory, "by_contact", phoneNumber);
            Directory.CreateDirectory(contactDir);

            var fileName = $"{timestamp:yyyy-MM}.log";
            var filePath = Path.Combine(contactDir, fileName);

            await File.AppendAllTextAsync(filePath, logEntry, Encoding.UTF8, cancellationToken);
        }

        private static string SanitizePhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return "unknown";
            }

            var sb = new StringBuilder();
            foreach (var c in phoneNumber)
            {
                if (char.IsDigit(c) || c == '+')
                {
                    sb.Append(c);
                }
            }

            return sb.Length > 0 ? sb.ToString() : "unknown";
        }

        public void Dispose()
        {
            _writeLock.Dispose();
        }
    }
}
