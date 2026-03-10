# LexSMS 错误处理改进

## 概述
此更新为 LexSMS 库添加了完善的错误处理和错误消息显示功能。当 AT 命令返回错误时（如 `+CME ERROR: SIM not inserted`），现在可以通过各个信息对象的 `ErrorMessage` 属性获取详细的错误信息。

## 主要改进

### 1. AtResponse 增强
- 新增 `ErrorMessage` 属性，用于存储错误详细信息
- 修改 `FirstLine` 属性，跳过 AT 命令回显行（以 "AT" 开头的行）
- 修复了获取制造商、型号等信息时返回命令回显的问题

**示例：**
```csharp
var response = await channel.SendCommandAsync("AT+CPIN?");
if (response.IsError)
{
    Console.WriteLine($"错误: {response.ErrorMessage}");
    // 输出: 错误: +CME ERROR: SIM not inserted
}
```

### 2. 模型类增强
为以下模型类添加了 `ErrorMessage` 属性：
- `ModuleInfo` - 模块信息
- `SimInfo` - SIM卡信息
- `SignalInfo` - 信号信息
- `NetworkInfo` - 网络信息

### 3. StatusManager 改进
所有状态查询方法现在都能捕获并传递错误信息：
- `GetModuleInfoAsync()` - 任何命令失败时设置 `ModuleStatus.Error` 并填充错误消息
- `GetSimInfoAsync()` - SIM卡错误时（如未插入）填充错误消息
- `GetSignalInfoAsync()` - 信号查询失败时填充错误消息
- `GetNetworkInfoAsync()` - 网络查询失败时填充错误消息

### 4. A76XXModem 初始化改进
`OpenAsync()` 方法现在会：
- 检查并显示所有 AT 命令的错误消息
- 在网络注册失败时提供详细的错误信息（包括底层 AT 命令错误）
- 更好地处理 SIM 卡未插入、网络注册失败等场景

**改进示例：**
- **之前**：`ModemException: "无法注册到网络，请检查SIM卡和信号强度"`
- **现在**：`ModemException: "无法注册到网络，请检查SIM卡和信号强度。详细错误: +CME ERROR: SIM not inserted"`

## 使用示例

### 检查 SIM 卡状态
```csharp
var simInfo = await modem.GetSimInfoAsync();
if (simInfo.ErrorMessage != null)
{
    Console.WriteLine($"SIM卡错误: {simInfo.ErrorMessage}");
    // 可能的输出: SIM卡错误: +CME ERROR: SIM not inserted
}
else if (simInfo.Status == SimStatus.Ready)
{
    Console.WriteLine($"IMSI: {simInfo.Imsi}");
    Console.WriteLine($"ICCID: {simInfo.Iccid}");
}
```

### 检查模块信息
```csharp
var moduleInfo = await modem.GetModuleInfoAsync();
if (moduleInfo.ErrorMessage != null)
{
    Console.WriteLine($"模块错误: {moduleInfo.ErrorMessage}");
    Console.WriteLine($"状态: {moduleInfo.Status}"); // 将是 ModuleStatus.Error
}
else
{
    Console.WriteLine($"制造商: {moduleInfo.Manufacturer}");
    Console.WriteLine($"型号: {moduleInfo.Model}");
}
```

### 检查信号信息
```csharp
var signalInfo = await modem.GetSignalInfoAsync();
if (signalInfo.ErrorMessage != null)
{
    Console.WriteLine($"信号查询错误: {signalInfo.ErrorMessage}");
}
else
{
    Console.WriteLine($"信号强度: {signalInfo.SignalLevel}");
}
```

### 检查网络信息
```csharp
var networkInfo = await modem.GetNetworkInfoAsync();
if (networkInfo.ErrorMessage != null)
{
    Console.WriteLine($"网络查询错误: {networkInfo.ErrorMessage}");
}
else
{
    Console.WriteLine($"注册状态: {networkInfo.RegistrationStatus}");
    Console.WriteLine($"运营商: {networkInfo.OperatorName}");
    Console.WriteLine($"GPRS附着: {networkInfo.GprsAttachStatus}");
    Console.WriteLine($"可数据通信: {networkInfo.IsAttached}");
}
```

