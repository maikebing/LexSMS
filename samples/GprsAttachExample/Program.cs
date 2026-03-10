using LexSMS;
using LexSMS.Models;

namespace GprsAttachExample;

class Program
{
    static async Task Main(string[] args)
    {
        string portName = "COM3"; // Windows: COM3, Linux: /dev/ttyUSB0
        
        Console.WriteLine("=== GPRS/PS 域附着状态示例 ===");
        Console.WriteLine($"使用串口: {portName}\n");
        
        using var modem = new A76XXModem(portName, 115200);
        modem.LogOutput = (message) => Console.WriteLine($"[LOG] {message}");
        
        try
        {
            Console.WriteLine("正在初始化模块...");
            await modem.OpenAsync();
            Console.WriteLine("初始化成功!\n");
            
            // 查询当前 GPRS 附着状态
            Console.WriteLine("=== 查询 GPRS 附着状态 ===");
            var status = await modem.GetGprsAttachStatusAsync();
            Console.WriteLine($"当前状态: {status}");
            
            if (status == GprsAttachStatus.Attached)
            {
                Console.WriteLine("✓ 已附着到 PS 域，可以进行数据通信（HTTP、MQTT 等）");
            }
            else if (status == GprsAttachStatus.Detached)
            {
                Console.WriteLine("✗ 未附着到 PS 域，无法进行数据通信");
                Console.WriteLine("\n尝试附着到 PS 域...");
                
                bool success = await modem.SetGprsAttachAsync(true);
                if (success)
                {
                    Console.WriteLine("✓ GPRS 附着命令发送成功");
                    
                    // 等待附着完成
                    await Task.Delay(2000);
                    
                    // 再次查询状态
                    status = await modem.GetGprsAttachStatusAsync();
                    Console.WriteLine($"新状态: {status}");
                    
                    if (status == GprsAttachStatus.Attached)
                    {
                        Console.WriteLine("✓ 成功附着到 PS 域");
                    }
                    else
                    {
                        Console.WriteLine("✗ 附着失败，请检查网络设置");
                    }
                }
                else
                {
                    Console.WriteLine("✗ GPRS 附着命令失败");
                }
            }
            else
            {
                Console.WriteLine("? 无法获取 GPRS 附着状态");
            }
            
            // 获取完整的网络信息
            Console.WriteLine("\n=== 完整网络信息 ===");
            var networkInfo = await modem.GetNetworkInfoAsync();
            if (networkInfo.ErrorMessage != null)
            {
                Console.WriteLine($"错误: {networkInfo.ErrorMessage}");
            }
            else
            {
                Console.WriteLine($"网络注册: {networkInfo.RegistrationStatus}");
                Console.WriteLine($"运营商: {networkInfo.OperatorName ?? "未知"}");
                Console.WriteLine($"网络类型: {networkInfo.AccessType}");
                Console.WriteLine($"GPRS状态: {networkInfo.GprsAttachStatus}");
                Console.WriteLine($"已注册: {(networkInfo.IsRegistered ? "是" : "否")}");
                Console.WriteLine($"已附着: {(networkInfo.IsAttached ? "是" : "否")}");
                
                if (networkInfo.IsRegistered && networkInfo.IsAttached)
                {
                    Console.WriteLine("\n✓ 网络就绪，可以使用所有功能（语音、短信、数据）");
                }
                else if (networkInfo.IsRegistered && !networkInfo.IsAttached)
                {
                    Console.WriteLine("\n⚠ 仅注册 CS 域，可以使用语音和短信，但不能使用数据功能");
                }
                else
                {
                    Console.WriteLine("\n✗ 网络未就绪");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ 错误: {ex.Message}");
            Console.WriteLine($"详细信息: {ex}");
        }
        finally
        {
            modem.Close();
        }
        
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
}
