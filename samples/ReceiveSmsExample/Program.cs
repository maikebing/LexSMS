using LexSMS;
using LexSMS.Models;

namespace ReceiveSmsExample;

class Program
{
    static async Task Main(string[] args)
    {
        string portName = "COM3";
        
        Console.WriteLine("=== LexSMS 接收短信示例 ===");
        Console.WriteLine($"使用串口: {portName}\n");
        
        using var modem = new A76XXModem(portName, 115200);
        modem.LogOutput = (message) => Console.WriteLine($"[LOG] {message}");
        
        // 订阅短信接收事件
        modem.SmsReceived += OnSmsReceived;
        
        try
        {
            Console.WriteLine("正在初始化模块...");
            await modem.OpenAsync();
            Console.WriteLine("✓ 模块初始化成功\n");
            
            Console.WriteLine("=== 短信接收配置 ===");
            Console.WriteLine("配置: AT+CNMI=2,2,0,0,0");
            Console.WriteLine("  - 新短信直接推送到 TE");
            Console.WriteLine("  - 不存储到 SIM 卡");
            Console.WriteLine("  - 节省 SIM 卡存储空间");
            Console.WriteLine("\n等待接收短信...");
            Console.WriteLine("(按 Ctrl+C 退出)\n");
            
            // 保持运行，等待短信
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ 错误: {ex.Message}");
        }
        finally
        {
            modem.Close();
        }
    }
    
    private static void OnSmsReceived(object? sender, LexSMS.Events.SmsReceivedEventArgs e)
    {
        Console.WriteLine("\n=== 收到新短信 ===");
        
        if (e.Message != null)
        {
            // 直接推送的短信，已包含完整内容
            Console.WriteLine($"发件人: {e.Message.PhoneNumber}");
            Console.WriteLine($"时间: {e.Message.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"内容: {e.Message.Content}");
            Console.WriteLine($"存储索引: {e.Index} (直接推送，未存储)");
        }
        else
        {
            // 存储到 SIM 卡的短信，需要手动读取
            Console.WriteLine($"短信已存储到索引: {e.Index}");
            Console.WriteLine("需要调用 ReadSmsAsync({0}) 读取内容", e.Index);
        }
        
        Console.WriteLine("==================\n");
    }
}