### GPRS/PS 域附着管理
GPRS 附着状态决定了模块是否可以进行数据通信（HTTP、MQTT、TCP/IP 等）。

```csharp
// 查询 GPRS 附着状态
var status = await modem.GetGprsAttachStatusAsync();
Console.WriteLine($"GPRS状态: {status}"); // Attached, Detached, 或 Unknown

// 附着到 PS 域（启用数据通信）
bool success = await modem.SetGprsAttachAsync(true);
if (success)
{
    Console.WriteLine("✓ 已附着到 PS 域，可以进行数据通信");
}

// 从 PS 域分离（禁用数据通信，节省电量）
success = await modem.SetGprsAttachAsync(false);
```

**网络域说明：**
- **CS 域（Circuit Switched）**：用于语音通话和短信
- **PS 域（Packet Switched）**：用于数据通信（HTTP、MQTT 等）
- 模块可以同时注册到 CS 域和 PS 域
- 如果只需要语音和短信，可以不附着 PS 域以节省电量

## 常见错误消息

| 错误消息 | 说明 | 解决方法 |
|---------|------|---------|
| `+CME ERROR: SIM not inserted` | SIM卡未插入 | 检查 SIM 卡是否正确插入模块 |
| `+CME ERROR: SIM PIN required` | 需要输入PIN码 | 输入 SIM 卡 PIN 码或禁用 PIN 码 |
| `+CME ERROR: SIM busy` | SIM卡忙 | 等待一段时间后重试 |
| `+CME ERROR: SIM failure` | SIM卡故障 | 检查 SIM 卡是否损坏，尝试更换 SIM 卡 |
| `+CME ERROR: network timeout` | 网络超时 | 检查天线连接和信号强度 |
| `+CME ERROR: not registered` | 未注册到网络 | 检查 SIM 卡是否激活，信号是否正常 |
| `+CMS ERROR: 500` | 未知短信错误 | 检查短信中心号码配置 |
| `ERROR` | 通用错误 | 检查 AT 命令格式或模块状态 |

## 初始化过程中的错误处理

在调用 `OpenAsync()` 时，如果遇到错误，异常消息会包含详细的底层错误信息：

```csharp
try
{
    await modem.OpenAsync();
}
catch (ModemException ex)
{
    // ex.Message 现在会包含详细的错误信息
    // 例如: "无法注册到网络，请检查SIM卡和信号强度。详细错误: +CME ERROR: SIM not inserted"
    Console.WriteLine($"初始化失败: {ex.Message}");
}
```

### 初始化日志
启用详细日志后，可以看到每个步骤的详细信息：

```csharp
modem.LogOutput = (message) => Console.WriteLine($"[LOG] {message}");
modem.EnableVerboseLogging = true;

await modem.OpenAsync();
```

日志示例：
```
[LOG] 正在打开串口连接...
[LOG] 串口连接成功，等待模块就绪...
[LOG] 正在获取模块信息...
[LOG] 模块制造商: SIMCOM INCORPORATED
[LOG] 正在获取SIM卡信息...
[ERROR] 获取SIM卡信息失败: +CME ERROR: SIM not inserted
[LOG] SIM卡状态: Absent
```

## 完整示例

请参考 `samples/StatusCheckExample/Program.cs` 获取完整的错误处理示例。

## 技术细节

### AT 命令回显问题
某些模块会回显 AT 命令，导致响应格式如下：
```
AT+CGMI          <- 命令回显
INCORPORATED     <- 实际数据
OK
```

现在 `AtResponse.FirstLine` 会自动跳过以 "AT" 开头的行，直接返回实际数据。

### 错误消息提取
在 `AtChannel.BuildResponse()` 方法中，当检测到错误行时会自动提取错误消息：
- `ERROR` -> `"ERROR"`
- `+CME ERROR: 10` -> `"+CME ERROR: 10"`
- `+CMS ERROR: 500` -> `"+CMS ERROR: 500"`
