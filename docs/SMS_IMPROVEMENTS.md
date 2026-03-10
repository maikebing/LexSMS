# 短信功能改进说明

## 概述

针对短信发送和接收功能进行了两项重要改进：

1. **新短信直接推送到 TE，不存储 SIM 卡** - 避免 SIM 卡存储空间浪费
2. **修复短信发送超时问题** - 解决发送成功但报告超时的问题

## 改进 1: 直接推送短信到 TE

### 问题

之前的配置 `AT+CNMI=2,1,0,0,0` 会将收到的短信存储到 SIM 卡，然后推送索引 (`+CMTI`)。

**缺点：**
- 浪费 SIM 卡存储空间（通常只能存储 20-50 条短信）
- SIM 卡满后无法接收新短信
- 需要手动调用 `ReadSmsAsync()` 读取内容
- 需要定期删除旧短信

### 解决方案

改用 `AT+CNMI=2,2,0,0,0` 配置，直接推送短信内容 (`+CMT`)，不存储到 SIM 卡。

**优点：**
- ✅ 不占用 SIM 卡存储空间
- ✅ 短信内容直接推送，无需额外查询
- ✅ 不会因为 SIM 卡满而丢失短信
- ✅ 无需管理 SIM 卡存储

### 配置参数说明

```
AT+CNMI=2,2,0,0,0
        │ │ │ │ └─ bfr=0: 缓冲区清除
        │ │ │ └─── ds=0: 状态报告不存储
        │ │ └───── bm=0: 小区广播不上报
        │ └─────── mt=2: 新短信直接上报内容 (+CMT)
        └───────── mode=2: 启用主动上报
```

### 接收流程

#### 之前（AT+CNMI=2,1）
```
收到短信 → 存储到 SIM 卡 → 推送索引 +CMTI
         ↓
    触发 SmsReceived 事件 (Index=5)
         ↓
    需要调用 ReadSmsAsync(5) 读取内容
```

#### 现在（AT+CNMI=2,2）
```
收到短信 → 直接推送内容 +CMT
         ↓
    触发 SmsReceived 事件 (Message 包含完整内容)
         ↓
    直接使用 e.Message 获取内容
```

### 代码示例

```csharp
modem.SmsReceived += (sender, e) =>
{
    if (e.Message != null)
    {
        // 直接推送的短信，已包含完整内容
        Console.WriteLine($"发件人: {e.Message.PhoneNumber}");
        Console.WriteLine($"内容: {e.Message.Content}");
        Console.WriteLine($"时间: {e.Message.Timestamp}");
    }
    else
    {
        // 存储到 SIM 卡的短信（向后兼容）
        var sms = await modem.ReadSmsAsync(e.Index);
        Console.WriteLine($"内容: {sms.Content}");
    }
};
```

## 改进 2: 修复短信发送超时问题

### 问题

之前的代码在发送 PDU + Ctrl+Z 后，会调用 `SendCommandAsync("")`，导致：

```csharp
_channel.SendRaw(pdu);
_channel.SendCtrlZ();
await Task.Delay(500);
var sendResp = await _channel.SendCommandAsync("", 30000);  // ❌ 错误！
```

**问题：**
- 发送空命令干扰了短信发送流程
- 可能导致超时但实际短信已发送成功
- 响应处理混乱

### 解决方案

重新设计发送逻辑，正确等待短信发送响应：

```csharp
// 1. 发送 AT+CMGS=n，等待 > 提示符
var promptResp = await _channel.SendCommandAsync($"AT+CMGS={tpduLength}", 10000);

// 2. 发送 PDU + Ctrl+Z
_channel.SendRaw(pdu);
_channel.SendCtrlZ();

// 3. 通过 URC 机制等待发送结果
//    响应格式：+CMGS: <mr>\r\nOK
await SendPduWithCtrlZAsync(pdu);
```

### 发送流程

```
发送 AT+CMGS=23
    ↓
收到 > 提示符
    ↓
发送 PDU + Ctrl+Z
    ↓
等待响应（通过 URC）:
  - +CMGS: 123  (消息引用号)
  - OK
    ↓
发送成功！
```

### 超时处理

现在发送超时会有更明确的提示：

```
TimeoutException: 短信发送超时（60秒）。注意：短信可能已发送成功但响应超时。
```

### 超时时间

- **之前**: 30 秒
- **现在**: 60 秒（更合理的超时时间）

### 错误处理

现在可以准确捕获各种错误：

