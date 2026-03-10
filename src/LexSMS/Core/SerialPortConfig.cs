using System.IO.Ports;

namespace LexSMS.Core
{
    /// <summary>
    /// 串口配置
    /// </summary>
    public class SerialPortConfig
    {
        /// <summary>
        /// 串口名称，例如 COM3 或 /dev/ttyUSB0
        /// </summary>
        public string PortName { get; set; } = "COM3";

        /// <summary>
        /// 波特率，默认 115200
        /// </summary>
        public int BaudRate { get; set; } = 115200;

        /// <summary>
        /// 数据位，默认 8
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// 停止位，默认 1
        /// </summary>
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>
        /// 校验位，默认 None
        /// </summary>
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>
        /// AT命令响应超时（毫秒），默认 10000ms
        /// </summary>
        public int CommandTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// 读取操作超时（毫秒），默认 500ms
        /// </summary>
        public int ReadTimeoutMs { get; set; } = 500;
    }
}
