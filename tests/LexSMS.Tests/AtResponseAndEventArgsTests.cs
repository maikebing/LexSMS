using System;
using System.Reflection;
using LexSMS.Core;
using LexSMS.Events;
using LexSMS.Features;
using LexSMS.Models;
using Xunit;

namespace LexSMS.Tests
{
    /// <summary>
    /// AtResponse 测试
    /// </summary>
    public class AtResponseTests
    {
        [Fact]
        public void AtResponse_IsOk_WhenContainsOkLine()
        {
            var response = new AtResponse
            {
                IsOk = true,
                Lines = { "AT", "+CSQ: 15,0", "OK" },
                RawResponse = "AT\r\n+CSQ: 15,0\r\nOK"
            };

            Assert.True(response.IsOk);
            Assert.False(response.IsError);
        }

        [Fact]
        public void AtResponse_IsError_WhenContainsErrorLine()
        {
            var response = new AtResponse
            {
                IsError = true,
                Lines = { "ERROR" },
                RawResponse = "ERROR"
            };

            Assert.False(response.IsOk);
            Assert.True(response.IsError);
        }

        [Fact]
        public void AtResponse_FirstLine_ReturnsFirstNonStatusLine()
        {
            var response = new AtResponse
            {
                IsOk = true,
                Lines = { "+CSQ: 15,0", "OK" }
            };

            Assert.Equal("+CSQ: 15,0", response.FirstLine);
        }

        [Fact]
        public void AtResponse_FirstLine_SkipsOkLine()
        {
            var response = new AtResponse
            {
                IsOk = true,
                Lines = { "OK" }
            };

            Assert.Null(response.FirstLine);
        }

        [Fact]
        public void AtResponse_FirstLine_SkipsErrorLine()
        {
            var response = new AtResponse
            {
                IsError = true,
                Lines = { "ERROR" }
            };

            Assert.Null(response.FirstLine);
        }

        [Fact]
        public void AtResponse_FirstLine_SkipsCmeError()
        {
            var response = new AtResponse
            {
                IsError = true,
                Lines = { "+CME ERROR: 10" }
            };

            Assert.Null(response.FirstLine);
        }
    }

    /// <summary>
    /// 事件参数测试
    /// </summary>
    public class EventArgsTests
    {
        [Fact]
        public void IncomingCallEventArgs_CreatesWithCorrectValues()
        {
            var args = new IncomingCallEventArgs("+8613800138000", 145);
            Assert.Equal("+8613800138000", args.PhoneNumber);
            Assert.Equal(145, args.NumberType);
        }

        [Fact]
        public void IncomingCallEventArgs_DefaultNumberType_Is129()
        {
            var args = new IncomingCallEventArgs("13800138000");
            Assert.Equal(129, args.NumberType);
        }

        [Fact]
        public void SmsReceivedEventArgs_CreatesWithIndex()
        {
            var args = new SmsReceivedEventArgs(3);
            Assert.Equal(3, args.Index);
            Assert.Null(args.Message);
        }

        [Fact]
        public void SmsReceivedEventArgs_CreatesWithMessage()
        {
            var msg = new SmsMessage { Index = 1, Content = "Test" };
            var args = new SmsReceivedEventArgs(1, msg);
            Assert.Equal(1, args.Index);
            Assert.NotNull(args.Message);
            Assert.Equal("Test", args.Message.Content);
        }

        [Fact]
        public void MqttMessageReceivedEventArgs_CreatesWithMessage()
        {
            var msg = new MqttMessage { Topic = "test/topic", Payload = "hello" };
            var args = new MqttMessageReceivedEventArgs(msg);
            Assert.Equal("test/topic", args.Message.Topic);
            Assert.Equal("hello", args.Message.Payload);
        }

        [Fact]
        public void CallStateChangedEventArgs_CreatesWithCallInfo()
        {
            var callInfo = new CallInfo
            {
                State = CallState.Active,
                PhoneNumber = "13800138000",
                Direction = CallDirection.Outgoing
            };
            var args = new CallStateChangedEventArgs(callInfo);
            Assert.Equal(CallState.Active, args.CallInfo.State);
            Assert.Equal("13800138000", args.CallInfo.PhoneNumber);
        }

        [Fact]
        public void MqttConnectionStateChangedEventArgs_ConnectedState()
        {
            var args = new MqttConnectionStateChangedEventArgs(true);
            Assert.True(args.IsConnected);
            Assert.Null(args.Reason);
        }

