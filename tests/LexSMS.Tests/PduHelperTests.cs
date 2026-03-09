using System;
using System.Text;
using LexSMS.Helpers;
using Xunit;

namespace LexSMS.Tests
{
    /// <summary>
    /// PDU编解码工具测试
    /// </summary>
    public class PduHelperTests
    {
        #region UCS2编解码测试

        [Fact]
        public void EncodeUcs2_EnglishText_ReturnsHexString()
        {
            string text = "Hello";
            string result = PduHelper.EncodeUcs2(text);
            Assert.Equal("00480065006C006C006F", result);
        }

        [Fact]
        public void EncodeUcs2_ChineseText_ReturnsHexString()
        {
            // '你好' = U+4F60 U+597D
            string text = "你好";
            string result = PduHelper.EncodeUcs2(text);
            Assert.Equal("4F60597D", result);
        }

        [Fact]
        public void DecodeUcs2_HexString_ReturnsEnglishText()
        {
            string hex = "00480065006C006C006F";
            string result = PduHelper.DecodeUcs2(hex);
            Assert.Equal("Hello", result);
        }

        [Fact]
        public void DecodeUcs2_HexString_ReturnsChineseText()
        {
            string hex = "4F60597D";
            string result = PduHelper.DecodeUcs2(hex);
            Assert.Equal("你好", result);
        }

        [Fact]
        public void EncodeDecodeUcs2_RoundTrip_ChineseAndEnglish()
        {
            string original = "Hello, 你好世界!";
            string encoded = PduHelper.EncodeUcs2(original);
            string decoded = PduHelper.DecodeUcs2(encoded);
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void EncodeUcs2_EmptyString_ReturnsEmpty()
        {
            string result = PduHelper.EncodeUcs2(string.Empty);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void DecodeUcs2_EmptyString_ReturnsEmpty()
        {
            string result = PduHelper.DecodeUcs2(string.Empty);
            Assert.Equal(string.Empty, result);
        }

        #endregion

        #region 编码检测测试

        [Fact]
        public void RequiresUcs2Encoding_EnglishOnly_ReturnsFalse()
        {
            Assert.False(PduHelper.RequiresUcs2Encoding("Hello World"));
        }

        [Fact]
        public void RequiresUcs2Encoding_ChineseCharacters_ReturnsTrue()
        {
            Assert.True(PduHelper.RequiresUcs2Encoding("你好"));
        }

        [Fact]
        public void RequiresUcs2Encoding_MixedContent_ReturnsTrue()
        {
            Assert.True(PduHelper.RequiresUcs2Encoding("Hello 世界"));
        }

        [Fact]
        public void RequiresUcs2Encoding_Null_ReturnsFalse()
        {
            Assert.False(PduHelper.RequiresUcs2Encoding(null!));
        }

        [Fact]
        public void RequiresUcs2Encoding_EmptyString_ReturnsFalse()
        {
            Assert.False(PduHelper.RequiresUcs2Encoding(string.Empty));
        }

        [Fact]
        public void RequiresUcs2Encoding_Numbers_ReturnsFalse()
        {
            Assert.False(PduHelper.RequiresUcs2Encoding("1234567890"));
        }

        #endregion

        #region GSM 7-bit字符检测测试

        [Fact]
        public void IsGsm7BitChar_AsciiLetter_ReturnsTrue()
        {
            Assert.True(PduHelper.IsGsm7BitChar('A'));
            Assert.True(PduHelper.IsGsm7BitChar('z'));
        }

        [Fact]
        public void IsGsm7BitChar_Digit_ReturnsTrue()
        {
            Assert.True(PduHelper.IsGsm7BitChar('5'));
        }

        [Fact]
        public void IsGsm7BitChar_ChineseChar_ReturnsFalse()
        {
            Assert.False(PduHelper.IsGsm7BitChar('中'));
        }

        #endregion

        #region PDU构建测试

        [Fact]
        public void BuildSmsPdu_EnglishMessage_ReturnsPduWithCorrectLength()
        {
            string phoneNumber = "13800138000";
            string message = "Hello";
            var (pdu, tpduLength) = PduHelper.BuildSmsPdu(phoneNumber, message);

            Assert.False(string.IsNullOrEmpty(pdu));
            Assert.True(tpduLength > 0);
            // PDU以00开头（无SMSC）
            Assert.StartsWith("00", pdu);
        }

        [Fact]
        public void BuildSmsPdu_ChineseMessage_UsesUcs2Encoding()
        {
            string phoneNumber = "13800138000";
            string message = "你好";
            var (pdu, tpduLength) = PduHelper.BuildSmsPdu(phoneNumber, message);

            Assert.False(string.IsNullOrEmpty(pdu));
            Assert.True(tpduLength > 0);
            // UCS2编码时DCS字节应为08
            Assert.Contains("08", pdu);
        }

        [Fact]
        public void BuildSmsPdu_InternationalNumber_EncodesCorrectly()
        {
            string phoneNumber = "+8613800138000";
            string message = "Test";
            var (pdu, tpduLength) = PduHelper.BuildSmsPdu(phoneNumber, message);

            Assert.False(string.IsNullOrEmpty(pdu));
            Assert.True(tpduLength > 0);
            // 国际号码类型应包含91
            Assert.Contains("91", pdu);
        }

        #endregion
    }
}
