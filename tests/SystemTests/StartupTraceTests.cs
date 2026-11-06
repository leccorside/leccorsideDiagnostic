using System.IO;
using LeccorWindowsHealthCenter.App;

namespace LeccorWindowsHealthCenter.SystemTests;

public sealed class StartupTraceTests : IDisposable
{
    private readonly string _logs = Path.Combine(Path.GetTempPath(), "lwhc-startup-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_logs, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Summary_lists_phases_in_order_with_process_time()
    {
        using var trace = new StartupTrace(_logs, TimeSpan.FromHours(1));
        trace.Mark("Host construído");
        trace.Mark("Janela criada");

        var summary = trace.Complete();

        Assert.StartsWith("processo→app ", summary, StringComparison.Ordinal);
        Assert.True(summary.IndexOf("Host construído", StringComparison.Ordinal) < summary.IndexOf("Janela criada", StringComparison.Ordinal));
        Assert.Contains("total ", summary, StringComparison.Ordinal);
        Assert.Equal("Janela criada", trace.CurrentPhase);
    }

    [Fact]
    public async Task Watchdog_records_current_phase_while_startup_is_pending_and_stops_after_complete()
    {
        using var trace = new StartupTrace(_logs, TimeSpan.FromMilliseconds(50));
        trace.Mark("Fase lenta simulada");

        // Aguarda o watchdog disparar (com tolerância a concorrência de execução de testes)
        var exists = false;
        for (var i = 0; i < 20; i++)
        {
            if (File.Exists(trace.SlowReportPath))
            {
                exists = true;
                break;
            }

            await Task.Delay(50);
        }

        Assert.True(exists);
        var content = string.Empty;
        for (var i = 0; i < 10; i++)
        {
            try
            {
                using var stream = new FileStream(trace.SlowReportPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                content = await reader.ReadToEndAsync();
                if (content.Contains("Fase lenta simulada", StringComparison.Ordinal))
                {
                    break;
                }
            }
            catch (IOException)
            {
                await Task.Delay(50);
            }
        }

        Assert.Contains("Fase lenta simulada", content, StringComparison.Ordinal);

        trace.Complete();
        await Task.Delay(100);
        var lengthAfterComplete = new FileInfo(trace.SlowReportPath).Length;
        await Task.Delay(300);

        Assert.Equal(lengthAfterComplete, new FileInfo(trace.SlowReportPath).Length);
    }
}
