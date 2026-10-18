using System.Diagnostics;
using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Infrastructure.Commands;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Commands;

/// <summary>
/// Executa apenas ferramentas inofensivas e somente-leitura (hostname, ping no loopback).
/// Nenhum comando que altere o sistema é executado em testes (seção 50).
/// </summary>
public sealed class CommandExecutorTests : IDisposable
{
    private static readonly string System32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
    private readonly CommandExecutor _executor = new(new ExecutablePolicy(), new NotElevated(), NullLogger<CommandExecutor>.Instance);

    public void Dispose() => _executor.Dispose();

    [Fact]
    public async Task Captures_stdout_and_exit_code()
    {
        var result = await _executor.ExecuteAsync(new CommandRequest { ExecutablePath = Path.Combine(System32, "hostname.exe") }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Environment.MachineName, result.StandardOutput.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Non_zero_exit_code_is_reported_not_thrown()
    {
        // "hostname -x" é uma opção inválida: exit code ≠ 0 sem efeitos colaterais.
        var result = await _executor.ExecuteAsync(
            new CommandRequest { ExecutablePath = Path.Combine(System32, "hostname.exe"), Arguments = ["-x"] }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Timeout_kills_the_process_tree()
    {
        var before = Process.GetProcessesByName("PING").Select(p => p.Id).ToHashSet();
        var request = new CommandRequest
        {
            ExecutablePath = Path.Combine(System32, "PING.EXE"),
            Arguments = ["-n", "30", "127.0.0.1"],
            Timeout = TimeSpan.FromMilliseconds(500),
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await _executor.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.Null(result.ExitCode);

        // Sem o timeout, "ping -n 30" levaria ~29 s. Margem larga: sob carga pesada (suítes em paralelo)
        // a criação do processo pode demorar segundos — o que importa é não esperar o comando terminar.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"Demorou {stopwatch.Elapsed.TotalSeconds:0.0} s");

        // O encerramento da árvore é assíncrono no SO: aguarda até 5 s em vez de um atraso fixo.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Process.GetProcessesByName("PING").Any(p => !before.Contains(p.Id)) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.DoesNotContain(Process.GetProcessesByName("PING"), p => !before.Contains(p.Id));
    }

    [Fact]
    public async Task Cancellation_stops_the_command()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var request = new CommandRequest { ExecutablePath = Path.Combine(System32, "PING.EXE"), Arguments = ["-n", "30", "127.0.0.1"] };

        var result = await _executor.ExecuteAsync(request, cts.Token);

        Assert.True(result.Cancelled);
        Assert.False(result.TimedOut);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Rejects_interpreters_before_starting_anything()
    {
        await Assert.ThrowsAsync<CommandRejectedException>(() => _executor.ExecuteAsync(
            new CommandRequest { ExecutablePath = Path.Combine(System32, "cmd.exe"), Arguments = ["/c", "echo", "x"] }, CancellationToken.None));
    }

    [Fact]
    public async Task Elevated_command_is_rejected_when_not_elevated()
    {
        await Assert.ThrowsAsync<CommandRejectedException>(() => _executor.ExecuteAsync(
            new CommandRequest { ExecutablePath = Path.Combine(System32, "hostname.exe"), RequiresElevation = true }, CancellationToken.None));
    }

    [Fact]
    public async Task Output_is_truncated_at_the_configured_limit()
    {
        var request = new CommandRequest
        {
            ExecutablePath = Path.Combine(System32, "PING.EXE"),
            Arguments = ["-n", "2", "127.0.0.1"],
            MaxOutputChars = 20,
        };

        var result = await _executor.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.OutputTruncated);
        Assert.Equal(20, result.StandardOutput.Length);
    }

    private sealed class NotElevated : IPrivilegeService
    {
        public bool IsElevated => false;

        public string CurrentUser => @"PC\teste";
    }
}
