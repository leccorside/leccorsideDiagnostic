using System.Diagnostics;
using System.Text;
using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Shared.Security;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Commands;

/// <summary>
/// Implementação do <see cref="ICommandExecutor"/> (seção 23): validação de política, ArgumentList
/// (sem concatenação), timeout, cancelamento, captura limitada de stdout/stderr, exit code,
/// logging com redação de segredos e encerramento da árvore de processos.
/// </summary>
public sealed class CommandExecutor : ICommandExecutor, IDisposable
{
    private readonly ExecutablePolicy _policy;
    private readonly IPrivilegeService _privileges;
    private readonly ILogger<CommandExecutor> _logger;
    private readonly Lazy<JobObject?> _job;

    public CommandExecutor(ExecutablePolicy policy, IPrivilegeService privileges, ILogger<CommandExecutor> logger)
    {
        _policy = policy;
        _privileges = privileges;
        _logger = logger;
        _job = new Lazy<JobObject?>(CreateJob);
    }

    public async Task<CommandResult> ExecuteAsync(CommandRequest request, CancellationToken cancellationToken)
    {
        _policy.Validate(request);

        if (request.RequiresElevation && !_privileges.IsElevated)
        {
            throw new CommandRejectedException(
                "Este comando exige privilégios de administrador. Operações administrativas são delegadas ao processo elevado dedicado.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var commandLine = SensitiveDataRedactor.Redact(FormatCommandLine(request));
        var startInfo = new ProcessStartInfo(request.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            WorkingDirectory = Path.GetDirectoryName(request.ExecutablePath)!,
        };
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _logger.LogInformation("Executando comando {CommandLine} ({Purpose})", commandLine, request.Purpose ?? "-");
        var stopwatch = Stopwatch.StartNew();

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Não foi possível iniciar '{request.ExecutablePath}'.");
        }

        _job.Value?.TryAssign(process);

        var stdoutTask = ReadCappedAsync(process.StandardOutput, request.MaxOutputChars);
        var stderrTask = ReadCappedAsync(process.StandardError, request.MaxOutputChars);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(request.Timeout);

        var timedOut = false;
        var cancelled = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = cancellationToken.IsCancellationRequested;
            timedOut = !cancelled;
            KillTree(process);
        }

        var (stdout, stdoutTruncated) = await stdoutTask.ConfigureAwait(false);
        var (stderr, stderrTruncated) = await stderrTask.ConfigureAwait(false);
        stopwatch.Stop();

        int? exitCode = process.HasExited && !timedOut && !cancelled ? process.ExitCode : null;
        var result = new CommandResult
        {
            CommandLine = commandLine,
            ExitCode = exitCode,
            StandardOutput = SensitiveDataRedactor.Redact(stdout),
            StandardError = SensitiveDataRedactor.Redact(stderr),
            Duration = stopwatch.Elapsed,
            TimedOut = timedOut,
            Cancelled = cancelled,
            OutputTruncated = stdoutTruncated || stderrTruncated,
        };

        if (timedOut)
        {
            _logger.LogWarning("Comando {CommandLine} excedeu o timeout de {Timeout} e foi encerrado", commandLine, request.Timeout);
        }
        else if (cancelled)
        {
            _logger.LogWarning("Comando {CommandLine} cancelado pelo usuário após {DurationMs} ms", commandLine, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogInformation("Comando {CommandLine} terminou com exit code {ExitCode} em {DurationMs} ms", commandLine, exitCode, stopwatch.ElapsedMilliseconds);
        }

        return result;
    }

    public void Dispose()
    {
        if (_job.IsValueCreated)
        {
            _job.Value?.Dispose();
        }
    }

    internal static string FormatCommandLine(CommandRequest request)
    {
        static string Quote(string value) =>
            value.Length == 0 || value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;

        var builder = new StringBuilder(Quote(request.ExecutablePath));
        foreach (var argument in request.Arguments)
        {
            builder.Append(' ').Append(Quote(argument));
        }

        return builder.ToString();
    }

    private static async Task<(string Text, bool Truncated)> ReadCappedAsync(StreamReader reader, int maxChars)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            var remaining = maxChars - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(read, remaining));
            }

            // Continua drenando o pipe mesmo após o limite para o processo não travar.
            truncated |= read > remaining;
        }

        return (builder.ToString(), truncated);
    }

    private void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Falha ao encerrar a árvore do processo {Pid}", process.Id);
        }
    }

    private JobObject? CreateJob()
    {
        try
        {
            return new JobObject();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Job Object indisponível; processos filhos serão encerrados apenas via Kill");
            return null;
        }
    }
}
