# A76XX 4G LTE 模块 AT 指令协议文档（校准版）

## 文档修订记录
| 版本 | 日期       | 修改说明                                     |
|------|------------|----------------------------------------------|
| V1.0 | 2025-07-15 | 初始版本，基于 A76XX 使用手册整理            |
| V1.1 | 2025-07-16 | 校准版本，对照原始 PDF 完善细节，修正格式错误 |

---

## 1. 概述
本文档基于 A76XX 系列 4G LTE 模块（如 A7670C-LASC、A7670SA-LASE 等）的官方使用手册，整理了模块支持的核心 AT 指令集。指令涵盖模块信息查询、网络状态、短信收发、拨号通话、TCP/IP、UDP、MQTT、HTTP、基站定位及 TTS 等功能。文档旨在为开发者提供清晰、准确的指令参考。

**通用说明**：
- 所有 AT 指令均以 `AT` 或 `at` 开头，以回车换行（`<CR><LF>`）结束。
- 指令响应通常包含 `OK` 或 `ERROR`，以及相关的数据信息。
- 参数中的尖括号 `<>` 表示必须提供的参数，方括号 `[]` 表示可选参数。
- 波特率默认为 115200，可通过 `AT+IPR=?` 查询，不建议修改以避免连接问题。
- 发送指令时务必在末尾添加回车换行，每条指令独立一行。

---

## 2. 基础信息与网络状态指令

### 2.1 AT —— 测试连接
- **功能**：测试模块与串口通信是否正常。
- **格式**：`AT`
- **响应**：`OK`

### 2.2 ATi —— 查询模块信息
- **功能**：查询模块制造商、型号、版本、IMEI 等。
- **格式**：`ATi`
- **响应示例**：
  ```
  Manufacturer: INCORPORATED
  Model: A7670C-LASC
  Revision: A7670M6_V1.11.1
  IMEI: 866651065109178
  +GCAP: +CGSM,+FCLASS,+DS
  OK
  ```

### 2.3 AT+CGSN —— 查询 IMEI
- **功能**：查询模块的唯一设备标识 IMEI 号。
- **格式**：`AT+CGSN`
- **响应示例**：
  ```
  866651065109178
  OK
  ```

### 2.4 AT+CIMI —— 查询 IMSI
- **功能**：查询 SIM 卡的国际移动用户识别码（IMSI）。
- **格式**：`AT+CIMI`
- **响应示例**：
  ```
  460023804807270
  OK
  ```

### 2.5 AT+CICCID —— 查询 ICCID
- **功能**：查询 SIM 卡的集成电路卡识别码（ICCID，即卡号）。
- **格式**：`AT+CICCID`
- **响应示例**：
  ```
  +ICCID: 898600281319F1035270
  OK
  ```

### 2.6 AT+CPIN? —— 查询 SIM 卡状态
- **功能**：查询 SIM 卡是否就绪。
- **格式**：`AT+CPIN?`
- **响应示例**：
  ```
  +CPIN: READY
  OK
  ```
- **说明**：返回 `READY` 表示 SIM 卡正常识别。

### 2.7 AT+CSQ —— 查询信号质量
- **功能**：查询当前信号强度。
- **格式**：`AT+CSQ`
- **响应示例**：
  ```
  +CSQ: 21,99
  OK
  ```
- **参数说明**：第一个数值范围 0~31，越大信号越好（10 以上通信基本正常）；第二个通常为 99。

### 2.8 AT+CGATT? —— 查询 GPRS 附着状态
- **功能**：查询模块是否已附着到 GPRS 网络（即 PDP 激活状态）。
- **格式**：`AT+CGATT?`
- **响应示例**：
  ```
  +CGATT: 1
  OK
  ```
- **说明**：`1` 表示已附着，`0` 表示未附着。网络操作前需确保为 `1`。

### 2.9 AT+CREG? —— 查询网络注册状态（2G/3G）
- **功能**：查询模块在当前网络的注册状态。
- **格式**：`AT+CREG?`
- **响应示例**：
  ```
  +CREG: 0,1
  OK
  ```
- **参数说明**：第二个参数 `1` 表示注册到本地网络，`5` 表示漫游。

### 2.10 AT+CGREG? —— 查询 GPRS 网络注册状态
- **功能**：查询 GPRS 服务注册状态（可理解为 2G 网络注册）。
- **格式**：`AT+CGREG?`
- **响应示例**：`+CGREG: 0,1`

