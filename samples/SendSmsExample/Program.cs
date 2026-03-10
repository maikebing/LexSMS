using LexSMS;
using LexSMS.Core;

namespace SendSmsExample;

class Program
{
    static async Task Main(string[] args)
    {
        // 配置串口参数 - 请根据实际情况修改串口名称
        string portName = "COM3"; // Windows: COM3, Linux: /dev/ttyUSB0
        
        Console.WriteLine("=== LexSMS 发送短信示例 ===");
        Console.WriteLine($"使用串口: {portName}");
        
        // 创建A76XX模块实例
        using var modem = new A76XXModem(portName, 115200);
        
        try
        {
            Console.WriteLine("正在打开串口并初始化模块...");
            await modem.OpenAsync();
            Console.WriteLine("模块初始化成功!");
            
            // 目标手机号码
            string phoneNumber = "18160209520";
            string message = "在吗";
            
            Console.WriteLine($"\n准备发送短信:");
            Console.WriteLine($"  目标号码: {phoneNumber}");
            Console.WriteLine($"  短信内容: {message}");
            
            // 发送短信
            Console.WriteLine("\n正在发送短信...");
            await modem.SendSmsAsync(phoneNumber, message);
            
            Console.WriteLine("✓ 短信发送成功!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 错误: {ex.Message}");
            Console.WriteLine($"详细信息: {ex}");
            return;
        }
        finally
        {
            Console.WriteLine("\n关闭连接...");
            modem.Close();
        }
        
        Console.WriteLine("程序结束，按任意键退出...");
        Console.ReadKey();
    }
}
