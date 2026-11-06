using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Infrastructure.Processes;
using LeccorWindowsHealthCenter.Infrastructure.Startup;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LeccorWindowsHealthCenter.UnitTests.Performance;

public class PacoteATelemetryTests
{
    [Fact]
    public void FileIdentity_RevokedStatus_RendersExpectedText()
    {
        var identity = new FileIdentity
        {
            Path = @"C:\Apps\tool.dll",
            Exists = true,
            Signature = SignatureStatus.Revoked,
            Signer = "Compromised CA",
        };

        Assert.Equal("Assinatura revogada", identity.SignatureText);
        Assert.Equal(SignatureStatus.Revoked, identity.Signature);
    }

    [Fact]
    public async Task BootPerformanceProvider_WhenQueriedInUnitTest_GracefullyReturnsMapWithoutThrowing()
    {
        var provider = new BootPerformanceProvider(NullLogger<BootPerformanceProvider>.Instance);

        // Não deve lançar exceção mesmo se não elevado ou sem eventos
        var result = await provider.GetRecentBootDegradationsAsync(CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public void EtwProcessIoTracer_Lifecycle_CanBeInstantiatedAndDisposedSafely()
    {
        using var tracer = new EtwProcessIoTracer(NullLogger<EtwProcessIoTracer>.Instance);

        Assert.Null(tracer.GetProcessIo(999999));
        tracer.Stop();
        Assert.False(tracer.IsActive);
    }

    [Fact]
    public void ProcessIoBreakdown_StoresDiskAndNetworkBytes()
    {
        var breakdown = new ProcessIoBreakdown(1024, 2048);

        Assert.Equal(1024, breakdown.DiskIoBytes);
        Assert.Equal(2048, breakdown.NetworkIoBytes);
    }

    [Fact]
    public void BootDegradationRecord_Properties_ArePreserved()
    {
        var now = DateTimeOffset.UtcNow;
        var record = new BootDegradationRecord
        {
            Target = @"C:\Program Files\App\app.exe",
            DegradationTimeMs = 3200,
            TotalTimeMs = 5400,
            EventId = 101,
            Timestamp = now,
        };

        Assert.Equal(@"C:\Program Files\App\app.exe", record.Target);
        Assert.Equal(3200, record.DegradationTimeMs);
        Assert.Equal(5400, record.TotalTimeMs);
        Assert.Equal(101, record.EventId);
        Assert.Equal(now, record.Timestamp);
    }
}