### 2.11 AT+CEREG? —— 查询 EPS 网络注册状态（4G）
- **功能**：查询 4G 网络注册状态（EPS 网络）。
- **格式**：`AT+CEREG?`
- **响应示例**：
  ```
  +CEREG: 0,1
  OK
  ```

### 2.12 AT+CGPADDR —— 查询 IP 地址
- **功能**：查询模块分配的 IP 地址。
- **格式**：`AT+CGPADDR`
- **响应示例**：
  ```
  +CGPADDR: 1,10.217.21.133,...
  OK
  ```

### 2.13 AT+IPR —— 查询/设置波特率
- **功能**：查询或设置串口波特率。
- **格式**：`AT+IPR?`（查询），`AT+IPR=<rate>`（设置）
- **说明**：不建议随意更改，以免忘记。

### 2.14 ATE —— 设置回显
- **功能**：开启或关闭命令回显。
- **格式**：`ATE0`（关闭回显），`ATE1`（开启回显）
- **响应**：`OK`

---

## 3. 拨号与通话指令

### 3.1 ATD —— 拨打电话
- **功能**：拨打电话号码。
- **格式**：`ATD<phone_number>;`（**注意分号结尾**）
- **示例**：`ATD059188206255;`
- **响应**：`OK` 表示拨号成功，后续会收到 `+CLCC` 等通话状态提示，通话结束显示 `NO CARRIER`。

### 3.2 ATA —— 接听电话
- **功能**：接听来电。
- **格式**：`ATA`
- **响应**：通话建立后显示 `OK`，结束时显示 `NO CARRIER`。

### 3.3 AT+CLCC —— 查询当前通话
- **功能**：列出当前通话状态。
- **格式**：`AT+CLCC`
- **响应示例**：
  ```
  +CLCC: 1,0,0,0,0,"059188206255",129,""
  ```
- **参数说明**：包含通话索引、方向、状态、号码等信息。

### 3.4 AT+COLP —— 查询被叫号码
- **功能**：查询当前通话的对方号码。
- **格式**：`AT+COLP`
- **响应示例**：`+COLP: "059188206255",129`

---

## 4. 短信指令

### 4.1 AT+CMGF —— 设置短信格式
- **功能**：设置短信使用文本模式还是 PDU 模式。
- **格式**：`AT+CMGF=<mode>`
  - `<mode>`：`0` 为 PDU 模式，`1` 为文本模式。
- **示例**：
  - `AT+CMGF=1`  // 文本模式
  - `AT+CMGF=0`  // PDU 模式
- **响应**：`OK`

### 4.2 AT+CMGS —— 发送短信

#### 4.2.1 文本模式（+CMGF=1）
- **格式**：`AT+CMGS="<phone_number>"`
- **响应**：返回 `>` 提示符后，输入短信内容（英文/ASCII），以 `Ctrl+Z`（十六进制 `1A`）结束。
- **示例**：
  ```
  AT+CMGS="18144070918"
  > HELLO 4G<Ctrl+Z>
  ```
- **成功响应**：`+CMGS: <mr>` 和 `OK`

#### 4.2.2 PDU 模式（+CMGF=0）
- **格式**：`AT+CMGS=<length>`
  - `<length>` 为 PDU 串长度（不包括 SCA 头长度，需按 PDU 编码规范计算）。
- **响应**：`>` 提示符后输入 PDU 串，以 `Ctrl+Z` 结束。
- **示例**（向 15050850677 发送“你好”）：
  ```
  AT+CMGS=19
  > 0011000D91685150800576F70008C4044F60597D<Ctrl+Z>
  ```
- **成功响应**：`+CMGS: <mr>` 和 `OK`

### 4.3 AT+CNMI —— 新消息指示
- **功能**：设置新消息到达时的提示方式。
- **格式**：`AT+CNMI=<mode>,<mt>[,<bm>,<ds>,<bfr>]`
- **常用设置**：
  - `AT+CNMI=2,1`  // 将新消息存储到 SIM 卡并通知 TE（返回 `+CMTI: "SM",<index>`）
  - `AT+CNMI=2,2`  // 直接转发新消息内容到 TE（文本模式返回 `+CMT:`，PDU 模式返回 PDU 串）
- **响应**：`OK`

### 4.4 AT+CMGR —— 读取短信
- **功能**：读取指定索引的短信。
- **格式**：`AT+CMGR=<index>`
- **响应示例**（文本模式）：
  ```
  +CMGR: "REC UNREAD","+8618144070918","","22/09/26,21:52:14+32"
  send 4g
  OK
  ```

