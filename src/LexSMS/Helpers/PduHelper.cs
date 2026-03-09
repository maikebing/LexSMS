using System;
using System.Text;

namespace LexSMS.Helpers
{
    /// <summary>
    /// PDU短信编解码工具类
    /// 支持GSM 7-bit编码（英文）和UCS2编码（中文/Unicode）
    /// </summary>
    public static class PduHelper
    {
        /// <summary>
        /// 将字符串编码为UCS2十六进制字符串（用于发送含中文的短信）
        /// </summary>
        public static string EncodeUcs2(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            byte[] bytes = Encoding.BigEndianUnicode.GetBytes(text);
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// 将UCS2十六进制字符串解码为普通字符串
        /// </summary>
        public static string DecodeUcs2(string hexString)
        {
            if (string.IsNullOrEmpty(hexString)) return string.Empty;
            if (hexString.Length % 4 != 0)
                hexString = hexString.PadLeft(hexString.Length + (4 - hexString.Length % 4), '0');

            var sb = new StringBuilder();
            for (int i = 0; i < hexString.Length; i += 4)
            {
                string hexChar = hexString.Substring(i, 4);
                int charCode = Convert.ToInt32(hexChar, 16);
                sb.Append((char)charCode);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 判断字符串是否包含非GSM 7-bit字符（需要UCS2编码）
        /// </summary>
        public static bool RequiresUcs2Encoding(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (!IsGsm7BitChar(c)) return true;
            }
            return false;
        }

        /// <summary>
        /// 判断字符是否属于GSM 7-bit基本字符集
        /// </summary>
        public static bool IsGsm7BitChar(char c)
        {
            // GSM 7-bit基本字符集
            const string gsm7BitChars =
                "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞ\x1bÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?" +
                "¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ`¿abcdefghijklmnopqrstuvwxyzäöñüà";
            return gsm7BitChars.IndexOf(c) >= 0;
        }

        /// <summary>
        /// 构建完整的PDU字符串（用于AT+CMGS PDU模式）
        /// </summary>
        /// <param name="phoneNumber">目标电话号码</param>
        /// <param name="message">消息内容</param>
        /// <returns>PDU字符串和TPDU长度</returns>
        public static (string Pdu, int TpduLength) BuildSmsPdu(string phoneNumber, string message)
        {
            bool useUcs2 = RequiresUcs2Encoding(message);

            // SMSC信息（0x00 = 使用SIM卡默认SMSC）
            string smscInfo = "00";

            // PDU-TYPE: SMS-SUBMIT, 无请求确认，无效期不存在
            string pduType = "11";

            // Message Reference
            string msgRef = "00";

            // 目标地址
            string destAddr = EncodePhoneNumber(phoneNumber);

            // 协议标识符 (TP-PID)
            string pid = "00";

            // 数据编码方案 (TP-DCS)
            string dcs = useUcs2 ? "08" : "00";

            // 有效期 (TP-VP) - 167 = 24小时
            string vp = "A7";

            // 用户数据
            string userData;
            int udLength;
            if (useUcs2)
            {
                string ucs2Hex = EncodeUcs2(message);
                udLength = ucs2Hex.Length / 2; // 字节数
                userData = ucs2Hex;
            }
            else
            {
                // GSM 7-bit编码
                byte[] encoded = EncodeGsm7Bit(message);
                udLength = message.Length;
                userData = BitConverter.ToString(encoded).Replace("-", "");
            }

            string udLengthHex = udLength.ToString("X2");

            // 组合TPDU（不含SMSC信息部分）
            string tpdu = pduType + msgRef + destAddr + pid + dcs + vp + udLengthHex + userData;

            // 完整PDU
            string pdu = smscInfo + tpdu;

            // TPDU长度（字节数，不含SMSC）
            int tpduLength = tpdu.Length / 2;

            return (pdu, tpduLength);
        }

        /// <summary>
        /// 解码PDU格式的短信
        /// </summary>
        public static (string PhoneNumber, string Message, DateTime? Timestamp) DecodePdu(string pduHex)
        {
            int pos = 0;

            // SMSC信息长度
            int smscLen = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
            pos += 2 + smscLen * 2;

            // PDU类型
            int pduTypeVal = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
            pos += 2;
            bool hasUdhi = (pduTypeVal & 0x40) != 0;
            bool hasMr = (pduTypeVal & 0x02) == 0x02;
            bool hasVp = false;
            int vpFormat = (pduTypeVal >> 3) & 0x03;
            if (vpFormat == 2) hasVp = true;

            // 发件人地址长度
            int oadLen = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
            pos += 2;
            int oadType = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
            pos += 2;
            int oadBytes = (oadLen + 1) / 2;
            string oadHex = pduHex.Substring(pos, oadBytes * 2);
            string phoneNumber = DecodePhoneNumber(oadHex, oadType, oadLen);
            pos += oadBytes * 2;

            // PID
            pos += 2;

            // DCS
            int dcsVal = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
            pos += 2;
            bool isUcs2 = (dcsVal & 0x0C) == 0x08;
            bool is8Bit = (dcsVal & 0x0C) == 0x04;

            // 时间戳（SCTS）
            DateTime? timestamp = null;
            if (pduHex.Length > pos + 14)
            {
                string scts = pduHex.Substring(pos, 14);
                timestamp = DecodeScts(scts);
                pos += 14;
            }

            // 有效期（如果有）
            if (hasVp) pos += 2;

            // 用户数据长度
            int udLen = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
            pos += 2;

            // UDHI跳过头信息
            int udStart = 0;
            if (hasUdhi && pos < pduHex.Length)
            {
                int udhLen = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
                udStart = (udhLen + 1) * 2;
            }

            string userDataHex = pduHex.Substring(pos);
            string message;
            if (isUcs2)
            {
                message = DecodeUcs2(userDataHex.Substring(udStart));
            }
            else if (is8Bit)
            {
                message = Encoding.UTF8.GetString(HexStringToBytes(userDataHex));
            }
            else
            {
                message = DecodeGsm7Bit(HexStringToBytes(userDataHex), udLen);
            }

            return (phoneNumber, message, timestamp);
        }

        private static string EncodePhoneNumber(string phoneNumber)
        {
            // 去掉+号
            string num = phoneNumber.TrimStart('+');
            bool isInternational = phoneNumber.StartsWith("+");

            // 地址类型：145=国际号码(含+), 129=普通
            string addrType = isInternational ? "91" : "81";

            // 号码长度（实际数字位数）
            string addrLen = num.Length.ToString("X2");

            // 半字节交换
            if (num.Length % 2 != 0) num += "F";
            var sb = new StringBuilder();
            for (int i = 0; i < num.Length; i += 2)
            {
                sb.Append(num[i + 1]);
                sb.Append(num[i]);
            }

            return addrLen + addrType + sb.ToString();
        }

        private static string DecodePhoneNumber(string hexStr, int addrType, int len)
        {
            var sb = new StringBuilder();
            bool isInternational = (addrType & 0x70) == 0x10;

            for (int i = 0; i < hexStr.Length - 1; i += 2)
            {
                char d1 = hexStr[i + 1];
                char d2 = hexStr[i];
                if (d1 != 'F' && d1 != 'f') sb.Append(d1);
                if (d2 != 'F' && d2 != 'f') sb.Append(d2);
            }

            string result = sb.ToString();
            if (isInternational) result = "+" + result;
            return result.Length > len ? result.Substring(0, len) : result;
        }

        private static DateTime? DecodeScts(string scts)
        {
            try
            {
                int year = SwapNibbles(scts.Substring(0, 2));
                int month = SwapNibbles(scts.Substring(2, 2));
                int day = SwapNibbles(scts.Substring(4, 2));
                int hour = SwapNibbles(scts.Substring(6, 2));
                int minute = SwapNibbles(scts.Substring(8, 2));
                int second = SwapNibbles(scts.Substring(10, 2));
                int tzRaw = SwapNibbles(scts.Substring(12, 2));
                int tzMinutes = (tzRaw & 0x7F) * 15;
                bool tzNeg = (tzRaw & 0x80) != 0;
                if (tzNeg) tzMinutes = -tzMinutes;

                year += year < 70 ? 2000 : 1900;
                var dt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
                dt = dt.AddMinutes(-tzMinutes);
                return dt;
            }
            catch
            {
                return null;
            }
        }

        private static int SwapNibbles(string twoHexChars)
        {
            return int.Parse($"{twoHexChars[1]}{twoHexChars[0]}");
        }

        private static byte[] EncodeGsm7Bit(string text)
        {
            // 简化实现：仅支持ASCII范围的GSM 7-bit编码
            int byteCount = (text.Length * 7 + 7) / 8;
            byte[] result = new byte[byteCount];
            int bitPos = 0;
            foreach (char c in text)
            {
                byte b = (byte)(c & 0x7F);
                int byteIdx = bitPos / 8;
                int bitIdx = bitPos % 8;
                result[byteIdx] |= (byte)(b << bitIdx);
                if (bitIdx > 1 && byteIdx + 1 < result.Length)
                {
                    result[byteIdx + 1] |= (byte)(b >> (8 - bitIdx));
                }
                bitPos += 7;
            }
            return result;
        }

        private static string DecodeGsm7Bit(byte[] data, int charCount)
        {
            var sb = new StringBuilder();
            int bitPos = 0;
            for (int i = 0; i < charCount; i++)
            {
                int byteIdx = bitPos / 8;
                int bitIdx = bitPos % 8;
                int c = (data[byteIdx] >> bitIdx) & 0x7F;
                if (bitIdx > 1 && byteIdx + 1 < data.Length)
                {
                    c |= (data[byteIdx + 1] << (8 - bitIdx)) & 0x7F;
                }
                sb.Append((char)c);
                bitPos += 7;
            }
            return sb.ToString();
        }

        private static byte[] HexStringToBytes(string hex)
        {
            if (hex.Length % 2 != 0) hex = "0" + hex;
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return result;
        }
    }
}
