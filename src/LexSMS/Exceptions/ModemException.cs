using System;

namespace LexSMS.Exceptions
{
    /// <summary>
    /// 模块通信异常
    /// </summary>
    public class ModemException : Exception
    {
        public ModemException(string message) : base(message) { }
        public ModemException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// AT命令超时异常
    /// </summary>
    public class AtCommandTimeoutException : ModemException
    {
        public string Command { get; }

        public AtCommandTimeoutException(string command)
            : base($"AT命令超时: {command}")
        {
            Command = command;
        }
    }

    /// <summary>
    /// AT命令错误异常
    /// </summary>
    public class AtCommandErrorException : ModemException
    {
        public string Command { get; }
        public string ErrorResponse { get; }

        public AtCommandErrorException(string command, string errorResponse)
            : base($"AT命令执行失败 [{command}]: {errorResponse}")
        {
            Command = command;
            ErrorResponse = errorResponse;
        }
    }
}