### 4.5 AT+CMGL —— 列出短信
- **功能**：列出符合状态的短信。
- **格式**：`AT+CMGL=<stat>`，`<stat>` 可以是 `"ALL"`、`"REC UNREAD"` 等。
- **响应**：多条短信内容。

### 4.6 短信编码补充
- **文本模式**：仅支持 ASCII 字符。
- **PDU 模式**：支持 Unicode（中文需转换为 UCS2 编码），通过工具生成 PDU 串。

---

## 5. TCP/IP 指令

### 5.1 AT+NETOPEN —— 打开网络
- **功能**：激活网络连接，准备进行 TCP/UDP 通信。
- **格式**：`AT+NETOPEN`
- **前提**：`AT+CGATT?` 返回 `1` 后才可执行。
- **响应**：`OK`，之后可进行连接操作。

### 5.2 AT+CIPOPEN —— 打开连接
- **功能**：建立到远程服务器的 TCP/UDP 连接。
- **格式**：
  - TCP：`AT+CIPOPEN=<connect_index>,"TCP","<remote_ip>",<remote_port>`
  - UDP：`AT+CIPOPEN=<connect_index>,"UDP",,,<local_port>`（先打开本地端口，然后发送时指定目标）
- **示例**（TCP）：
  ```
  AT+CIPOPEN=1,"TCP","101.200.212.234",1001
  ```
- **响应**：`OK` 表示连接已建立。

### 5.3 AT+CIPSEND —— 发送数据
- **功能**：通过指定连接发送数据。
- **格式**：
  - `AT+CIPSEND=<connect_index>,<length>`  // 发送指定长度的数据（后续出现 `>` 提示输入）
  - UDP 模式可指定目标：`AT+CIPSEND=<connect_index>,,"<remote_ip>",<remote_port>`
- **示例**：
  ```
  AT+CIPSEND=1,5
  > 12345<Ctrl+Z>
  ```
- **响应**：`SEND OK` 或失败提示。

### 5.4 接收数据
- **说明**：当模块收到 TCP/UDP 数据时，会自动通过串口输出，无需发送读取命令。输出格式为：
  ```
  RECV FROM: <ip>:<port>
  +IPD<length>: <data>
  ```

### 5.5 AT+CIPCLOSE —— 关闭连接
- **功能**：关闭指定连接。
- **格式**：`AT+CIPCLOSE=<connect_index>`
- **响应**：`OK`

### 5.6 AT+NETCLOSE —— 关闭网络
- **功能**：关闭网络连接。
- **格式**：`AT+NETCLOSE`
- **响应**：`OK`

---

## 6. UDP 指令
UDP 指令与 TCP 类似，但 `AT+CIPOPEN` 参数不同：
- 打开 UDP 本地端口：`AT+CIPOPEN=1,"UDP",,,5000`
- 发送数据时指定目标：`AT+CIPSEND=1,,"101.200.212.234",1001`
- 接收数据自动输出，格式同 TCP。

---

## 7. MQTT 指令

### 7.1 AT+CMQTTSTART —— 启动 MQTT 服务
- **功能**：启动 MQTT 协议栈。
- **格式**：`AT+CMQTTSTART`
- **响应**：`OK`

### 7.2 AT+CMQTTACCQ —— 设置客户端 ID
- **功能**：为 MQTT 连接分配客户端标识符。
- **格式**：`AT+CMQTTACCQ=<client_index>,"<client_id>",<qos>`
- **示例**：`AT+CMQTTACCQ=0,"CAT1Module",0`
- **响应**：`OK`

### 7.3 AT+CMQTTCONNECT —— 连接 MQTT 服务器
- **功能**：连接到 MQTT 代理服务器。
- **格式**：`AT+CMQTTCONNECT=<client_index>,"tcp://<host>:<port>",<keepalive>,<clean_session>`
- **示例**：`AT+CMQTTCONNECT=0,"tcp://106.14.148.185:1883",60,1`
- **响应**：`OK` 后等待连接成功提示。

### 7.4 AT+CMQTTSUBTOPIC —— 设置订阅主题
- **功能**：设置要订阅的主题及其长度。
- **格式**：`AT+CMQTTSUBTOPIC=<client_index>,<topic_length>,<qos>`
- **示例**：`AT+CMQTTSUBTOPIC=0,9,1`
- **后续操作**：在 `>` 提示后输入主题内容（如 `testtopic`）。

