using LexSMS;
using LexSMS.Core;

namespace StatusCheckExample;

class Program
{
    static async Task Main(string[] args)
    {
        // 配置串口参数 - 请根据实际情况修改串口名称
        string portName = "COM3"; // Windows: COM3, Linux: /dev/ttyUSB0
        
        Console.WriteLine("=== LexSMS 状态检查示例（含错误处理） ===");
        Console.WriteLine($"使用串口: {portName}");
        
        // 创建A76XX模块实例
        using var modem = new A76XXModem(portName, 115200);
        modem.LogOutput = (message) =>
        {
            Console.WriteLine($"[LOG] {message}");
        };
        
        try
        {
            Console.WriteLine("\n正在打开串口并初始化模块...");
            await modem.OpenAsync();
            Console.WriteLine("模块初始化成功!\n");
            
            // 获取模块信息
            Console.WriteLine("=== 模块信息 ===");
            var moduleInfo = await modem.GetModuleInfoAsync();
            if (moduleInfo.ErrorMessage != null)
            {
                Console.WriteLine($"✗ 获取模块信息失败: {moduleInfo.ErrorMessage}");
            }
            else
            {
                Console.WriteLine($"  制造商: {moduleInfo.Manufacturer ?? "未知"}");
                Console.WriteLine($"  型号: {moduleInfo.Model ?? "未知"}");
                Console.WriteLine($"  固件版本: {moduleInfo.FirmwareVersion ?? "未知"}");
                Console.WriteLine($"  IMEI: {moduleInfo.Imei ?? "未知"}");
                Console.WriteLine($"  状态: {moduleInfo.Status}");
                if (moduleInfo.BatteryPercent >= 0)
                    Console.WriteLine($"  电池电量: {moduleInfo.BatteryPercent}%");
                if (moduleInfo.VoltageMillivolts > 0)
                    Console.WriteLine($"  供电电压: {moduleInfo.VoltageMillivolts}mV");
            }
            
            // 获取SIM卡信息
            Console.WriteLine("\n=== SIM卡信息 ===");
            var simInfo = await modem.GetSimInfoAsync();
            if (simInfo.ErrorMessage != null)
            {
                Console.WriteLine($"✗ 获取SIM卡信息失败: {simInfo.ErrorMessage}");
                Console.WriteLine($"  状态: {simInfo.Status}");
            }
            else
            {
                Console.WriteLine($"  状态: {simInfo.Status}");
                if (simInfo.Status == LexSMS.Models.SimStatus.Ready)
                {
                    Console.WriteLine($"  IMSI: {simInfo.Imsi ?? "未知"}");
                    Console.WriteLine($"  ICCID: {simInfo.Iccid ?? "未知"}");
                    Console.WriteLine($"  电话号码: {simInfo.PhoneNumber ?? "未知"}");
                }
            }
            
            // 获取信号信息
            Console.WriteLine("\n=== 信号信息 ===");
            var signalInfo = await modem.GetSignalInfoAsync();
            if (signalInfo.ErrorMessage != null)
            {
                Console.WriteLine($"✗ 获取信号信息失败: {signalInfo.ErrorMessage}");
            }
            else
            {
                Console.WriteLine($"  RSSI: {signalInfo.Rssi} ({signalInfo.RssiDbm}dBm)");
                Console.WriteLine($"  误码率: {signalInfo.Ber}");
                Console.WriteLine($"  信号等级: {signalInfo.SignalLevel}");
            }
            
            // 获取网络信息
            Console.WriteLine("\n=== 网络信息 ===");
            var networkInfo = await modem.GetNetworkInfoAsync();
            if (networkInfo.ErrorMessage != null)
            {
                Console.WriteLine($"✗ 获取网络信息失败: {networkInfo.ErrorMessage}");
            }
            else
            {
                Console.WriteLine($"  CS域状态 (语音/短信): {networkInfo.CsRegistrationStatus}");
                Console.WriteLine($"  GPRS域状态 (3G数据): {networkInfo.GprsRegistrationStatus}");
                Console.WriteLine($"  EPS域状态 (4G数据): {networkInfo.EpsRegistrationStatus}");
                Console.WriteLine($"  综合注册状态: {networkInfo.RegistrationStatus}");
                Console.WriteLine($"  已注册: {(networkInfo.IsRegistered ? "是" : "否")}");
                Console.WriteLine($"  运营商: {networkInfo.OperatorName ?? "未知"}");
                Console.WriteLine($"  接入类型: {networkInfo.AccessType}");
                Console.WriteLine($"  GPRS附着状态: {networkInfo.GprsAttachStatus}");
                Console.WriteLine($"  可数据通信: {(networkInfo.IsAttached ? "是" : "否")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ 发生异常: {ex.Message}");
            Console.WriteLine($"详细信息: {ex}");
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