```csharp
try
{
    await modem.SendSmsAsync(phoneNumber, message);
    Console.WriteLine("✓ 短信发送成功");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"⚠ 超时: {ex.Message}");
    Console.WriteLine("短信可能已发送，请检查对方是否收到");
}
catch (AtCommandErrorException ex)
{
    Console.WriteLine($"✗ 发送失败: {ex.Message}");
    // 例如: +CMS ERROR: 500
}
```

## 使用示例

### 接收短信

参考 `samples/ReceiveSmsExample/Program.cs`：

```csharp
using var modem = new A76XXModem(portName, 115200);

modem.SmsReceived += (sender, e) =>
{
    if (e.Message != null)
    {
        Console.WriteLine($"收到短信: {e.Message.Content}");
        Console.WriteLine($"发件人: {e.Message.PhoneNumber}");
    }
};

await modem.OpenAsync();
await Task.Delay(Timeout.Infinite);  // 保持运行
```

### 发送短信

参考 `samples/SendSmsExample/Program.cs`：

```csharp
using var modem = new A76XXModem(portName, 115200);

try
{
    await modem.OpenAsync();
    await modem.SendSmsAsync("18160209520", "测试短信");
    Console.WriteLine("✓ 发送成功");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"⚠ {ex.Message}");
}
```

## 向后兼容性

### SmsReceivedEventArgs

事件参数同时支持两种模式：

```csharp
public class SmsReceivedEventArgs : EventArgs
{
    public int Index { get; }           // 存储索引（+CMTI 模式）
    public SmsMessage? Message { get; }  // 完整消息（+CMT 模式）
}
```

### 检测推送模式

```csharp
modem.SmsReceived += (sender, e) =>
{
    if (e.Message != null)
    {
        // 直接推送模式 (+CMT)
        Console.WriteLine("直接推送: " + e.Message.Content);
    }
    else if (e.Index > 0)
    {
        // 存储模式 (+CMTI)
        var sms = await modem.ReadSmsAsync(e.Index);
        Console.WriteLine("已存储: " + sms.Content);
    }
};
```

## 技术细节

### +CMT 消息格式

```
+CMT: [<alpha>],<length>
<pdu>
```

例如：
```
+CMT: ,24
07912345678901F0040B912143658709F100008210211053302304C8329BFD06
```

### 多行 URC 处理

由于 `+CMT` 是两行消息，实现了状态机来处理：

```csharp
private string? _pendingCmtHeader;  // 缓存 +CMT 头部

private void OnUnsolicitedReceived(object? sender, string urc)
{
    // 如果上一次收到了 +CMT 头部，这次应该是 PDU 数据
    if (_pendingCmtHeader != null)
    {
        var (phoneNumber, message, timestamp) = PduHelper.DecodePdu(urc);
        // 触发事件...
        _pendingCmtHeader = null;
        return;
    }

    if (urc.StartsWith("+CMT:"))
    {
        _pendingCmtHeader = urc;  // 保存头部，等待下一行
    }
}
```

## 最佳实践

### 1. 接收短信时不要阻塞

```csharp
modem.SmsReceived += async (sender, e) =>
{
    // ✅ 使用 Task.Run 避免阻塞
    await Task.Run(async () =>
    {
        await ProcessSmsAsync(e.Message);
    });
};
```

### 2. 处理异常

```csharp
modem.SmsReceived += (sender, e) =>
{
    try
    {
        ProcessSms(e.Message);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"处理短信失败: {ex.Message}");
    }
};
```

### 3. 保存重要短信

虽然不再存储到 SIM 卡，但可以保存到数据库：

```csharp
modem.SmsReceived += async (sender, e) =>
{
    if (e.Message != null)
    {
        await database.SaveSmsAsync(e.Message);
    }
};
```

## 故障排查

### 问题：收不到短信通知

**检查清单：**
1. 确认已调用 `OpenAsync()` 完成初始化
2. 确认已订阅 `SmsReceived` 事件
3. 检查日志中是否有 `AT+CNMI` 配置成功
4. 使用其他手机发送测试短信

### 问题：短信发送超时但对方收到了

这是正常现象，可能原因：
- 网络延迟
- 模块响应慢
- 短信中心处理慢

**处理方法：**
```csharp
try
{
    await modem.SendSmsAsync(phone, message);
}
catch (TimeoutException)
{
    // 虽然超时，但短信可能已发送
    // 可以等待几秒后询问对方是否收到
    await Task.Delay(5000);
}
```

## 相关文件

- `src/LexSMS/Features/SmsManager.cs` - 短信管理器
- `samples/SendSmsExample/` - 发送短信示例
- `samples/ReceiveSmsExample/` - 接收短信示例
- `src/LexSMS/Events/EventArgs.cs` - 事件参数定义