### 7.5 AT+CMQTTSUB —— 启动订阅
- **功能**：执行主题订阅。
- **格式**：`AT+CMQTTSUB=<client_index>`
- **响应**：`OK`，成功后收到 `+CMQTTSUB: 0,0` 确认。

### 7.6 AT+CMQTTTOPIC —— 设置发布主题
- **功能**：设置要发布消息的主题。
- **格式**：`AT+CMQTTTOPIC=<client_index>,<topic_length>`
- **示例**：`AT+CMQTTTOPIC=0,9`
- **后续**：输入主题内容。

### 7.7 AT+CMQTTPAYLOAD —— 设置发布内容
- **功能**：设置要发布的消息内容。
- **格式**：`AT+CMQTTPAYLOAD=<client_index>,<payload_length>`
- **示例**：`AT+CMQTTPAYLOAD=0,13`
- **后续**：输入消息内容（如 `{"temp":"45"}`）。

### 7.8 AT+CMQTTPUB —— 发布消息
- **功能**：执行消息发布。
- **格式**：`AT+CMQTTPUB=<client_index>,<qos>,<timeout>`
- **示例**：`AT+CMQTTPUB=0,1,60`
- **响应**：`OK` 后等待发布完成提示 `+CMQTTPUB: 0,0`。

### 7.9 接收 MQTT 消息
- **说明**：当有订阅消息到达时，自动输出：
  ```
  +CMQTTRXSTART: 0,<topic_len>,<payload_len>
  +CMQTTRXTOPIC: 0,<topic_len>
  <topic>
  +CMQTTRXPAYLOAD: 0,<payload_len>
  <payload>
  +CMQTTRXEND: 0
  ```

---

## 8. 基站定位指令

### 8.1 AT+CREG —— 设置网络注册结果码（获取 LAC/CI）
- **功能**：设置注册结果码模式，使能返回位置区编码（LAC）和小区 ID（CI）。
- **格式**：`AT+CREG=<mode>`
  - `<mode>`：`2` 表示激活注册结果码并显示区域和小区信息。
- **示例**：`AT+CREG=2`
- **查询**：`AT+CREG?` 返回 `+CREG: 2,1,<lac>,<ci>`，如 `+CREG: 2,1,591B,02686442`。

### 8.2 AT+CEREG —— 设置 EPS 注册结果码
- **功能**：针对 4G 网络，获取 LAC 和 CI。
- **格式**：`AT+CEREG=2`
- **查询**：`AT+CEREG?` 返回 `+CEREG: 2,1,<lac>,<ci>`。

### 8.3 AT+CLBS —— 基站定位
- **功能**：直接通过基站获取经纬度信息（需网络支持）。
- **格式**：`AT+CLBS=<mode>`（通常为 1）
- **响应示例**：
  ```
  +CLBS: 0,26.060793,119.209763,550
  OK
  ```
- **说明**：返回码、纬度、经度、精度（米）。

### 8.4 恢复网络注册模式
- **使用完定位后建议恢复**：`AT+CREG=0`

---

## 9. HTTP 指令

### 9.1 AT+HTTPINIT —— 初始化 HTTP 服务
- **功能**：启动 HTTP 功能。
- **格式**：`AT+HTTPINIT`
- **响应**：`OK`

### 9.2 AT+HTTPPARA —— 设置 HTTP 参数
- **功能**：配置 HTTP 会话参数，如 URL。
- **格式**：`AT+HTTPPARA="<param>","<value>"`
- **常用参数**：`"URL"`，例如：
  ```
  AT+HTTPPARA="URL","http://example.com"
  ```

### 9.3 AT+HTTPACTION —— 执行 HTTP 请求
- **功能**：发起 HTTP 动作（GET/POST 等）。
- **格式**：`AT+HTTPACTION=<method>`
  - `<method>`：`0` 为 GET，`1` 为 POST，`2` 为 HEAD。
- **示例**：`AT+HTTPACTION=0`
- **响应**：执行后返回 `+HTTPACTION: <method>,<status_code>,<data_len>`。

### 9.4 AT+HTTPREAD —— 分块读取 HTTP 响应体
- **功能**：逐块读取服务器返回的数据，每次指定偏移和块大小。
- **格式**：`AT+HTTPREAD=<offset>,<length>`
  - `<offset>`：本次读取的起始字节偏移（首次为 0）。
  - `<length>`：本次请求读取的字节数（建议 ≤ 2048）。
- **单次响应格式**：
  ```
  +HTTPREAD: <actual_length>
  <actual_length 字节的二进制数据>
  OK
  ```
