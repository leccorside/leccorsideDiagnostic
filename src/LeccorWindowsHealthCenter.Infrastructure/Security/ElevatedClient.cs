using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.Security;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Shared.Ipc;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Security;

/// <summary>
/// Cliente IPC que orquestra a comunicação segura com o <c>LeccorWindowsHealthCenter.Elevated.exe</c>
/// via Named Pipe com autenticação por token e controle de ciclo de vida (seção 64 do PROMPT).
/// Se o processo atual já estiver com privilégios de administrador, executa diretamente in-process.
/// </summary>
public sealed class ElevatedClient : IElevatedClient, IAsyncDisposable, IDisposable
{
    private readonly IPrivilegeService _privileges;
    private readonly ILogger<ElevatedClient> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private NamedPipeClientStream? _pipeStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Process? _elevatedProcess;

    public ElevatedClient(IPrivilegeService privileges, ILogger<ElevatedClient> logger)
    {
        _privileges = privileges;
        _logger = logger;
    }

    public bool IsElevatedOrAvailable => _privileges.IsElevated || File.Exists(GetElevatedExecutablePath());

    public async Task<CommandResult> ExecuteCommandAsync(CommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Se o processo atual já tiver privilégios de administrador, despacha diretamente
        if (_privileges.IsElevated)
        {
            var elevatedReq = new ElevatedRequest
            {
                Type = ElevatedOperationType.ExecuteCommand,
                Command = new ExecuteCommandPayload(
                    request.ExecutablePath,
                    request.Arguments,
                    (int)request.Timeout.TotalSeconds,
                    request.MaxOutputChars,
                    request.OutputEncoding?.WebName),
            };

            var inProcessResp = await ElevatedDispatcher.DispatchAsync(elevatedReq, cancellationToken).ConfigureAwait(false);
            return ToCommandResult(request, inProcessResp);
        }

        var req = new ElevatedRequest
        {
            Type = ElevatedOperationType.ExecuteCommand,
            Command = new ExecuteCommandPayload(
                request.ExecutablePath,
                request.Arguments,
                (int)request.Timeout.TotalSeconds,
                request.MaxOutputChars,
                request.OutputEncoding?.WebName),
        };

        var response = await SendRequestAsync(req, cancellationToken).ConfigureAwait(false);
        return ToCommandResult(request, response);
    }

    public async Task<IReadOnlyList<ElevatedStorageReliabilityDto>> QueryStorageReliabilityAsync(CancellationToken cancellationToken)
    {
        if (_privileges.IsElevated)
        {
            var inProcessReq = new ElevatedRequest { Type = ElevatedOperationType.QueryStorageReliability };
            var inProcessResp = await ElevatedDispatcher.DispatchAsync(inProcessReq, cancellationToken).ConfigureAwait(false);
            return inProcessResp.StorageReliability ?? [];
        }

        var req = new ElevatedRequest { Type = ElevatedOperationType.QueryStorageReliability };
        var response = await SendRequestAsync(req, cancellationToken).ConfigureAwait(false);
        return response.StorageReliability ?? [];
    }

    public async Task<bool> TerminateProcessAsync(int processId, string executableName, DateTimeOffset? expectedStartTime, CancellationToken cancellationToken)
    {
        if (_privileges.IsElevated)
        {
            var inProcessReq = new ElevatedRequest
            {
                Type = ElevatedOperationType.TerminateProcess,
                Terminate = new TerminateProcessPayload(processId, executableName, expectedStartTime),
            };
            var inProcessResp = await ElevatedDispatcher.DispatchAsync(inProcessReq, cancellationToken).ConfigureAwait(false);
            return inProcessResp.ProcessTerminated == true;
        }

        var req = new ElevatedRequest
        {
            Type = ElevatedOperationType.TerminateProcess,
            Terminate = new TerminateProcessPayload(processId, executableName, expectedStartTime),
        };

        var response = await SendRequestAsync(req, cancellationToken).ConfigureAwait(false);
        return response.ProcessTerminated == true;
    }

    private async Task<ElevatedResponse> SendRequestAsync(ElevatedRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            var json = JsonSerializer.Serialize(request);
            await _writer!.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);

