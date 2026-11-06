using LeccorWindowsHealthCenter.App.ViewModels;
using LeccorWindowsHealthCenter.Application.Crash;
using LeccorWindowsHealthCenter.Application.DevTools;
using LeccorWindowsHealthCenter.Application.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.SystemTests.ViewModels;

public class DevToolsAndSecurityViewModelTests
{
    [Fact]
    public async Task DevToolsViewModel_loads_tools_and_wsl_correctly()
    {
        var summary = new DevEnvironmentSummary
        {
            Tools =
            [
                new DevToolItem { Id = "git", Name = "Git", Category = "VCS", IsInstalled = true, Version = "2.40" }
            ],
            WslDistributions =
            [
                new WslDistributionInfo { Name = "Ubuntu", WslVersion = 2, State = "Running", IsDefault = true, VhdxSizeBytes = 100 }
            ],
            Docker = new DockerStatusInfo { IsInstalled = true, IsRunning = false, Backend = "WSL2" },
            PathAnalysis = new PathEnvironmentAnalysis { UserEntries = [], SystemEntries = [], InvalidEntries = [], TotalLength = 100 },
            HasHyperV = false,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var provider = new TestDevToolsProvider(summary);
        var vm = new DevToolsViewModel(provider, NullLogger<DevToolsViewModel>.Instance);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Single(vm.Tools);
        Assert.Equal("Git", vm.Tools[0].Name);
        Assert.Single(vm.WslDistributions);
        Assert.Equal("Ubuntu", vm.WslDistributions[0].Name);
        Assert.NotNull(vm.Docker);
        Assert.False(vm.Docker.IsRunning);
    }

    [Fact]
    public async Task SecurityViewModel_loads_security_and_crash_correctly()
    {
        var secSummary = new SecurityStatusSummary
        {
            AntivirusProducts =
            [
                new AntivirusProductInfo { Name = "Windows Defender", InstanceGuid = "{1}", PathToSignedProductExe = "C:\\def.exe", IsEnabled = true, IsUpToDate = true, IsWindowsDefender = true }
            ],
            FirewallProducts = [],
            IsUacEnabled = true,
            IsRealTimeProtectionActive = true,
            IsTamperProtectionActive = true,
            SuspiciousPersistences = [],
            CapturedAt = DateTimeOffset.UtcNow
        };

        var crashSummary = new CrashSummary
        {
            DumpFiles = [],
            TopCrashingApps = [],
            TotalKernelDumpsCount = 0,
            TotalAppDumpsCount = 0,
            TotalBsodEventsPast30Days = 0,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var secProvider = new TestSecurityProvider(secSummary);
        var crashProvider = new TestCrashProvider(crashSummary);
        var vm = new SecurityViewModel(secProvider, crashProvider, NullLogger<SecurityViewModel>.Instance);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Single(vm.AntivirusProducts);
        Assert.Equal("Windows Defender", vm.AntivirusProducts[0].Name);
        Assert.NotNull(vm.Security);
        Assert.True(vm.Security.IsUacEnabled);
    }

    private sealed class TestDevToolsProvider : IDevToolsProvider
    {
        private readonly DevEnvironmentSummary _summary;

        public TestDevToolsProvider(DevEnvironmentSummary summary) => _summary = summary;

        public Task<DevEnvironmentSummary> GetDevEnvironmentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_summary);
    }

    private sealed class TestSecurityProvider : ISecurityProvider
    {
        private readonly SecurityStatusSummary _summary;

        public TestSecurityProvider(SecurityStatusSummary summary) => _summary = summary;

        public Task<SecurityStatusSummary> GetSecurityStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_summary);
    }

    private sealed class TestCrashProvider : ICrashAnalyzerProvider
    {
        private readonly CrashSummary _summary;

        public TestCrashProvider(CrashSummary summary) => _summary = summary;

        public Task<CrashSummary> GetCrashSummaryAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_summary);
    }
}

