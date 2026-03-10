using LexSMS;

namespace ModemWithLoggingExample;

class Program
{
    static async Task Main(string[] args)
    {
        // 配置串口参数 - 请根据实际情况修改串口名称
        string portName = "COM3"; // Windows: COM3, Linux: /dev/ttyUSB0

        Console.WriteLine("=== LexSMS 日志输出示例 ===");
        Console.WriteLine($"使用串口: {portName}\n");

        // 创建A76XX模块实例
        using var modem = new A76XXModem(portName, 115200);

        // 示例1: 将日志输出到控制台
        Console.WriteLine("--- 示例1: 控制台日志输出 ---\n");
        modem.LogOutput = (message) =>
        {
            Console.WriteLine(message);
        };

        // 示例2: 将日志同时输出到控制台和文件
        // var logFile = "modem.log";
        // modem.LogOutput = (message) =>
        // {
        //     File.AppendAllText(logFile, message + Environment.NewLine);
        //     Console.WriteLine(message);
        // };

        // 示例3: 带颜色的控制台日志输出
        // modem.LogOutput = (message) =>
        // {
        //     if (message.Contains("[ERROR]"))
        //     {
        //         Console.ForegroundColor = ConsoleColor.Red;
        //     }
        //     else if (message.Contains("[WARN]"))
        //     {
        //         Console.ForegroundColor = ConsoleColor.Yellow;
        //     }
        //     else if (message.Contains("[DEBUG]"))
        //     {
        //         Console.ForegroundColor = ConsoleColor.Gray;
        //     }
        //     else
        //     {
        //         Console.ForegroundColor = ConsoleColor.White;
        //     }
        //     Console.WriteLine(message);
        //     Console.ResetColor();
        // };

        // 启用详细日志（包括DEBUG级别）
        modem.EnableVerboseLogging = true;

        try
        {
            Console.WriteLine("开始初始化模块...\n");

            // OpenAsync 会自动输出详细的初始化日志
            // 包括：模块信息、SIM卡信息、网络注册状态、运营商信息、信号强度等
            await modem.OpenAsync();

            Console.WriteLine("\n=== 模块初始化完成，开始测试各项功能 ===\n");

            // 获取并显示网络信息
            Console.WriteLine("\n--- 网络信息详情 ---");
            var networkInfo = await modem.GetNetworkInfoAsync();
            Console.WriteLine($"注册状态: {networkInfo.RegistrationStatus}");
            Console.WriteLine($"运营商: {networkInfo.OperatorName ?? "未知"}");
            Console.WriteLine($"网络类型: {networkInfo.AccessType}");
            Console.WriteLine($"是否已注册: {(networkInfo.IsRegistered ? "是" : "否")}");

            // 获取并显示信号强度
            Console.WriteLine("\n--- 信号强度详情 ---");
            var signalInfo = await modem.GetSignalInfoAsync();
            Console.WriteLine($"信号强度: {signalInfo.RssiDbm} dBm");
            Console.WriteLine($"RSSI值: {signalInfo.Rssi}");
            Console.WriteLine($"信号等级: {signalInfo.SignalLevel}");
            Console.WriteLine($"误码率: {signalInfo.Ber}");

            // 获取并显示模块信息
            Console.WriteLine("\n--- 模块信息详情 ---");
            var moduleInfo = await modem.GetModuleInfoAsync();
            Console.WriteLine($"制造商: {moduleInfo.Manufacturer}");
            Console.WriteLine($"型号: {moduleInfo.Model}");
            Console.WriteLine($"固件版本: {moduleInfo.FirmwareVersion}");
            Console.WriteLine($"IMEI: {moduleInfo.Imei}");

            // 获取并显示SIM卡信息
            Console.WriteLine("\n--- SIM卡信息详情 ---");
            var simInfo = await modem.GetSimInfoAsync();
            Console.WriteLine($"状态: {simInfo.Status}");
            Console.WriteLine($"IMSI: {simInfo.Imsi}");
            Console.WriteLine($"ICCID: {simInfo.Iccid}");
            Console.WriteLine($"电话号码: {simInfo.PhoneNumber ?? "未知"}");

            // 发送短信示例（可选 - 取消注释以测试）
            // Console.WriteLine("\n--- 发送短信测试 ---");
            // string phoneNumber = "13800138000";
            // string message = "这是一条测试短信";
            // Console.WriteLine($"目标号码: {phoneNumber}");
            // Console.WriteLine($"短信内容: {message}");
            // await modem.SendSmsAsync(phoneNumber, message);
            // Console.WriteLine("✓ 短信发送成功");

            Console.WriteLine("\n=== 所有测试完成 ===");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ 错误: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"内部错误: {ex.InnerException.Message}");
            }
            Console.ResetColor();

            // 输出完整的异常堆栈（用于调试）
            Console.WriteLine($"\n详细信息:\n{ex}");
        }
        finally
        {
            Console.WriteLine("\n关闭连接...");
            modem.Close();
        }

        Console.WriteLine("\n程序结束，按任意键退出...");
        Console.ReadKey();
    }
}
