namespace LexSMS.Models
{
    /// <summary>
    /// A76XX模块信息
    /// </summary>
    public class ModuleInfo
    {
        /// <summary>
        /// 制造商信息
        /// </summary>
        public string? Manufacturer { get; set; }

        /// <summary>
        /// 模块型号
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// 固件版本
        /// </summary>
        public string? FirmwareVersion { get; set; }

        /// <summary>
        /// 国际移动设备识别码 (IMEI)
        /// </summary>
        public string? Imei { get; set; }

        /// <summary>
        /// 模块工作状态
        /// </summary>
        public ModuleStatus Status { get; set; }

        /// <summary>
        /// 供电电压（mV），-1 表示未知
        /// </summary>
        public int VoltageMillivolts { get; set; } = -1;

        /// <summary>
        /// 电池电量百分比，-1 表示未知或无电池
        /// </summary>
        public int BatteryPercent { get; set; } = -1;
    }

    /// <summary>
    /// 模块工作状态枚举
    /// </summary>
    public enum ModuleStatus
    {
        /// <summary>正常工作</summary>
        Ready,
        /// <summary>初始化中</summary>
        Initializing,
        /// <summary>错误状态</summary>
        Error,
        /// <summary>未知状态</summary>
        Unknown
    }
}
