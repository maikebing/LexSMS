using LexSMS;
using LexSMS.Core;
using LexSMS.Exceptions;

namespace DiagnosticExample;

class Program
{
    static async Task Main(string[] args)
    {
        // 配置串口参数 - 请根据实际情况修改串口名称
        string portName = "COM3"; // Windows: COM3, Linux: /dev/ttyUSB0
        
        Console.WriteLine("=== LexSMS 诊断工具 ===");
        Console.WriteLine($"使用串口: {portName}");
        Console.WriteLine();
        
        // 创建A76XX模块实例并启用详细日志
        using var modem = new A76XXModem(portName, 115200);
        modem.LogOutput = (message) => Console.WriteLine(message);
        modem.EnableVerboseLogging = true;
        
        try
        {
            Console.WriteLine("开始初始化模块...\n");
            await modem.OpenAsync();
            Console.WriteLine("\n✓ 模块初始化成功!");
        }
        catch (ModemException ex)
        {
            Console.WriteLine("\n✗ 模块初始化失败");
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("可能的原因：");
            
            if (ex.Message.Contains("SIM not inserted") || ex.Message.Contains("SIM"))
            {
                Console.WriteLine("  - SIM卡未插入或未被识别");
                Console.WriteLine("  - 请检查 SIM 卡是否正确插入模块");
            }
            else if (ex.Message.Contains("网络") || ex.Message.Contains("注册"))
            {
                Console.WriteLine("  - 无法注册到移动网络");
                Console.WriteLine("  - 请检查：");
                Console.WriteLine("    1. SIM 卡是否激活");
                Console.WriteLine("    2. 天线是否正确连接");
                Console.WriteLine("    3. 当前位置的信号强度");
                Console.WriteLine("    4. SIM 卡是否欠费");
            }
            else if (ex.Message.Contains("串口") || ex.Message.Contains("COM"))
            {
                Console.WriteLine("  - 串口通信问题");
                Console.WriteLine("  - 请检查串口名称是否正确");
                Console.WriteLine("  - 请检查串口是否被其他程序占用");
            }
            
            Console.WriteLine();
            Console.WriteLine("详细信息:");
            Console.WriteLine(ex.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n✗ 发生未预期的错误");
            Console.WriteLine($"错误类型: {ex.GetType().Name}");
            Console.WriteLine($"错误消息: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("详细信息:");
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            modem.Close();
        }
        
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
}
