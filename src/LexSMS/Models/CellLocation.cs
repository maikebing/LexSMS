namespace LexSMS.Models
{
    /// <summary>
    /// 基站定位信息
    /// </summary>
    public class CellLocation
    {
        /// <summary>
        /// 纬度（度数，正为北纬，负为南纬）
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// 经度（度数，正为东经，负为西经）
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// 定位精度（米）
        /// </summary>
        public int AccuracyMeters { get; set; }

        /// <summary>
        /// 移动国家码 (MCC)
        /// </summary>
        public int Mcc { get; set; }

        /// <summary>
        /// 移动网络码 (MNC)
        /// </summary>
        public int Mnc { get; set; }

        /// <summary>
        /// 位置区域码 (LAC)，或 LTE 跟踪区域码 (TAC)
        /// 2G/3G 网络使用 LAC；4G/LTE 网络使用 TAC（含义相同，均存储在此字段）
        /// </summary>
        public int Lac { get; set; }

        /// <summary>
        /// 小区ID (Cell ID)
        /// </summary>
        public int CellId { get; set; }

        /// <summary>
        /// 定位是否有效
        /// </summary>
        public bool IsValid { get; set; }
    }
}
