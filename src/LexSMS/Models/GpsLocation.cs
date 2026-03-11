namespace LexSMS.Models
{
    /// <summary>
    /// GPS 定位信息
    /// </summary>
    public class GpsLocation
    {
        /// <summary>
        /// 纬度（十进制度）
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// 经度（十进制度）
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// 海拔（米）
        /// </summary>
        public double? AltitudeMeters { get; set; }

        /// <summary>
        /// GPS UTC 时间
        /// </summary>
        public DateTimeOffset? UtcTimestamp { get; set; }

        /// <summary>
        /// 定位是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
