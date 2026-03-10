using System;
using LexSMS.Helpers;

namespace LexSMS.Tests
{
    /// <summary>
    /// PDU 和 UDH 测试工具
    /// 用于诊断长短信合并问题
    /// </summary>
    class PduAnalyzer
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== PDU 和 UDH 分析工具 ===\n");
            Console.WriteLine("请粘贴脱敏后的 PDU 数据（或输入 'quit' 退出）：\n");

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLower() == "quit")
                    break;

                AnalyzePdu(input.Trim());
                Console.WriteLine();
            }
        }

        static void AnalyzePdu(string pduHex)
        {
            try
            {
                Console.WriteLine($"PDU 长度: {pduHex.Length} 字符 ({pduHex.Length / 2} 字节)");
                Console.WriteLine($"PDU 数据: {pduHex}\n");

                // 1. 提取 UDH 信息
                var udhInfo = PduHelper.ExtractUdhInfo(pduHex);
                if (udhInfo != null)
                {
                    Console.WriteLine("✓ 检测到长短信 UDH:");
                    Console.WriteLine($"  参考号: {udhInfo.ReferenceNumber}");
                    Console.WriteLine($"  总段数: {udhInfo.TotalParts}");
                    Console.WriteLine($"  当前段: {udhInfo.PartNumber}");
                }
                else
                {
                    Console.WriteLine("✗ 未检测到 UDH（普通短信）");
                }

                Console.WriteLine();

                // 2. 解析 PDU 内容
                try
                {
                    var (phoneNumber, message, timestamp) = PduHelper.DecodePdu(pduHex);
                    Console.WriteLine("✓ PDU 解析成功:");
                    Console.WriteLine($"  发送号码: {phoneNumber}");
                    Console.WriteLine($"  时间戳: {timestamp}");
                    Console.WriteLine($"  内容长度: {message?.Length ?? 0} 字符");
                    Console.WriteLine("  内容: [已解析，建议仅在本地查看，不要提交到仓库]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ PDU 解析失败: {ex.Message}");
                }

                Console.WriteLine();

                // 3. 显示 PDU 结构
                ShowPduStructure(pduHex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"分析失败: {ex.Message}");
            }
        }

        static void ShowPduStructure(string pduHex)
        {
            try
            {
                Console.WriteLine("PDU 结构分析:");
                int pos = 0;

                // SMSC
                if (pos + 2 <= pduHex.Length)
                {
                    int smscLen = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
                    Console.WriteLine($"  SMSC 长度: {smscLen} 字节");
                    pos += 2 + smscLen * 2;
                }

                // PDU Type
                if (pos + 2 <= pduHex.Length)
                {
                    int pduType = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
                    bool hasUdhi = (pduType & 0x40) != 0;
                    Console.WriteLine($"  PDU 类型: 0x{pduType:X2} (UDHI={hasUdhi})");
                    pos += 2;
                }

                // OA Length
                if (pos + 2 <= pduHex.Length)
                {
                    int oaLen = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
                    Console.WriteLine($"  发送号码长度: {oaLen} 位");
                    pos += 2;

                    // OA Type
                    if (pos + 2 <= pduHex.Length)
                    {
                        pos += 2;
                        int oaBytes = (oaLen + 1) / 2;
                        pos += oaBytes * 2;
                    }
                }

                // PID, DCS
                if (pos + 4 <= pduHex.Length)
                {
                    Console.WriteLine($"  PID: 0x{pduHex.Substring(pos, 2)}");
                    pos += 2;
                    int dcs = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
                    bool isUcs2 = (dcs & 0x0C) == 0x08;
                    Console.WriteLine($"  DCS: 0x{dcs:X2} (编码={(isUcs2 ? "UCS2" : "GSM7")})");
                    pos += 2;
                }

                // SCTS
                if (pos + 14 <= pduHex.Length)
                {
                    Console.WriteLine($"  时间戳: {pduHex.Substring(pos, 14)}");
                    pos += 14;
                }

                // UDL
                if (pos + 2 <= pduHex.Length)
                {
                    int udl = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
                    Console.WriteLine($"  用户数据长度: {udl}");
                    pos += 2;

                    // UDH
                    if (pos + 2 <= pduHex.Length)
                    {
                        int udhLen = Convert.ToInt32(pduHex.Substring(pos, 2), 16);
                        if (udhLen > 0)
                        {
                            Console.WriteLine($"  UDH 长度: {udhLen} 字节");
                            Console.WriteLine($"  UDH 数据: {pduHex.Substring(pos, Math.Min((udhLen + 1) * 2, pduHex.Length - pos))}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  结构分析失败: {ex.Message}");
            }
        }
    }
}
