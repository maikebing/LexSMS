namespace LexSMS.Models
{
    /// <summary>
    /// MQTT连接配置
    /// </summary>
    public class MqttConfig
    {
        /// <summary>
        /// MQTT Broker 地址
        /// </summary>
        public string BrokerAddress { get; set; } = string.Empty;

        /// <summary>
        /// MQTT Broker 端口，默认 1883
        /// </summary>
        public int Port { get; set; } = 1883;

        /// <summary>
        /// 客户端ID
        /// </summary>
        public string ClientId { get; set; } = "A76XX_Client";

        /// <summary>
        /// 用户名（可选）
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// 密码（可选）
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Keep-Alive间隔（秒），默认 60
        /// </summary>
        public int KeepAliveSeconds { get; set; } = 60;

        /// <summary>
        /// 是否清除会话，默认 true
        /// </summary>
        public bool CleanSession { get; set; } = true;

        /// <summary>
        /// 是否使用SSL/TLS
        /// </summary>
        public bool UseSsl { get; set; } = false;
    }

    /// <summary>
    /// MQTT消息
    /// </summary>
    public class MqttMessage
    {
        /// <summary>
        /// 主题
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>
        /// 服务质量等级 (QoS)，0, 1, 或 2
        /// </summary>
        public int Qos { get; set; } = 0;

        /// <summary>
        /// 是否保留消息
        /// </summary>
        public bool Retain { get; set; } = false;
    }
}
