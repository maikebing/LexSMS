# LexSMS 日志功能使用指南

## 概述

LexSMS 库提供了灵活的日志输出功能，允许您自定义日志的输出目标和格式。通过设置 `LogOutput` 委托，您可以将日志输出到控制台、文件或任何自定义目标。

## 功能特性

- **灵活的日志输出**：通过委托自定义日志输出目标
- **日志级别**：支持 DEBUG、INFO、WARN、ERROR 四个日志级别
- **详细日志开关**：可通过 `EnableVerboseLogging` 控制是否输出 DEBUG 级别日志
- **自动日志**：`OpenAsync()` 方法会自动输出详细的初始化过程日志

## 基本用法

### 1. 输出到控制台

```csharp
using var modem = new A76XXModem("COM3", 115200);

// 设置日志输出到控制台
modem.LogOutput = (message) =>
{
    Console.WriteLine(message);
};

await modem.OpenAsync();
```

### 2. 输出到文件

```csharp
var logFile = "modem.log";

modem.LogOutput = (message) =>
{
    File.AppendAllText(logFile, message + Environment.NewLine);
};
```

### 3. 同时输出到控制台和文件

```csharp
var logFile = "modem.log";

modem.LogOutput = (message) =>
{
    File.AppendAllText(logFile, message + Environment.NewLine);
    Console.WriteLine(message);
};
```

### 4. 带颜色的控制台输出

```csharp
modem.LogOutput = (message) =>
{
    if (message.Contains("[ERROR]"))
    {
        Console.ForegroundColor = ConsoleColor.Red;
    }
    else if (message.Contains("[WARN]"))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
    }
    else if (message.Contains("[DEBUG]"))
    {
        Console.ForegroundColor = ConsoleColor.Gray;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.White;
    }
    
    Console.WriteLine(message);
    Console.ResetColor();
};
```

## 日志级别控制

### 启用/禁用详细日志

```csharp
// 启用详细日志（包括 DEBUG 级别）
modem.EnableVerboseLogging = true;

// 禁用详细日志（仅显示 INFO、WARN、ERROR 级别）
modem.EnableVerboseLogging = false;
```

## 日志格式

日志输出格式为：
```
[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] message
```

示例：
```
[2024-01-15 10:30:45.123] [INFO ] 正在打开串口连接...
[2024-01-15 10:30:45.234] [INFO ] 串口连接成功，等待模块就绪...
[2024-01-15 10:30:45.456] [DEBUG] 正在测试模块响应...
[2024-01-15 10:30:45.567] [DEBUG] 模块响应正常
[2024-01-15 10:30:46.789] [INFO ] 模块制造商: SIMCOM
[2024-01-15 10:30:46.890] [INFO ] 模块型号: A7670C
[2024-01-15 10:30:47.001] [WARN ] SIM卡未就绪，当前状态: PinRequired
[2024-01-15 10:30:48.123] [ERROR] 网络注册被拒绝
```

## OpenAsync 自动日志内容

调用 `OpenAsync()` 时，会自动输出以下信息：

1. **串口连接状态**
2. **模块响应测试**
3. **模块信息**
   - 制造商
   - 型号
   - 固件版本
   - IMEI

4. **SIM卡信息**
   - SIM卡状态
   - IMSI
   - ICCID
   - 电话号码

5. **网络注册过程**
   - 注册状态（支持最多20次重试）
   - 运营商名称
   - 网络类型

6. **信号强度**
   - 信号强度（dBm）
   - RSSI值
   - 信号等级
   - 误码率

7. **初始化状态**
   - 通话管理器初始化
   - 短信管理器初始化
   - 最终就绪状态

## 高级用法

### 集成到日志框架

您可以将 LexSMS 日志集成到现有的日志框架中，例如 Serilog、NLog 等：

```csharp
using Serilog;

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/modem-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

// 将 LexSMS 日志桥接到 Serilog
modem.LogOutput = (message) =>
{
    // 解析日志级别
    if (message.Contains("[ERROR]"))
        Log.Error(message);
    else if (message.Contains("[WARN]"))
        Log.Warning(message);
    else if (message.Contains("[DEBUG]"))
        Log.Debug(message);
    else
        Log.Information(message);
};
```

### 自定义日志过滤

```csharp
modem.LogOutput = (message) =>
{
    // 只输出错误和警告
    if (message.Contains("[ERROR]") || message.Contains("[WARN]"))
    {
        Console.WriteLine(message);
    }
};
```

## 注意事项

1. 日志输出是同步操作，请确保日志处理不会阻塞主线程
2. 写入文件时注意文件锁和并发访问问题
3. 大量日志可能影响性能，生产环境建议禁用详细日志
4. 日志中可能包含敏感信息（如电话号码、IMEI等），请妥善保管日志文件

## 示例代码

完整示例请参考：
- `samples/ModemWithLoggingExample/Program.cs` - 完整的日志使用示例
- `samples/SendSmsExample/Program.cs` - 基础使用示例
