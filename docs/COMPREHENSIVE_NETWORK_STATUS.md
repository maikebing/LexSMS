# 综合网络注册状态查询

## 概述

A76XX 模块支持多种网络域，每种域有各自的注册状态。为了准确判断网络状态，LexSMS 现在会查询所有三个域的注册状态并综合判断。

## 网络域类型

### 1. CS 域 (Circuit Switched Domain)
- **命令**: `AT+CREG?`
- **用途**: 语音通话和短信（2G/3G）
- **响应**: `+CREG: <n>,<stat>[,<lac>,<ci>]`

### 2. GPRS/PS 域 (Packet Switched Domain)
- **命令**: `AT+CGREG?`
- **用途**: GPRS 数据通信（2G/3G）
- **响应**: `+CGREG: <n>,<stat>[,<lac>,<ci>,<AcT>]`

### 3. EPS 域 (Evolved Packet System)
- **命令**: `AT+CEREG?`
- **用途**: LTE/4G 数据通信
- **响应**: `+CEREG: <n>,<stat>[,<tac>,<ci>,<AcT>]`

## 综合判断逻辑

### 优先级
当多个域都已注册时，按以下优先级选择：
1. **EPS 域** (4G/LTE) - 最优先
2. **GPRS 域** (3G) - 次优先
3. **CS 域** (2G) - 最低优先

### 判断规则

```csharp
综合状态 = 
    if (EPS 已注册) → 返回 EPS 状态
    else if (GPRS 已注册) → 返回 GPRS 状态
    else if (CS 已注册) → 返回 CS 状态
    else if (任意域正在搜索) → 返回 Searching
    else if (任意域被拒绝) → 返回 RegistrationDenied
    else → 返回最有价值的状态
```

## 使用示例

### 查询网络信息

```csharp
var networkInfo = await modem.GetNetworkInfoAsync();

// 查看各域的注册状态
Console.WriteLine($"CS域 (语音/短信): {networkInfo.CsRegistrationStatus}");
Console.WriteLine($"GPRS域 (3G数据): {networkInfo.GprsRegistrationStatus}");
Console.WriteLine($"EPS域 (4G数据): {networkInfo.EpsRegistrationStatus}");

// 查看综合状态
Console.WriteLine($"综合状态: {networkInfo.RegistrationStatus}");
Console.WriteLine($"已注册: {networkInfo.IsRegistered}");
```

### 示例输出

#### 场景 1: 4G 网络
```
CS域 (语音/短信): RegisteredHome
GPRS域 (3G数据): RegisteredHome
EPS域 (4G数据): RegisteredHome
综合状态: RegisteredHome (使用 EPS)
已注册: True
```

#### 场景 2: 3G 网络（无 4G）
```
CS域 (语音/短信): RegisteredHome
GPRS域 (3G数据): RegisteredHome
EPS域 (4G数据): Unknown
综合状态: RegisteredHome (使用 GPRS)
已注册: True
```

#### 场景 3: 2G 网络（仅语音/短信）
```
CS域 (语音/短信): RegisteredHome
GPRS域 (3G数据): NotRegistered
EPS域 (4G数据): Unknown
综合状态: RegisteredHome (使用 CS)
已注册: True
```

#### 场景 4: 正在搜索网络
```
CS域 (语音/短信): Searching
GPRS域 (3G数据): Searching
EPS域 (4G数据): Searching
综合状态: Searching
已注册: False
```

## NetworkInfo 属性

### 详细状态属性
```csharp
public NetworkRegistrationStatus CsRegistrationStatus { get; set; }      // CS 域状态
public NetworkRegistrationStatus GprsRegistrationStatus { get; set; }    // GPRS 域状态
public NetworkRegistrationStatus EpsRegistrationStatus { get; set; }     // EPS 域状态
```

### 综合状态属性
```csharp
public NetworkRegistrationStatus RegistrationStatus { get; }  // 只读，自动计算
public bool IsRegistered { get; }                             // 是否已注册（任意域）
```

### 其他属性
```csharp
public string? OperatorName { get; set; }                     // 运营商名称
public NetworkAccessType AccessType { get; set; }             // 网络接入类型
public GprsAttachStatus GprsAttachStatus { get; set; }        // GPRS 附着状态
public bool IsAttached { get; }                               // 是否已附着 PS 域
public string? ErrorMessage { get; set; }                     // 错误消息
```

## 网络类型判断

