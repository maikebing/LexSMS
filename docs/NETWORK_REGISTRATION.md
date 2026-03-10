# 网络注册状态说明

## AT+CREG? 响应格式

### 标准响应格式
```
+CREG: <n>,<stat>[,<lac>,<ci>[,<AcT>]]
```

### 参数说明

#### 参数 `<n>` - URC 配置
URC（Unsolicited Result Code）主动上报配置：
- `0` = 禁用网络注册主动上报
- `1` = 启用主动上报（`+CREG: <stat>`）
- `2` = 启用主动上报并包含位置信息（`+CREG: <stat>,<lac>,<ci>`）

#### 参数 `<stat>` - 网络注册状态
**这是实际的网络注册状态值：**
| 值 | 枚举 | 说明 | 可用功能 |
|---|------|------|---------|
| 0 | NotRegistered | 未注册，未搜索 | 无 |
| 1 | RegisteredHome | 已注册，本地网络 | 语音、短信、数据 |
| 2 | Searching | 未注册，正在搜索 | 无 |
| 3 | RegistrationDenied | 注册被拒绝 | 无 |
| 4 | Unknown | 未知状态 | 无 |
| 5 | RegisteredRoaming | 已注册，漫游网络 | 语音、短信、数据 |
| 6 | RegisteredHomeSms | 已注册，本地网络（仅限SMS） | 短信 |
| 7 | RegisteredRoamingSms | 已注册，漫游网络（仅限SMS） | 短信 |

### 响应示例解析

#### 示例 1: `+CREG: 0,0`
```
参数0 (n): 0  → 禁用 URC
参数1 (stat): 0  → 未注册，未搜索
```
**解读：** 模块未注册到任何网络，也未在搜索网络。

**可能原因：**
- SIM卡未插入
- SIM卡未被识别
- 天线未连接
- 模块刚启动，尚未开始搜索

#### 示例 2: `+CREG: 0,2`
```
参数0 (n): 0  → 禁用 URC
参数1 (stat): 2  → 未注册，正在搜索
```
**解读：** 模块正在搜索可用网络，这是正常的初始化过程。

**操作：** 等待 10-60 秒，模块通常会自动注册到网络。

#### 示例 3: `+CREG: 0,1`
```
参数0 (n): 0  → 禁用 URC
参数1 (stat): 1  → 已注册，本地网络
```
**解读：** 模块已成功注册到本地网络，可以使用所有功能。

#### 示例 4: `+CREG: 2,1,"1234","5678",7`
```
参数0 (n): 2  → 启用 URC 并包含位置信息
参数1 (stat): 1  → 已注册，本地网络
参数2 (lac): "1234"  → 位置区码
参数3 (ci): "5678"  → 小区 ID
参数4 (AcT): 7  → 接入技术（LTE）
```
**解读：** 完整的注册信息，包括位置和网络类型。

## 代码解析逻辑

### StatusManager.GetNetworkInfoAsync()
```csharp
// 响应格式：+CREG: <n>,<stat>[,<lac>,<ci>]
// 或：+CREG: <stat> （如果之前未设置过 AT+CREG=n）

string data = line.Substring(6).Trim();  // 提取 "0,0" 或 "1"
string[] parts = data.Split(',');        // 分割成 ["0", "0"] 或 ["1"]

// 如果有2个或更多参数，第2个是状态；如果只有1个参数，第1个就是状态
int statPart = parts.Length >= 2 ? 1 : 0;

if (int.TryParse(parts[statPart].Trim(), out int stat))
{
    info.RegistrationStatus = (NetworkRegistrationStatus)stat;
}
```

### 解析示例

**输入：** `+CREG: 0,0`
```
data = "0,0"
parts = ["0", "0"]
parts.Length = 2
statPart = 1
stat = int.Parse(parts[1]) = 0
RegistrationStatus = NotRegistered (0)
```

**输入：** `+CREG: 1`
```
data = "1"
parts = ["1"]
parts.Length = 1
statPart = 0
stat = int.Parse(parts[0]) = 1
RegistrationStatus = RegisteredHome (1)
```

## 故障排查

### 状态 0 (未注册，未搜索)

**检查清单：**
1. ✅ SIM 卡是否插入
   ```csharp
   var simInfo = await modem.GetSimInfoAsync();
   Console.WriteLine($"SIM状态: {simInfo.Status}");
   ```

2. ✅ 信号强度是否正常
   ```csharp
   var signalInfo = await modem.GetSignalInfoAsync();
   Console.WriteLine($"信号: {signalInfo.SignalLevel} ({signalInfo.RssiDbm}dBm)");
   ```

3. ✅ 天线是否连接

4. ✅ 等待模块初始化（重启后等待 30-60 秒）

### 状态 2 (正在搜索)

**正常行为：** 模块正在搜索网络，通常需要 10-60 秒。

**操作：**
- 等待自动注册
- 确保信号强度足够（RSSI >= 10）

### 状态 3 (注册被拒绝)

**可能原因：**
- SIM卡未激活或已停机
- SIM卡欠费
- 运营商网络问题
- 模块被运营商限制

**操作：**
1. 检查 SIM 卡余额和状态
2. 使用其他设备测试 SIM 卡
3. 联系运营商确认账户状态

## 使用调试工具

运行网络调试工具查看详细信息：

```bash
cd samples/NetworkDebugExample
dotnet run
```

该工具会显示：
- 原始 AT 命令响应
- 详细的参数解析过程
- 当前网络状态
- 故障排查建议

## 相关命令

| 命令 | 功能 | 示例响应 |
|-----|------|---------|
| `AT+CREG?` | 查询 CS 域注册状态 | `+CREG: 0,1` |
| `AT+CGREG?` | 查询 GPRS（PS 域）注册状态 | `+CGREG: 0,1` |
| `AT+CEREG?` | 查询 EPS（LTE）注册状态 | `+CEREG: 0,1` |
| `AT+COPS?` | 查询运营商信息 | `+COPS: 0,0,"CHINA MOBILE",7` |
| `AT+CGATT?` | 查询 GPRS 附着状态 | `+CGATT: 1` |

## 完整工作流程

1. **检查 SIM 卡状态**
   ```csharp
   var simInfo = await modem.GetSimInfoAsync();
   if (simInfo.Status != SimStatus.Ready) {
       Console.WriteLine($"SIM卡未就绪: {simInfo.Status}");
       return;
   }
   ```

2. **检查信号强度**
   ```csharp
   var signalInfo = await modem.GetSignalInfoAsync();
   if (signalInfo.Rssi < 5) {
       Console.WriteLine("信号太弱");
       return;
   }
   ```

3. **等待网络注册**
   ```csharp
   for (int i = 0; i < 30; i++) {
       var networkInfo = await modem.GetNetworkInfoAsync();
       if (networkInfo.IsRegistered) {
           Console.WriteLine("网络注册成功");
           break;
       }
       await Task.Delay(2000);
   }
   ```

4. **确保 GPRS 附着**（用于数据通信）
   ```csharp
   var status = await modem.GetGprsAttachStatusAsync();
   if (status != GprsAttachStatus.Attached) {
       await modem.SetGprsAttachAsync(true);
   }
   ```
