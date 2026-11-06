using System.Security;
using LeccorWindowsHealthCenter.Infrastructure.Security;
using LeccorWindowsHealthCenter.Shared.Ipc;

namespace LeccorWindowsHealthCenter.UnitTests.Security;

public class ElevatedPolicyTests
{
    private static readonly string System32 = Environment.SystemDirectory;

    [Fact]
    public void ValidateCommandPolicy_Allows_DismCheckHealth()
    {
        var dism = Path.Combine(System32, "dism.exe");
        var args = new[] { "/Online", "/Cleanup-Image", "/CheckHealth" };

        var exception = Record.Exception(() => ElevatedDispatcher.ValidateCommandPolicy(dism, args));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCommandPolicy_Allows_DismScanHealth()
    {
        var dism = Path.Combine(System32, "dism.exe");
        var args = new[] { "/Online", "/Cleanup-Image", "/ScanHealth" };

        var exception = Record.Exception(() => ElevatedDispatcher.ValidateCommandPolicy(dism, args));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCommandPolicy_Rejects_DismRestoreHealth()
    {
        var dism = Path.Combine(System32, "dism.exe");
        var args = new[] { "/Online", "/Cleanup-Image", "/RestoreHealth" };

        var ex = Assert.Throws<SecurityException>(() => ElevatedDispatcher.ValidateCommandPolicy(dism, args));
        Assert.Contains("DISM rejeitados", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCommandPolicy_Allows_SfcVerifyOnly()
    {
        var sfc = Path.Combine(System32, "sfc.exe");
        var args = new[] { "/VERIFYONLY" };

        var exception = Record.Exception(() => ElevatedDispatcher.ValidateCommandPolicy(sfc, args));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCommandPolicy_Rejects_SfcScannow()
    {
        var sfc = Path.Combine(System32, "sfc.exe");
        var args = new[] { "/SCANNOW" };

        var ex = Assert.Throws<SecurityException>(() => ElevatedDispatcher.ValidateCommandPolicy(sfc, args));
        Assert.Contains("SFC rejeitados", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCommandPolicy_Allows_ChkdskDriveOnly()
    {
        var chkdsk = Path.Combine(System32, "chkdsk.exe");
        var args = new[] { "C:" };

        var exception = Record.Exception(() => ElevatedDispatcher.ValidateCommandPolicy(chkdsk, args));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCommandPolicy_Rejects_ChkdskWithFixFlags()
    {
        var chkdsk = Path.Combine(System32, "chkdsk.exe");
        var args = new[] { "C:", "/F" };

        var ex = Assert.Throws<SecurityException>(() => ElevatedDispatcher.ValidateCommandPolicy(chkdsk, args));
        Assert.Contains("CHKDSK rejeitados", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCommandPolicy_Rejects_ExecutablesOutsideSystem32()
    {
        var tempExe = Path.Combine(Path.GetTempPath(), "dism.exe");
        var args = new[] { "/Online", "/Cleanup-Image", "/CheckHealth" };

        var ex = Assert.Throws<SecurityException>(() => ElevatedDispatcher.ValidateCommandPolicy(tempExe, args));
        Assert.Contains("não está no diretório de sistema confiável", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCommandPolicy_Rejects_NonWhitelistedExecutables()
    {
        var cmd = Path.Combine(System32, "cmd.exe");
        var args = new[] { "/c", "dir" };

        var ex = Assert.Throws<SecurityException>(() => ElevatedDispatcher.ValidateCommandPolicy(cmd, args));
        Assert.Contains("não está na lista de operações administrativas permitidas", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCommandPolicy_Rejects_Powershell()
    {
        var powershell = Path.Combine(System32, "WindowsPowerShell\\v1.0\\powershell.exe");
        var args = new[] { "-Command", "Get-Process" };

        Assert.Throws<SecurityException>(() => ElevatedDispatcher.ValidateCommandPolicy(powershell, args));
    }

    [Fact]
    public async Task DispatchAsync_Rejects_CriticalProcessTermination()
    {
        var request = new ElevatedRequest
        {
            Id = Guid.NewGuid(),
            Type = ElevatedOperationType.TerminateProcess,
            Terminate = new TerminateProcessPayload(4, "System", null),
        };

        var response = await ElevatedDispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("crítico do sistema", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DispatchAsync_Handles_PingRequest()
    {
        var request = new ElevatedRequest
        {
            Id = Guid.NewGuid(),
            Type = ElevatedOperationType.Ping,
        };

        var response = await ElevatedDispatcher.DispatchAsync(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(request.Id, response.RequestId);
    }
}
