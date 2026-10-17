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

        await Task.Delay(400);
        Assert.True(File.Exists(trace.SlowReportPath));
        Assert.Contains("Fase lenta simulada", await File.ReadAllTextAsync(trace.SlowReportPath), StringComparison.Ordinal);

        trace.Complete();
        await Task.Delay(100);
        var lengthAfterComplete = new FileInfo(trace.SlowReportPath).Length;
        await Task.Delay(300);

        Assert.Equal(lengthAfterComplete, new FileInfo(trace.SlowReportPath).Length);
    }
}
