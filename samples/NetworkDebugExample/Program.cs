using LexSMS;
using LexSMS.Core;
using LexSMS.Models;

namespace NetworkDebugExample;

class Program
{
    static async Task Main(string[] args)
    {
        string portName = "COM3";

        Console.WriteLine("=== 网络注册调试工具 ===\n");
        Console.WriteLine("此工具用于调试网络注册问题，会绕过完整初始化流程");
        Console.WriteLine("直接查询模块状态\n");

        // 使用底层 AtChannel 直接通信，避免完整初始化流程
        var config = new SerialPortConfig 
        { 
            PortName = portName, 
            BaudRate = 115200 
        };

        using var channel = new AtChannel(config);

        try
        {
            Console.WriteLine("正在打开串口...");
            channel.Open();
            await Task.Delay(1000);  // 等待模块就绪

            Console.WriteLine("串口已打开\n");
            Console.WriteLine("=== 原始 AT 命令响应 ===\n");

            // 1. 测试模块响应
            Console.WriteLine("执行: AT");
            var atResp = await channel.SendCommandAsync("AT");
            if (!atResp.IsOk)
            {
                Console.WriteLine("✗ 模块未响应，请检查串口连接");
                return;
            }
            Console.WriteLine("✓ 模块响应正常\n");

            // 2. 查询网络注册状态
            Console.WriteLine("执行: AT+CREG?");
            var cregResp = await channel.SendCommandAsync("AT+CREG?");
            Console.WriteLine($"原始响应:\n{cregResp.RawResponse}\n");
            Console.WriteLine("响应行:");
            foreach (var line in cregResp.Lines)
            {
                Console.WriteLine($"  '{line}'");
            }
            
            // 解析响应
            foreach (var line in cregResp.Lines)
            {
                if (line.StartsWith("+CREG:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = line.Substring(6).Trim();
                    Console.WriteLine($"\n数据部分: '{data}'");
                    
                    string[] parts = data.Split(',');
                    Console.WriteLine($"分割后: [{string.Join("], [", parts)}]");
                    Console.WriteLine($"参数数量: {parts.Length}");
                    
                    if (parts.Length >= 2)
                    {
                        Console.WriteLine($"  参数0 (n - URC配置): {parts[0]}");
                        Console.WriteLine($"  参数1 (stat - 注册状态): {parts[1]}");
                        
                        if (int.TryParse(parts[1].Trim(), out int stat))
                        {
                            var status = (NetworkRegistrationStatus)stat;
                            Console.WriteLine($"\n解析结果: {status} ({stat})");
                            Console.WriteLine(GetStatusDescription(status));
                        }
                    }
                    else if (parts.Length == 1)
                    {
                        Console.WriteLine($"  参数0 (stat - 注册状态): {parts[0]}");
                        
                        if (int.TryParse(parts[0].Trim(), out int stat))
                        {
                            var status = (NetworkRegistrationStatus)stat;
                            Console.WriteLine($"\n解析结果: {status} ({stat})");
                            Console.WriteLine(GetStatusDescription(status));
                        }
                    }
                }
            }

            // 2. 查询 GPRS 注册状态
            Console.WriteLine("\n\n执行: AT+CGREG?");
            var cgregResp = await channel.SendCommandAsync("AT+CGREG?");
            Console.WriteLine($"原始响应:\n{cgregResp.RawResponse}\n");
            Console.WriteLine("响应行:");
            foreach (var line in cgregResp.Lines)
            {
                Console.WriteLine($"  '{line}'");
            }

            // 3. 查询 EPS 注册状态
            Console.WriteLine("\n\n执行: AT+CEREG?");
            var ceregResp = await channel.SendCommandAsync("AT+CEREG?");
            Console.WriteLine($"原始响应:\n{ceregResp.RawResponse}\n");
            Console.WriteLine("响应行:");
            foreach (var line in ceregResp.Lines)
            {
                Console.WriteLine($"  '{line}'");
            }

            // 4. 查询运营商
            Console.WriteLine("\n\n执行: AT+COPS?");
            var copsResp = await channel.SendCommandAsync("AT+COPS?");
            Console.WriteLine($"原始响应:\n{copsResp.RawResponse}\n");
            Console.WriteLine("响应行:");
            foreach (var line in copsResp.Lines)
            {
                Console.WriteLine($"  '{line}'");
            }

            // 5. 查询 GPRS 附着
            Console.WriteLine("\n\n执行: AT+CGATT?");
            var cgattResp = await channel.SendCommandAsync("AT+CGATT?");
            Console.WriteLine($"原始响应:\n{cgattResp.RawResponse}\n");
            Console.WriteLine("响应行:");
            foreach (var line in cgattResp.Lines)
            {
                Console.WriteLine($"  '{line}'");
            }

            // 6. 使用 StatusManager 解析
            Console.WriteLine("\n\n=== 使用 StatusManager 解析 ===");
            var statusManager = new LexSMS.Features.StatusManager(channel);
            var networkInfo = await statusManager.GetNetworkInfoAsync();

            if (networkInfo.ErrorMessage != null)
            {
                Console.WriteLine($"错误: {networkInfo.ErrorMessage}");
            }
            else
            {
                Console.WriteLine($"CS域状态 (AT+CREG): {networkInfo.CsRegistrationStatus}");
                Console.WriteLine($"GPRS域状态 (AT+CGREG): {networkInfo.GprsRegistrationStatus}");
                Console.WriteLine($"EPS域状态 (AT+CEREG): {networkInfo.EpsRegistrationStatus}");
                Console.WriteLine($"综合注册状态: {networkInfo.RegistrationStatus}");
                Console.WriteLine($"运营商: {networkInfo.OperatorName ?? "未知"}");
                Console.WriteLine($"网络类型: {networkInfo.AccessType}");
                Console.WriteLine($"GPRS附着: {networkInfo.GprsAttachStatus}");
                Console.WriteLine($"已注册: {networkInfo.IsRegistered}");
                Console.WriteLine($"已附着: {networkInfo.IsAttached}");
            }
            
            // 建议
            Console.WriteLine("\n\n=== 故障排查建议 ===");
            if (networkInfo.RegistrationStatus == NetworkRegistrationStatus.NotRegistered)
            {
                Console.WriteLine("⚠ 网络未注册 (状态 0)");
                Console.WriteLine("可能原因：");
                Console.WriteLine("  1. SIM卡未插入或未识别");
                Console.WriteLine("  2. SIM卡未激活");
                Console.WriteLine("  3. 天线未连接");
                Console.WriteLine("  4. 当前位置无信号");
                Console.WriteLine("\n建议操作：");
                Console.WriteLine("  - 检查 SIM 卡状态: await modem.GetSimInfoAsync()");
                Console.WriteLine("  - 检查信号强度: await modem.GetSignalInfoAsync()");
                Console.WriteLine("  - 等待模块自动搜索网络（可能需要1-2分钟）");
            }
            else if (networkInfo.RegistrationStatus == NetworkRegistrationStatus.Searching)
            {
                Console.WriteLine("⏳ 正在搜索网络 (状态 2)");
                Console.WriteLine("模块正在搜索可用网络，请等待...");
            }
            else if (networkInfo.RegistrationStatus == NetworkRegistrationStatus.RegistrationDenied)
            {
                Console.WriteLine("✗ 网络注册被拒绝 (状态 3)");
                Console.WriteLine("可能原因：");
                Console.WriteLine("  1. SIM卡未激活或已停机");
                Console.WriteLine("  2. 运营商拒绝注册");
                Console.WriteLine("  3. 网络设置问题");
            }
            else if (networkInfo.IsRegistered)
            {
                Console.WriteLine($"✓ 已注册到网络 (状态 {(int)networkInfo.RegistrationStatus})");
                if (networkInfo.IsAttached)
                {
                    Console.WriteLine("✓ 已附着 PS 域，可以使用所有功能");
                }
                else
                {
                    Console.WriteLine("⚠ 未附着 PS 域，无法使用数据功能");
                    Console.WriteLine("尝试执行: await modem.SetGprsAttachAsync(true)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ 错误: {ex.Message}");
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            channel.Close();
        }

        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
    
    static string GetStatusDescription(NetworkRegistrationStatus status)
    {
        return status switch
        {
            NetworkRegistrationStatus.NotRegistered => "未注册，未搜索",
            NetworkRegistrationStatus.RegisteredHome => "已注册，本地网络",
            NetworkRegistrationStatus.Searching => "未注册，正在搜索",
            NetworkRegistrationStatus.RegistrationDenied => "注册被拒绝",
            NetworkRegistrationStatus.Unknown => "未知状态",
            NetworkRegistrationStatus.RegisteredRoaming => "已注册，漫游网络",
            NetworkRegistrationStatus.RegisteredHomeSms => "已注册，本地网络（仅限SMS）",
            NetworkRegistrationStatus.RegisteredRoamingSms => "已注册，漫游网络（仅限SMS）",
            _ => "未定义状态"
        };
    }
}
