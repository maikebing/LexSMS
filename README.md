# LexSMS — A76XX 4G模块 C# 封装库

A76XX 系列 4G 模块（SIMCom）的 C# 封装动态库，通过 AT 命令与模块通信，支持：

- 📞 **语音通话**：拨打电话、来电显示、接听/拒接/挂断
- 💬 **短信收发**：支持中英文（自动检测 GSM-7bit / UCS2 编码）
- 🌐 **HTTP 请求**：通过模块发起 HTTP/HTTPS GET、POST 等请求
- 📡 **MQTT 连接**：连接、发布、订阅 MQTT Broker
- 📍 **基站定位**：获取基站经纬度及 MCC/MNC/LAC/CellID
- 📊 **状态查询**：信号强度、网络信息、SIM 卡状态、模块信息（IMEI/固件版本等）

---

## 快速开始

### 安装

```xml
<!-- 在 .csproj 中添加项目引用 -->
<ProjectReference Include="path/to/LexSMS.csproj" />
```

### 基本用法

```csharp
using LexSMS;
using LexSMS.Core;
using LexSMS.Models;

// 创建模块实例
using var modem = new A76XXModem("COM3"); // Windows
// using var modem = new A76XXModem("/dev/ttyUSB2"); // Linux

// 注册事件
modem.IncomingCall += (sender, e) =>
{
    Console.WriteLine($"来电: {e.PhoneNumber}");
    // 接听
    _ = modem.AnswerCallAsync();
};

modem.SmsReceived += async (sender, e) =>
{
    Console.WriteLine($"收到新短信，索引: {e.Index}");
    var sms = await modem.ReadSmsAsync(e.Index);
    Console.WriteLine($"发件人: {sms?.PhoneNumber}, 内容: {sms?.Content}");
};

// 打开连接
await modem.OpenAsync();

// 检查模块状态
var moduleInfo = await modem.GetModuleInfoAsync();
Console.WriteLine($"模块型号: {moduleInfo.Model}, IMEI: {moduleInfo.Imei}");

// 检查信号强度
var signal = await modem.GetSignalInfoAsync();
Console.WriteLine($"信号: {signal.SignalLevel} ({signal.RssiDbm} dBm)");
```

---

## 功能示例

### 拨打电话

```csharp
var callInfo = await modem.DialAsync("+8613800138000");
Console.WriteLine($"正在拨出: {callInfo.PhoneNumber}");
// ... 通话中 ...
await modem.HangUpAsync();
```

### 发送短信（支持中文）

```csharp
// 发送英文短信
await modem.SendSmsAsync("13800138000", "Hello World!");

// 发送中文短信（自动使用 UCS2 编码）
await modem.SendSmsAsync("13800138000", "你好，这是一条中文短信！");
```

### 读取短信

```csharp
// 读取指定索引
var sms = await modem.ReadSmsAsync(1);
Console.WriteLine($"发件人: {sms?.PhoneNumber}");
Console.WriteLine($"内容: {sms?.Content}");
Console.WriteLine($"时间: {sms?.Timestamp}");

// 列出所有未读短信
var unreadList = await modem.ListSmsAsync(SmsStatus.ReceivedUnread);
foreach (var msg in unreadList)
{
    Console.WriteLine($"[{msg.Index}] {msg.PhoneNumber}: {msg.Content}");
}
```

### HTTP 请求

```csharp
// GET 请求
var response = await modem.HttpGetAsync("http://httpbin.org/get");
Console.WriteLine($"状态: {response.StatusCode}, 内容: {response.Body}");

// POST 请求
var postResp = await modem.HttpPostAsync(
    "http://httpbin.org/post",
    "{\"key\":\"value\"}",
    "application/json");
```

### MQTT

```csharp
// 连接 Broker
modem.MqttMessageReceived += (sender, e) =>
{
    Console.WriteLine($"MQTT消息 [{e.Message.Topic}]: {e.Message.Payload}");
};
modem.MqttConnectionStateChanged += (sender, e) =>
{
    Console.WriteLine($"MQTT {(e.IsConnected ? "已连接" : "已断开")}");
};

await modem.MqttConnectAsync(new MqttConfig
{
    BrokerAddress = "broker.hivemq.com",
    Port = 1883,
    ClientId = "A76XX_Demo",
    KeepAliveSeconds = 60
});

// 订阅主题
await modem.MqttSubscribeAsync("test/topic");

// 发布消息
await modem.MqttPublishAsync("test/topic", "Hello from A76XX!", qos: 1);

// 断开连接
await modem.MqttDisconnectAsync();
```

### 基站定位

```csharp
// 获取基站定位（需要运营商支持）
var location = await modem.GetCellLocationAsync();
if (location.IsValid)
{
    Console.WriteLine($"纬度: {location.Latitude}, 经度: {location.Longitude}");
    Console.WriteLine($"精度: {location.AccuracyMeters} 米");
}

// 获取基站信息
var cellInfo = await modem.GetCellInfoAsync();
Console.WriteLine($"MCC: {cellInfo.Mcc}, MNC: {cellInfo.Mnc}");
Console.WriteLine($"LAC: {cellInfo.Lac:X4}, CellID: {cellInfo.CellId:X8}");
```

### 查询 SIM 卡状态

```csharp
var simInfo = await modem.GetSimInfoAsync();
Console.WriteLine($"SIM状态: {simInfo.Status}");
Console.WriteLine($"IMSI: {simInfo.Imsi}");
Console.WriteLine($"ICCID: {simInfo.Iccid}");
Console.WriteLine($"电话号码: {simInfo.PhoneNumber}");
```

---

## 项目结构

```
src/LexSMS/
├── A76XXModem.cs           # 主门面类，统一入口
├── Core/
│   ├── AtChannel.cs        # 底层串口 AT 命令通信
│   ├── AtResponse.cs       # AT 响应数据结构
│   └── SerialPortConfig.cs # 串口配置
├── Features/
│   ├── CallManager.cs      # 通话管理
│   ├── SmsManager.cs       # 短信管理
│   ├── ModemHttpClient.cs  # HTTP 客户端
│   ├── ModemMqttClient.cs  # MQTT 客户端
│   ├── LocationManager.cs  # 定位管理
│   └── StatusManager.cs    # 状态查询
├── Models/                 # 数据模型
├── Events/                 # 事件参数
├── Helpers/
│   └── PduHelper.cs        # PDU 编解码（中英文短信）
└── Exceptions/             # 自定义异常
```

---

## 支持的 AT 命令

| 功能 | AT 命令 |
|------|---------|
| 拨打电话 | `ATD<number>;` |
| 接听电话 | `ATA` |
| 挂断电话 | `ATH` |
| 来电显示 | `AT+CLIP=1` |
| 发送短信 | `AT+CMGS` (PDU模式) |
| 接收短信 | `AT+CMGR`, `AT+CMGL` |
| HTTP请求 | `AT+HTTPINIT`, `AT+HTTPPARA`, `AT+HTTPACTION` |
| MQTT连接 | `AT+SMCONF`, `AT+SMCONN` |
| MQTT发布 | `AT+SMPUB` |
| MQTT订阅 | `AT+SMSUB` |
| 基站定位 | `AT+CLBS` |
| 信号强度 | `AT+CSQ` |
| 网络状态 | `AT+CREG`, `AT+COPS` |
| SIM状态 | `AT+CPIN`, `AT+CIMI`, `AT+CCID` |
| 模块信息 | `AT+CGMI`, `AT+CGMM`, `AT+CGMR`, `AT+CGSN` |

---

## 许可证

MIT License
