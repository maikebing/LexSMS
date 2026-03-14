using System;
using System.Threading;
using System.Threading.Tasks;
using LexSMS.Logging;
using LexSMS.Models;

namespace LexSMS.Features
{
    /// <summary>
    /// 短信存储管理器
    /// 协调短信的文件日志记录（LexSMS 库级别）
    /// 数据库存储由应用层（LexApp）负责
    /// </summary>
    public class SmsStorageManager
    {
        private readonly SmsFileLogger? _fileLogger;

        /// <summary>
        /// 初始化短信存储管理器
        /// </summary>
        /// <param name="fileLogger">文件日志记录器（可选）</param>
        public SmsStorageManager(SmsFileLogger? fileLogger = null)
        {
            _fileLogger = fileLogger;
        }

        /// <summary>
        /// 当收到短信时记录到文件日志
        /// </summary>
        public async Task OnSmsReceivedAsync(SmsMessage message, CancellationToken cancellationToken = default)
        {
            if (_fileLogger != null && message != null)
            {
                await _fileLogger.LogReceivedSmsAsync(message, cancellationToken);
            }
        }

        /// <summary>
        /// 当发送短信时记录到文件日志
        /// </summary>
        public async Task OnSmsSentAsync(string phoneNumber, string content, bool success, string? errorMessage = null, CancellationToken cancellationToken = default)
        {
            if (_fileLogger != null)
            {
                await _fileLogger.LogSentSmsAsync(phoneNumber, content, success, errorMessage, cancellationToken);
            }
        }

        /// <summary>
        /// 记录长短信处理事件
        /// </summary>
        public async Task OnLongSmsEventAsync(string phoneNumber, int totalParts, int receivedParts, bool isComplete, CancellationToken cancellationToken = default)
        {
            if (_fileLogger != null)
            {
                await _fileLogger.LogLongSmsEventAsync(phoneNumber, totalParts, receivedParts, isComplete, cancellationToken);
            }
        }

        /// <summary>
        /// 获取指定联系人的所有日志文件路径
        /// </summary>
        public string[] GetLogFilesForContact(string phoneNumber)
        {
            return _fileLogger?.GetLogFilesForContact(phoneNumber) ?? Array.Empty<string>();
        }

        /// <summary>
        /// 获取指定日期的日志文件路径
        /// </summary>
        public string? GetLogFileForDate(DateTime date)
        {
            return _fileLogger?.GetLogFileForDate(date);
        }

        /// <summary>
        /// 读取日志文件内容
        /// </summary>
        public async Task<string?> ReadLogFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (_fileLogger == null) return null;
            return await _fileLogger.ReadLogFileAsync(filePath, cancellationToken);
        }
    }
}