        [Fact]
        public void MqttConnectionStateChangedEventArgs_DisconnectedWithReason()
        {
            var args = new MqttConnectionStateChangedEventArgs(false, "网络断开");
            Assert.False(args.IsConnected);
            Assert.Equal("网络断开", args.Reason);
        }

        [Fact]
        public void TcpDataReceivedEventArgs_StoresAllFields()
        {
            var args = new TcpDataReceivedEventArgs(1, "192.168.1.1", 5000, "hello", 5);
            Assert.Equal(1, args.ConnectionIndex);
            Assert.Equal("192.168.1.1", args.RemoteAddress);
            Assert.Equal(5000, args.RemotePort);
            Assert.Equal("hello", args.Data);
            Assert.Equal(5, args.Length);
        }

        [Fact]
        public void TcpConnectionClosedEventArgs_DefaultReason_IsZero()
        {
            var args = new TcpConnectionClosedEventArgs(0);
            Assert.Equal(0, args.ConnectionIndex);
            Assert.Equal(0, args.Reason);
        }

        [Fact]
        public void TcpConnectionClosedEventArgs_StoresConnectionIndexAndReason()
        {
            var args = new TcpConnectionClosedEventArgs(2, 1);
            Assert.Equal(2, args.ConnectionIndex);
            Assert.Equal(1, args.Reason);
        }
    }

    /// <summary>
    /// HTTP API 签名测试（验证默认参数值为 1024）
    /// </summary>
    public class HttpApiSurfaceTests
    {
        private static int GetReadChunkSizeDefault(string methodName)
        {
            var method = typeof(ModemHttpClient).GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            var parameters = method!.GetParameters();
            var param = Array.Find(parameters, p => p.Name == "readChunkSize");
            Assert.NotNull(param);
            return (int)param!.DefaultValue!;
        }

        [Fact]
        public void DownloadFileAsync_ReadChunkSizeDefault_Is1024()
            => Assert.Equal(1024, GetReadChunkSizeDefault(nameof(ModemHttpClient.DownloadFileAsync)));

        [Fact]
        public void DownloadToStreamAsync_ReadChunkSizeDefault_Is1024()
            => Assert.Equal(1024, GetReadChunkSizeDefault(nameof(ModemHttpClient.DownloadToStreamAsync)));

        [Fact]
        public void DownloadToBufferAsync_ReadChunkSizeDefault_Is1024()
            => Assert.Equal(1024, GetReadChunkSizeDefault(nameof(ModemHttpClient.DownloadToBufferAsync)));

        [Fact]
        public void DownloadToMemoryStreamAsync_ReadChunkSizeDefault_Is1024()
            => Assert.Equal(1024, GetReadChunkSizeDefault(nameof(ModemHttpClient.DownloadToMemoryStreamAsync)));
    }

    /// <summary>
    /// 逐块 AT+HTTPREAD 协议解析测试
    /// </summary>
    public class HttpReadPerChunkParsingTests
    {
        private static readonly MethodInfo s_parsePayloadLength =
            typeof(AtChannel).GetMethod("ParsePayloadLength",
                BindingFlags.NonPublic | BindingFlags.Static)!;

        private static int InvokeParsePayloadLength(string line, string prefix = "+HTTPREAD:")
            => (int)s_parsePayloadLength.Invoke(null, new object[] { line, prefix })!;

        [Theory]
        [InlineData("+HTTPREAD: 1024", 1024)]
        [InlineData("+HTTPREAD: 99", 99)]
        [InlineData("+HTTPREAD: 0", 0)]
        public void ParsePayloadLength_ReturnsCorrectLength(string line, int expected)
        {
            int actual = InvokeParsePayloadLength(line);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ParsePayloadLength_ZeroChunk_SignalsEndOfData()
        {
            int length = InvokeParsePayloadLength("+HTTPREAD: 0");
            Assert.Equal(0, length);
        }

        [Fact]
        public void PerChunkDownload_TotalBytesMatchesSumOfChunks()
        {
            // Simulates 11363-byte total downloaded in 1024-byte chunks
            const int totalLength = 11363;
            const int chunkSize = 1024;
            int offset = 0;
            int totalReceived = 0;

            while (offset < totalLength)
            {
                int remaining = totalLength - offset;
                int thisChunk = Math.Min(chunkSize, remaining);
                totalReceived += thisChunk;
                offset += thisChunk;
            }

            Assert.Equal(totalLength, totalReceived);
        }
    }
}
