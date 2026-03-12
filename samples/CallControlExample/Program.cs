using LexSMS;
using LexSMS.Exceptions;
using LexSMS.Models;
using System.Text.Json;

namespace CallControlExample;

internal static class Program
{
    private static string? s_lastIncomingNumber;
    private static bool s_autoAnswerEnabled;
    private static readonly List<CallHistoryRecord> s_callHistory = [];
    private static readonly string s_callHistoryFilePath = Path.Combine(AppContext.BaseDirectory, "call-history.json");
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true };
    private static readonly TimeSpan s_signalRefreshInterval = TimeSpan.FromSeconds(30);
    private static CallHistoryRecord? s_activeCallRecord;

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=== LexSMS 通话控制示例 ===");
        Console.Write("请输入串口名称（默认 COM3）: ");
        var portName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(portName))
        {
            portName = "COM3";
        }

        Console.Write("是否开启自动接听？(y/N): ");
        s_autoAnswerEnabled = IsYes(Console.ReadLine());

        using var signalRefreshCts = new CancellationTokenSource();
        using var modem = new A76XXModem(portName, 115200);
        Task? signalRefreshTask = null;

        modem.LogOutput = message => Console.WriteLine($"[LOG] {message}");
        modem.IncomingCall += async (_, eventArgs) =>
        {
            s_lastIncomingNumber = eventArgs.PhoneNumber;
            WriteHighlightedBlock(
                "[EVENT] 检测到来电",
                $"[EVENT] 来电号码: {eventArgs.PhoneNumber}",
                "[EVENT] 可选择 3 接听，或选择 7 拒接。");

            if (s_autoAnswerEnabled)
            {
                Console.WriteLine("[EVENT] 自动接听已开启，正在接听...");
                try
                {
                    await modem.AnswerCallAsync().ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"[EVENT] 自动接听失败: {ex.Message}");
                }
                catch (ModemException ex)
                {
                    Console.WriteLine($"[EVENT] 自动接听失败: {ex.Message}");
                }
            }

            Console.WriteLine();
            ShowMenu();
        };
        modem.CallStateChanged += (_, eventArgs) =>
        {
            UpdateLastCallRecord(eventArgs.CallInfo);
            Console.WriteLine();
            Console.WriteLine($"[EVENT] 通话状态: {FormatCallState(eventArgs.CallInfo)}");
            Console.WriteLine();
        };

        try
        {
            LoadCallHistory();
            Console.WriteLine($"通话记录文件: {s_callHistoryFilePath}");
            Console.WriteLine("正在打开串口并初始化模块...");
            await modem.OpenAsync();
            await ShowStartupStatusAsync(modem).ConfigureAwait(false);
            await ShowSignalStrengthAsync(modem, "启动信号强度").ConfigureAwait(false);

            signalRefreshTask = RunSignalRefreshLoopAsync(modem, signalRefreshCts.Token);

            var clipResponse = await modem.SendRawCommandAsync("AT+CLIP=1", 3000);
            if (!clipResponse.IsOk)
            {
                Console.WriteLine($"启用来电显示失败: {clipResponse.RawResponse}");
            }

            await RunMenuAsync(modem).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生异常: {ex.Message}");
        }
        finally
        {
            signalRefreshCts.Cancel();
            if (signalRefreshTask is not null)
            {
                try
                {
                    await signalRefreshTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            modem.Close();
        }
    }

    private static async Task RunMenuAsync(A76XXModem modem)
    {
        while (true)
        {
            ShowMenu();
            Console.Write("请选择操作: ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await DialAsync(modem).ConfigureAwait(false);
                        break;
                    case "2":
                        await modem.HangUpAsync().ConfigureAwait(false);
                        Console.WriteLine("已发送挂断命令。");
                        break;
                    case "3":
                        await modem.AnswerCallAsync().ConfigureAwait(false);
                        Console.WriteLine("已发送接听命令。");
                        break;
                    case "4":
                        ShowIncomingNumber();
                        break;
                    case "5":
                        await ShowCalledNumberAsync(modem).ConfigureAwait(false);
                        break;
                    case "6":
                        await ShowCallStatusAsync(modem).ConfigureAwait(false);
                        break;
                    case "7":
                        await modem.RejectCallAsync().ConfigureAwait(false);
                        Console.WriteLine("已发送拒接命令。");
                        break;
                    case "8":
                        s_autoAnswerEnabled = !s_autoAnswerEnabled;
                        Console.WriteLine($"自动接听已切换为: {(s_autoAnswerEnabled ? "开" : "关")}");
                        break;
                    case "9":
                        ShowCallHistory();
                        break;
                    case "10":
                        await ShowSignalStrengthAsync(modem).ConfigureAwait(false);
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("无效选择，请重新输入。");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"操作失败: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private static void ShowMenu()
    {
        Console.WriteLine("1. 拨打电话");
        Console.WriteLine("2. 挂断电话");
        Console.WriteLine("3. 接听电话");
        Console.WriteLine("4. 显示来电");
        Console.WriteLine("5. 显示被叫号码");
        Console.WriteLine("6. 查询通话状态");
        Console.WriteLine("7. 拒接电话");
        Console.WriteLine($"8. 自动接听: {(s_autoAnswerEnabled ? "开" : "关")}");
        Console.WriteLine("9. 通话记录列表");
        Console.WriteLine("10. 查询信号强度");
        Console.WriteLine("0. 退出");
    }

    private static async Task ShowStartupStatusAsync(A76XXModem modem)
    {
        Console.WriteLine();
        Console.WriteLine("=== 启动状态检测 ===");

        var simInfo = await modem.GetSimInfoAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(simInfo.ErrorMessage))
        {
            Console.WriteLine($"SIM 状态检测失败: {simInfo.ErrorMessage}");
        }
        else
        {
            Console.WriteLine($"SIM 状态: {simInfo.Status}");
            if (!string.IsNullOrWhiteSpace(simInfo.PhoneNumber))
            {
                Console.WriteLine($"SIM 电话号码: {simInfo.PhoneNumber}");
            }
        }

        var networkInfo = await modem.GetNetworkInfoAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(networkInfo.ErrorMessage))
        {
            Console.WriteLine($"网络状态检测失败: {networkInfo.ErrorMessage}");
        }
        else
        {
            Console.WriteLine($"网络注册状态: {networkInfo.RegistrationStatus}");
            Console.WriteLine($"已注册网络: {(networkInfo.IsRegistered ? "是" : "否")}");
            Console.WriteLine($"运营商: {networkInfo.OperatorName ?? "未知"}");
            Console.WriteLine($"接入类型: {networkInfo.AccessType}");
        }

        Console.WriteLine($"自动接听: {(s_autoAnswerEnabled ? "已开启" : "未开启")}");
        Console.WriteLine($"已加载通话记录: {s_callHistory.Count} 条");
        Console.WriteLine();
    }

    private static async Task DialAsync(A76XXModem modem)
    {
        Console.Write("请输入要拨打的号码: ");
        var phoneNumber = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            Console.WriteLine("号码不能为空。");
            return;
        }

        var callInfo = await modem.DialAsync(phoneNumber).ConfigureAwait(false);
        Console.WriteLine($"正在拨打: {FormatCallState(callInfo)}");
    }

    private static void ShowIncomingNumber()
    {
        if (string.IsNullOrWhiteSpace(s_lastIncomingNumber))
        {
            Console.WriteLine("当前没有记录到来电号码。请等待模块上报来电事件。");
            return;
        }

        Console.WriteLine($"最近一次来电号码: {s_lastIncomingNumber}");
    }

    private static async Task ShowCalledNumberAsync(A76XXModem modem)
    {
        var currentCall = await modem.GetCurrentCallAsync().ConfigureAwait(false);
        if (currentCall.Direction == CallDirection.Outgoing && !string.IsNullOrWhiteSpace(currentCall.PhoneNumber))
        {
            Console.WriteLine($"当前被叫号码: {currentCall.PhoneNumber}");
        }
        else
        {
            Console.WriteLine("当前没有外呼通话记录。");
        }

        var colpResponse = await modem.SendRawCommandAsync("AT+COLP", 3000).ConfigureAwait(false);
        var colpNumber = TryParseColpNumber(colpResponse);
        if (!string.IsNullOrWhiteSpace(colpNumber))
        {
            Console.WriteLine($"模块返回的被叫号码: {colpNumber}");
        }
        else if (!colpResponse.IsOk)
        {
            Console.WriteLine($"查询被叫号码失败: {colpResponse.RawResponse}");
        }
    }

    private static async Task ShowCallStatusAsync(A76XXModem modem)
    {
        var currentCall = await modem.GetCurrentCallAsync().ConfigureAwait(false);
        Console.WriteLine($"当前通话: {FormatCallState(currentCall)}");
    }

    private static void ShowCallHistory()
    {
        if (s_callHistory.Count == 0)
        {
            Console.WriteLine("当前没有通话记录。");
            return;
        }

        Console.WriteLine("通话记录列表:");
        for (int index = s_callHistory.Count - 1; index >= 0; index--)
        {
            var callRecord = s_callHistory[index];
            Console.WriteLine($"{s_callHistory.Count - index}. {FormatCallHistory(callRecord)}");
        }
    }

    private static async Task ShowSignalStrengthAsync(A76XXModem modem, string title = "当前信号强度")
    {
        var signalInfo = await modem.GetSignalInfoAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(signalInfo.ErrorMessage))
        {
            Console.WriteLine($"查询信号强度失败: {signalInfo.ErrorMessage}");
            return;
        }

        Console.WriteLine($"{title}: {signalInfo.SignalLevel}");
        Console.WriteLine($"RSSI: {signalInfo.Rssi}");
        Console.WriteLine($"dBm: {signalInfo.RssiDbm}");
        Console.WriteLine($"BER: {signalInfo.Ber}");
    }

    private static bool IsYes(string? input)
        => string.Equals(input?.Trim(), "y", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(input?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);

    private static void UpdateLastCallRecord(CallInfo callInfo)
    {
        if (callInfo.State == CallState.Idle)
        {
            return;
        }

        var observedAt = DateTime.Now;

        if (s_activeCallRecord is null)
        {
            s_activeCallRecord = new CallHistoryRecord
            {
                PhoneNumber = callInfo.PhoneNumber,
                Direction = callInfo.Direction,
                State = callInfo.State,
                StartedAt = callInfo.StartTime ?? observedAt,
                LastUpdatedAt = observedAt
            };
            s_callHistory.Add(s_activeCallRecord);
        }
        else
        {
            s_activeCallRecord.PhoneNumber = string.IsNullOrWhiteSpace(callInfo.PhoneNumber)
                ? s_activeCallRecord.PhoneNumber
                : callInfo.PhoneNumber;
            s_activeCallRecord.Direction = callInfo.Direction;
            s_activeCallRecord.State = callInfo.State;
            s_activeCallRecord.StartedAt = callInfo.StartTime ?? s_activeCallRecord.StartedAt;
            s_activeCallRecord.LastUpdatedAt = observedAt;
        }

        if (callInfo.State == CallState.Disconnected)
        {
            s_activeCallRecord.EndedAt = observedAt;
            s_activeCallRecord = null;
        }

        try
        {
            PersistCallHistory();
        }
        catch (IOException ex)
        {
            Console.WriteLine($"写入通话记录失败: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"写入通话记录失败: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"写入通话记录失败: {ex.Message}");
        }
    }

    private static string FormatCallHistory(CallHistoryRecord callRecord)
    {
        var number = string.IsNullOrWhiteSpace(callRecord.PhoneNumber) ? "未知号码" : callRecord.PhoneNumber;
        var direction = callRecord.Direction == CallDirection.Incoming ? "来电" : "去电";
        var timeText = callRecord.StartedAt.ToString("yyyy-MM-dd HH:mm:ss");
        var durationText = callRecord.EndedAt.HasValue
            ? (callRecord.EndedAt.Value - callRecord.StartedAt).ToString(@"hh\:mm\:ss")
            : "未结束";

        return $"{direction} | {number} | 状态 {callRecord.State} | 开始 {timeText} | 时长 {durationText}";
    }

    private static void LoadCallHistory()
    {
        if (!File.Exists(s_callHistoryFilePath))
        {
            return;
        }

        var json = File.ReadAllText(s_callHistoryFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var history = JsonSerializer.Deserialize<List<CallHistoryRecord>>(json, s_jsonSerializerOptions);
        if (history is null)
        {
            return;
        }

        s_callHistory.Clear();
        s_callHistory.AddRange(history);
    }

    private static void PersistCallHistory()
    {
        var json = JsonSerializer.Serialize(s_callHistory, s_jsonSerializerOptions);
        File.WriteAllText(s_callHistoryFilePath, json);
    }

    private static async Task RunSignalRefreshLoopAsync(A76XXModem modem, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(s_signalRefreshInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                Console.WriteLine();
                await ShowSignalStrengthAsync(modem, "自动刷新信号强度").ConfigureAwait(false);
                Console.WriteLine();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ModemException ex)
            {
                Console.WriteLine($"自动刷新信号强度失败: {ex.Message}");
            }
        }
    }

    private static void WriteHighlightedBlock(params string[] lines)
    {
        var originalForeground = Console.ForegroundColor;
        var originalBackground = Console.BackgroundColor;

        try
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Yellow;

            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }
        finally
        {
            Console.ForegroundColor = originalForeground;
            Console.BackgroundColor = originalBackground;
            Console.WriteLine();
        }
    }

    private static string FormatCallState(CallInfo callInfo)
    {
        var number = string.IsNullOrWhiteSpace(callInfo.PhoneNumber) ? "未知号码" : callInfo.PhoneNumber;
        var direction = callInfo.Direction == CallDirection.Incoming ? "来电" : "去电";

        if (callInfo.State == CallState.Idle)
        {
            return "空闲";
        }

        if (callInfo.State == CallState.Active && callInfo.StartTime.HasValue)
        {
            return $"{direction} | {number} | {callInfo.State} | 时长 {callInfo.Duration:hh\\:mm\\:ss}";
        }

        return $"{direction} | {number} | {callInfo.State}";
    }

    private static string? TryParseColpNumber(LexSMS.Core.AtResponse response)
    {
        foreach (var line in response.Lines)
        {
            if (!line.StartsWith("+COLP:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var firstQuote = line.IndexOf('"');
            if (firstQuote < 0)
            {
                continue;
            }

            var secondQuote = line.IndexOf('"', firstQuote + 1);
            if (secondQuote <= firstQuote)
            {
                continue;
            }

            return line.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        return null;
    }

    private sealed class CallHistoryRecord
    {
        public string? PhoneNumber { get; set; }
        public CallDirection Direction { get; set; }
        public CallState State { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