            var responseLine = await _reader!.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                throw new InvalidOperationException("O processo elevado encerrou ou desconectou antes de responder.");
            }

            var response = JsonSerializer.Deserialize<ElevatedResponse>(responseLine)
                ?? throw new InvalidOperationException("Resposta inválida recebida do processo elevado.");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na comunicação IPC com o processo elevado");
            ResetConnection();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_pipeStream is { IsConnected: true } && _writer is not null && _reader is not null)
        {
            return;
        }

        ResetConnection();

        var exePath = GetElevatedExecutablePath();
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Executável do processo elevado não encontrado em '{exePath}'.", exePath);
        }

        var pipeName = $"{IpcConstants.PipePrefix}{Guid.NewGuid():N}";
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var psi = new ProcessStartInfo(exePath)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"--pipe {pipeName} --token {token} --parent-pid {Environment.ProcessId}",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            _logger.LogInformation("Iniciando processo elevado {ExePath} via UAC", exePath);
            _elevatedProcess = Process.Start(psi);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED (usuário clicou "Não" no UAC)
        {
            _logger.LogInformation("Solicitação de elevação UAC cancelada pelo usuário");
            throw new ElevationCancelledException();
        }

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(IpcConstants.ConnectTimeoutSeconds));

            await pipe.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);

            var reader = new StreamReader(pipe, leaveOpen: true);
            var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

            // Handshake
            var handshakeReq = new IpcHandshakeRequest(token, Environment.ProcessId);
            await writer.WriteLineAsync(JsonSerializer.Serialize(handshakeReq)).ConfigureAwait(false);

            var handshakeRespLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var handshakeResp = JsonSerializer.Deserialize<IpcHandshakeResponse>(handshakeRespLine ?? string.Empty);

            if (handshakeResp is not { Success: true })
            {
                throw new InvalidOperationException($"Falha no handshake IPC com o processo elevado: {handshakeResp?.ErrorMessage ?? "rejeitado"}");
            }

            _pipeStream = pipe;
            _reader = reader;
            _writer = writer;
        }
        catch
        {
            pipe.Dispose();
            ResetConnection();
            throw;
        }
    }

    private static string GetElevatedExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "LeccorWindowsHealthCenter.Elevated.exe");
    }

    private void ResetConnection()
    {
        try { _reader?.Dispose(); } catch { /* ignore */ }
        try { _writer?.Dispose(); } catch { /* ignore */ }
        try { _pipeStream?.Dispose(); } catch { /* ignore */ }
        try { _elevatedProcess?.Dispose(); } catch { /* ignore */ }

        _reader = null;
        _writer = null;
        _pipeStream = null;
        _elevatedProcess = null;
    }

    private static CommandResult ToCommandResult(CommandRequest request, ElevatedResponse response)
    {
        if (response.CommandResult is not null)
        {
            var res = response.CommandResult;
            return new CommandResult
            {
                CommandLine = res.CommandLine,
                ExitCode = res.ExitCode,
                StandardOutput = res.StandardOutput,
                StandardError = res.StandardError,
                Duration = res.Duration,
                TimedOut = res.TimedOut,
                Cancelled = res.Cancelled,
            };
        }

        return new CommandResult
        {
            CommandLine = $"{request.ExecutablePath} {string.Join(" ", request.Arguments)}",
            ExitCode = response.Success ? 0 : 1,
            StandardOutput = string.Empty,
            StandardError = response.ErrorMessage ?? "Erro desconhecido ao executar comando no processo elevado.",
            Duration = TimeSpan.Zero,
            TimedOut = false,
            Cancelled = false,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_writer is not null && _pipeStream is { IsConnected: true })
        {
            try
            {
                var shutdownReq = new ElevatedRequest { Type = ElevatedOperationType.Shutdown };
                await _writer.WriteLineAsync(JsonSerializer.Serialize(shutdownReq)).ConfigureAwait(false);
            }
            catch { /* ignore */ }
        }

        ResetConnection();
        _lock.Dispose();
    }

    public void Dispose()
    {
        ResetConnection();
        _lock.Dispose();
    }
}