- **结束标志**：当 `<actual_length>` 为 `0` 时表示数据已读完：
  ```
  +HTTPREAD: 0
  OK
  ```
- **下载循环示例**（总长 11363 字节，每块 1024 字节）：
  ```
  -> AT+HTTPREAD=0,1024
  <- +HTTPREAD: 1024  <1024 字节数据>  OK

  -> AT+HTTPREAD=1024,1024
  <- +HTTPREAD: 1024  <1024 字节数据>  OK

  ...（重复直到剩余不足一块）...

  -> AT+HTTPREAD=11264,1024
  <- +HTTPREAD: 99  <99 字节数据>  OK

  -> AT+HTTPREAD=11363,1024
  <- +HTTPREAD: 0  OK          ← 数据读完
  ```
- **说明**：
  - `<actual_length>` 可能小于请求的 `<length>`（最后一块），需按实际值推进偏移。
  - 超时时间建议按块大小计算：基础 60 秒 + 每 1 KB 增加 30 秒，最大 10 分钟。
  - 必须在 `AT+HTTPACTION` 成功并收到 `+HTTPACTION` URC 后才能调用。

### 9.5 AT+HTTPHEAD —— 读取响应头
- **功能**：读取 HTTP 响应头信息。
- **格式**：`AT+HTTPHEAD`

### 9.6 AT+HTTPDATA —— 输入 HTTP 数据（用于 POST）
- **功能**：提供 POST 请求的数据内容。
- **格式**：`AT+HTTPDATA=<length>,<timeout>`
- **后续**：在 `>` 提示后输入数据。

### 9.7 AT+HTTPTERM —— 终止 HTTP 服务
- **功能**：关闭 HTTP 功能。
- **格式**：`AT+HTTPTERM`
- **响应**：`OK`

### 9.8 文件相关指令（扩展）
- `AT+HTTPPOSTFILE`：通过 HTTP 发送文件。
- `AT+HTTPREADFILE`：将 HTTP 响应保存到文件。

---

## 10. TTS 语音合成指令

### 10.1 AT+CTTS=? —— 查询 TTS 支持
- **功能**：查询模块是否支持 TTS 功能。
- **格式**：`AT+CTTS=?`
- **响应**：若支持则返回 `OK` 及参数列表，否则返回 `ERROR`。

### 10.2 AT+CTTS —— 播放文本

#### 10.2.1 UCS2 编码输入
- **格式**：`AT+CTTS=1,"<ucs2_string>"`
- **示例**：播放“欢迎使用语音合成系统”
  ```
  AT+CTTS=1,"6B228FCE4F7F75288BED97F3540862107CFB7EDF"
  ```

#### 10.2.2 混合编码输入（ASCII + GBK）
- **格式**：`AT+CTTS=2,"<text>"`
- **示例**：
  ```
  AT+CTTS=2,"hello，欢迎使用语音合成系统"
  ```

### 10.3 AT+CTTS=0 —— 停止播放
- **功能**：停止当前 TTS 播放。
- **格式**：`AT+CTTS=0`

---

## 附录：常用指令速查表
| 指令 | 功能 |
|------|------|
| AT | 测试通信 |
| ATi | 查询模块信息 |
| AT+CGSN | 查询 IMEI |
| AT+CIMI | 查询 IMSI |
| AT+CPIN? | 查询 SIM 卡状态 |
| AT+CSQ | 信号质量 |
| AT+CGATT? | GPRS 附着状态 |
| AT+CREG? | 网络注册状态 |
| AT+CEREG? | 4G 网络注册状态 |
| AT+CGPADDR | 查询 IP 地址 |
| ATD<号码>; | 拨打电话 |
| ATA | 接听电话 |
| AT+CMGF | 设置短信模式 |
| AT+CMGS | 发送短信 |
| AT+CNMI | 新消息指示 |
| AT+CMGR | 读取短信 |
| AT+NETOPEN | 开启网络 |
| AT+CIPOPEN | 建立连接 |
| AT+CIPSEND | 发送数据 |
| AT+CMQTTSTART | 启动 MQTT |
| AT+CMQTTCONNECT | 连接 MQTT 服务器 |
| AT+CMQTTSUB | 订阅主题 |
| AT+CMQTTPUB | 发布消息 |
| AT+CREG=2 | 开启基站定位信息 |
| AT+CLBS=1 | 获取经纬度 |
| AT+HTTPINIT | 初始化 HTTP |
| AT+HTTPACTION=0 | HTTP GET 请求 |
| AT+CTTS | 语音合成 |

---

**文档结束**