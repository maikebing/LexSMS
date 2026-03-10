using System;
using LexSMS.Models;
using Xunit;

namespace LexSMS.Tests
{
    /// <summary>
    /// 数据模型测试
    /// </summary>
    public class ModelsTests
    {
        #region SignalInfo 测试

        [Fact]
        public void SignalInfo_RssiDbm_CalculatesCorrectly()
        {
            var signal = new SignalInfo { Rssi = 15 };
            // RssiDbm = -113 + 15 * 2 = -83
            Assert.Equal(-83, signal.RssiDbm);
        }

        [Fact]
        public void SignalInfo_UnknownRssi_ReturnsMinusNineNineNine()
        {
            var signal = new SignalInfo { Rssi = 99 };
            Assert.Equal(-999, signal.RssiDbm);
        }

        [Theory]
        [InlineData(99, "未知")]
        [InlineData(20, "强")]
        [InlineData(15, "良")]
        [InlineData(10, "中")]
        [InlineData(5, "弱")]
        [InlineData(2, "极弱")]
        public void SignalInfo_SignalLevel_ReturnsCorrectLevel(int rssi, string expectedLevel)
        {
            var signal = new SignalInfo { Rssi = rssi };
            Assert.Equal(expectedLevel, signal.SignalLevel);
        }

        #endregion

        #region NetworkInfo 测试

        [Theory]
        [InlineData(NetworkRegistrationStatus.RegisteredHome, true)]
        [InlineData(NetworkRegistrationStatus.RegisteredRoaming, true)]
        [InlineData(NetworkRegistrationStatus.NotRegistered, false)]
        [InlineData(NetworkRegistrationStatus.Searching, false)]
        [InlineData(NetworkRegistrationStatus.RegistrationDenied, false)]
        public void NetworkInfo_IsRegistered_ReturnsCorrectValue(
            NetworkRegistrationStatus status, bool expected)
        {
            var network = new NetworkInfo { CsRegistrationStatus = status };
            Assert.Equal(expected, network.IsRegistered);
        }

        [Fact]
        public void NetworkInfo_RegistrationStatus_PrioritizesEpsOverGprsOverCs()
        {
            // EPS 优先级最高
            var network1 = new NetworkInfo
            {
                CsRegistrationStatus = NetworkRegistrationStatus.RegisteredHome,
                GprsRegistrationStatus = NetworkRegistrationStatus.RegisteredHome,
                EpsRegistrationStatus = NetworkRegistrationStatus.RegisteredRoaming
            };
            Assert.Equal(NetworkRegistrationStatus.RegisteredRoaming, network1.RegistrationStatus);

            // GPRS 次之
            var network2 = new NetworkInfo
            {
                CsRegistrationStatus = NetworkRegistrationStatus.RegisteredHome,
                GprsRegistrationStatus = NetworkRegistrationStatus.RegisteredRoaming,
                EpsRegistrationStatus = NetworkRegistrationStatus.NotRegistered
            };
            Assert.Equal(NetworkRegistrationStatus.RegisteredRoaming, network2.RegistrationStatus);

            // CS 最后
            var network3 = new NetworkInfo
            {
                CsRegistrationStatus = NetworkRegistrationStatus.RegisteredHome,
                GprsRegistrationStatus = NetworkRegistrationStatus.NotRegistered,
                EpsRegistrationStatus = NetworkRegistrationStatus.Unknown
            };
            Assert.Equal(NetworkRegistrationStatus.RegisteredHome, network3.RegistrationStatus);
        }

        #endregion

        #region CallInfo 测试

        [Fact]
        public void CallInfo_IdleState_DurationIsZero()
        {
            var call = new CallInfo { State = CallState.Idle };
            Assert.Equal(TimeSpan.Zero, call.Duration);
        }

        [Fact]
        public void CallInfo_ActiveState_DurationIsPositive()
        {
            var call = new CallInfo
            {
                State = CallState.Active,
                StartTime = DateTime.Now.AddSeconds(-5)
            };
            Assert.True(call.Duration.TotalSeconds >= 4);
        }

        #endregion

        #region MqttConfig 测试

        [Fact]
        public void MqttConfig_DefaultValues_AreCorrect()
        {
            var config = new MqttConfig();
            Assert.Equal(1883, config.Port);
            Assert.Equal("A76XX_Client", config.ClientId);
            Assert.Equal(60, config.KeepAliveSeconds);
            Assert.True(config.CleanSession);
            Assert.False(config.UseSsl);
        }

        #endregion

        #region HttpResponse 测试

        [Theory]
        [InlineData(200, true)]
        [InlineData(201, true)]
        [InlineData(299, true)]
        [InlineData(300, false)]
        [InlineData(400, false)]
        [InlineData(500, false)]
        public void HttpResponse_IsSuccess_ReturnsCorrectValue(int statusCode, bool expected)
        {
            var response = new HttpResponse { StatusCode = statusCode };
            Assert.Equal(expected, response.IsSuccess);
        }

        [Fact]
        public void HttpResponse_Headers_IsNullByDefault()
        {
            var response = new HttpResponse { StatusCode = 200 };
            Assert.Null(response.Headers);
        }

        [Fact]
        public void HttpResponse_Headers_StoresHeaderContent()
        {
            var response = new HttpResponse
            {
                StatusCode = 200,
                Headers = "Content-Type: application/json\r\nContent-Length: 42"
            };
            Assert.NotNull(response.Headers);
            Assert.Contains("Content-Type", response.Headers);
        }

        #endregion

        #region SerialPortConfig 测试

        [Fact]
        public void SerialPortConfig_DefaultValues_AreCorrect()
        {
            var config = new LexSMS.Core.SerialPortConfig();
            Assert.Equal("COM3", config.PortName);
            Assert.Equal(115200, config.BaudRate);
            Assert.Equal(8, config.DataBits);
            Assert.Equal(10000, config.CommandTimeoutMs);
        }

        #endregion

        #region ModuleInfo 测试

        [Fact]
        public void ModuleInfo_DefaultVoltage_IsMinusOne()
        {
            var info = new ModuleInfo();
            Assert.Equal(-1, info.VoltageMillivolts);
            Assert.Equal(-1, info.BatteryPercent);
        }

        #endregion
    }
}
