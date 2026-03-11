using LexSMS;
using LexSMS.Models;

namespace HttpDownloadExample;

class Program
{
    private const string HtmlUrl = "https://gitee.com/IoTSharp/IoTSharp/raw/master/servers.json";
    private const string ImageUrl = "https://gitee.com/static/images/gitee-logos/logo_gitee_white.png";

    static async Task Main(string[] args)
    {
        string portName = "COM3"; // Windows: COM3, Linux: /dev/ttyUSB0
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        string htmlPath = Path.Combine(AppContext.BaseDirectory, $"demo_{timestamp}.json");
        string imagePath = Path.Combine(AppContext.BaseDirectory, $"demo_{timestamp}.png");
        string imageCopyPath = Path.Combine(AppContext.BaseDirectory, $"demo_{timestamp}_copy.png");

        Console.WriteLine("=== A76XX HTTP 下载能力示例 ===");
        Console.WriteLine($"串口: {portName}");
        Console.WriteLine($"HTML URL: {HtmlUrl}");
        Console.WriteLine($"图片 URL: {ImageUrl}");
        Console.WriteLine("说明: 当前下载实现为逐块 HTTPREAD 模式（AT+HTTPREAD=offset,chunkSize 循环，+HTTPREAD: 0 终止）。\n");

        using var modem = new A76XXModem(portName, 115200)
        {
            EnableVerboseLogging = true,
            LogOutput = message => Console.WriteLine($"[LOG] {message}")
        };

        try
        {
            Console.WriteLine("正在初始化模块...");
            await modem.OpenAsync();

            Console.WriteLine("\n=== 1) GET + 保存 HTML 到本地文件 ===");
            HttpResponse htmlResp = await modem.HttpGetAsync(HtmlUrl);
            string htmlText = htmlResp.Body ?? string.Empty;
            await File.WriteAllTextAsync(htmlPath, htmlText);
            Console.WriteLine($"状态码: {htmlResp.StatusCode}, 长度: {htmlResp.ContentLength}");
            Console.WriteLine($"HTML 已保存: {htmlPath}");

            Console.WriteLine("\n=== 2) 下载图片到本地磁盘文件（逐块读取模式）===");
            using var imageDownloadCts = new CancellationTokenSource();
            using var keyMonitorCts = new CancellationTokenSource();
            Task keyMonitorTask = MonitorCancelKeyAsync(imageDownloadCts, keyMonitorCts.Token);

            try
            {
                HttpResponse imageResp = await modem.HttpDownloadFileAsync(ImageUrl, imagePath, cancellationToken: imageDownloadCts.Token);
                Console.WriteLine($"状态码: {imageResp.StatusCode}, 长度: {imageResp.ContentLength}");
                Console.WriteLine($"图片已保存: {imagePath}");
            }
            finally
            {
                keyMonitorCts.Cancel();
                await keyMonitorTask;
            }

            Console.WriteLine("\n=== 3) 下载 json 到 byte[] buffer（逐块读取模式）===");
            byte[] htmlBytes = await modem.HttpDownloadToBufferAsync(HtmlUrl);
            Console.WriteLine($"Buffer 字节数: {htmlBytes.Length}");


            Console.WriteLine("\n=== 5) 下载图片到 FileStream（逐块读取模式）===");
            await using (var targetStream = new FileStream(imageCopyPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                HttpResponse streamResp = await modem.HttpDownloadToStreamAsync(ImageUrl, targetStream);
                Console.WriteLine($"状态码: {streamResp.StatusCode}, 长度: {streamResp.ContentLength}");
            }
            Console.WriteLine($"图片副本已保存: {imageCopyPath}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n! 下载已取消。");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n执行失败: {ex.Message}");
            Console.WriteLine(ex);
        }
        finally
        {
            modem.Close();
        }

        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }

    private static async Task MonitorCancelKeyAsync(CancellationTokenSource downloadCts, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine("提示: 下载过程中按 C 可取消当前下载。");
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.C)
                    {
                        Console.WriteLine("\n检测到取消键，正在取消下载...");
                        downloadCts.Cancel();
                        return;
                    }
                }

                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}