### CAT1 模块 (4G)
- 支持 `AT+CREG`, `AT+CGREG`, `AT+CEREG`
- 优先使用 `EpsRegistrationStatus`（4G）
- 降级到 `GprsRegistrationStatus`（3G）

### NB-IoT 模块
- 主要使用 `AT+CEREG`（EPS 域）
- 可能不支持 `AT+CGREG`

### 2G/3G 模块
- 使用 `AT+CREG` 和 `AT+CGREG`
- `AT+CEREG` 返回 Unknown

## 故障排查

### 所有域都未注册

**可能原因：**
- SIM 卡未插入或未识别
- 天线未连接
- SIM 卡未激活或欠费
- 当前位置无信号覆盖

**排查步骤：**
```csharp
// 1. 检查 SIM 卡
var simInfo = await modem.GetSimInfoAsync();
if (simInfo.Status != SimStatus.Ready)
{
    Console.WriteLine($"SIM卡问题: {simInfo.Status}");
    if (simInfo.ErrorMessage != null)
        Console.WriteLine($"错误: {simInfo.ErrorMessage}");
}

// 2. 检查信号强度
var signalInfo = await modem.GetSignalInfoAsync();
Console.WriteLine($"信号强度: {signalInfo.SignalLevel} ({signalInfo.RssiDbm}dBm)");

// 3. 等待网络搜索
await Task.Delay(30000);  // 等待 30 秒
var networkInfo = await modem.GetNetworkInfoAsync();
```

### 仅 CS 域注册，数据域未注册

这是正常现象，表示：
- 可以打电话和发短信
- 不能使用数据功能（HTTP、MQTT 等）

**解决方法：**
```csharp
// 尝试附着到 PS 域
await modem.SetGprsAttachAsync(true);
await Task.Delay(5000);

var status = await modem.GetGprsAttachStatusAsync();
if (status == GprsAttachStatus.Attached)
{
    Console.WriteLine("✓ 已附着 PS 域，可以使用数据功能");
}
```

## 调试工具

使用网络调试工具查看详细信息：

```bash
cd samples/NetworkDebugExample
dotnet run
```

输出示例：
```
=== 网络注册调试工具 ===

执行: AT+CREG?
原始响应:
+CREG: 0,1
OK

执行: AT+CGREG?
原始响应:
+CGREG: 0,1
OK

执行: AT+CEREG?
原始响应:
+CEREG: 0,1
OK

=== 使用 StatusManager 解析 ===
CS域状态 (AT+CREG): RegisteredHome
GPRS域状态 (AT+CGREG): RegisteredHome
EPS域状态 (AT+CEREG): RegisteredHome
综合注册状态: RegisteredHome
已注册: True
```

## 最佳实践

### 1. 检查是否已注册
```csharp
var networkInfo = await modem.GetNetworkInfoAsync();
if (!networkInfo.IsRegistered)
{
    Console.WriteLine("网络未注册，等待...");
    // 实现重试逻辑
}
```

### 2. 判断可用功能
```csharp
var networkInfo = await modem.GetNetworkInfoAsync();

// 检查语音通话
bool canCall = networkInfo.CsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome ||
               networkInfo.CsRegistrationStatus == NetworkRegistrationStatus.RegisteredRoaming;

// 检查数据通信
bool canData = (networkInfo.GprsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome ||
                networkInfo.GprsRegistrationStatus == NetworkRegistrationStatus.RegisteredRoaming ||
                networkInfo.EpsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome ||
                networkInfo.EpsRegistrationStatus == NetworkRegistrationStatus.RegisteredRoaming) &&
               networkInfo.IsAttached;

Console.WriteLine($"可以打电话: {canCall}");
Console.WriteLine($"可以上网: {canData}");
```

### 3. 选择合适的网络
```csharp
var networkInfo = await modem.GetNetworkInfoAsync();

if (networkInfo.EpsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome)
{
    Console.WriteLine("使用 4G 网络");
}
else if (networkInfo.GprsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome)
{
    Console.WriteLine("使用 3G 网络");
}
else if (networkInfo.CsRegistrationStatus == NetworkRegistrationStatus.RegisteredHome)
{
    Console.WriteLine("使用 2G 网络（仅语音/短信）");
}
```

## 相关文档

- `docs/NETWORK_REGISTRATION.md` - 网络注册详细说明
- `docs/ERROR_HANDLING.md` - 错误处理指南
- `samples/NetworkDebugExample/` - 网络调试工具
