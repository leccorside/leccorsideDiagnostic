using System.Diagnostics;
using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Infrastructure.Startup;
using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Processes;

/// <summary>
/// Fontes reais desta máquina, somente leitura. O único encerramento testado é de um processo que o próprio teste
/// iniciou (ping no loopback) — nenhum processo do usuário é tocado.
/// </summary>
[Collection(PingProcessesCollection.Name)]
public class ProcessAndStartupSourcesTests
{
    [Fact]
    public void Process_inventory_describes_this_process_with_parent_user_path_and_io()
    {
        var provider = new ProcessInventoryProvider(NullLogger<ProcessInventoryProvider>.Instance, TimeProvider.System);

        var snapshot = provider.Capture();
        var self = Assert.Single(snapshot.Processes, p => p.Pid == Environment.ProcessId);

        Assert.Equal(Environment.ProcessPath, self.ExecutablePath, ignoreCase: true);
        Assert.Equal($"{Environment.UserDomainName}\\{Environment.UserName}", self.User, ignoreCase: true);
        Assert.True(self.ParentPid > 0);
        Assert.NotNull(self.TotalProcessorTime);
        Assert.NotNull(self.StartTime);
        Assert.True(self.IoBytes > 0);
        Assert.True(self.ThreadCount > 0 && self.HandleCount > 0 && self.PrivateBytes > 0);
        Assert.DoesNotContain(snapshot.Processes, p => p.Pid == 0);
    }

    [Fact]
    public void Critical_processes_are_refused_without_touching_them()
    {
        var provider = new ProcessInventoryProvider(NullLogger<ProcessInventoryProvider>.Instance, TimeProvider.System);
        var lsass = Process.GetProcessesByName("lsass").Single();

        Assert.Equal(TerminateOutcome.Protected, provider.Terminate(lsass.Id, "lsass", null));

        // HasExited exige abrir o processo (acesso negado no lsass): confere pela lista do sistema.
        Assert.Contains(Process.GetProcessesByName("lsass"), p => p.Id == lsass.Id);
    }

    [Fact]
    public void Terminate_checks_start_time_so_a_reused_pid_is_never_killed()
    {
        var provider = new ProcessInventoryProvider(NullLogger<ProcessInventoryProvider>.Instance, TimeProvider.System);
        using var ping = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "PING.EXE"), ["-n", "30", "127.0.0.1"]) { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            var start = new DateTimeOffset(ping.StartTime.ToUniversalTime(), TimeSpan.Zero);

            Assert.Equal(TerminateOutcome.NotFound, provider.Terminate(ping.Id, "PING", start.AddMinutes(-5)));
            Assert.False(ping.HasExited);

            Assert.Equal(TerminateOutcome.Ended, provider.Terminate(ping.Id, "PING", start));
            Assert.True(ping.WaitForExit(5000));
        }
        finally
        {
            if (!ping.HasExited)
            {
                ping.Kill();
            }
        }
    }

    [Fact]
    public async Task File_identity_tells_catalog_signed_windows_files_from_unsigned_ones()
    {
        using var provider = new FileIdentityProvider(NullLogger<FileIdentityProvider>.Instance);

        var notepad = await provider.GetAsync(Path.Combine(Environment.SystemDirectory, "notepad.exe"), CancellationToken.None);
        Assert.True(notepad.Signature is SignatureStatus.Valid or SignatureStatus.SignedByCatalog, notepad.Signature.ToString());
        Assert.Contains("Microsoft", notepad.Publisher, StringComparison.OrdinalIgnoreCase);

        using var temp = new TempDirectory();
        var unsigned = temp.File("nao-assinado.exe");
        await File.WriteAllBytesAsync(unsigned, [0x4D, 0x5A, 0, 0]);
        Assert.Equal(SignatureStatus.NoSignature, (await provider.GetAsync(unsigned, CancellationToken.None)).Signature);

        Assert.False((await provider.GetAsync(temp.File("nao-existe.exe"), CancellationToken.None)).Exists);
    }

    [Fact]
    public async Task Startup_inventory_reads_registry_tasks_and_services_without_admin()
    {
        using var provider = new StartupInventoryProvider(NullLogger<StartupInventoryProvider>.Instance, TimeProvider.System);

        var inventory = await provider.GetAsync(CancellationToken.None);

        Assert.True(inventory.Warnings.Count == 0, string.Join(" | ", inventory.Warnings));
        Assert.Contains(inventory.Entries, e => e.Source == StartupSourceKind.ScheduledTask);
        Assert.Contains(inventory.Entries, e => e.Source == StartupSourceKind.Service && e.IsWindowsComponent);
        Assert.All(inventory.Entries.Where(e => e.ExecutablePath is not null), e => Assert.NotNull(e.ExecutableExists));
        Assert.Equal(inventory.Entries.Count, inventory.Entries.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
