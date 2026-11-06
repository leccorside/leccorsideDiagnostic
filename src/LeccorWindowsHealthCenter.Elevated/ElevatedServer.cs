using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using LeccorWindowsHealthCenter.Infrastructure.Security;
using LeccorWindowsHealthCenter.Shared.Ipc;
using Microsoft.Win32.SafeHandles;

namespace LeccorWindowsHealthCenter.Elevated;

public sealed class ElevatedServer : IDisposable
{
    private readonly string _pipeName;
    private readonly string _token;
    private readonly int _parentPid;
    private readonly CancellationTokenSource _cts = new();

    public ElevatedServer(string pipeName, string token, int parentPid)
    {
        _pipeName = pipeName;
        _token = token;
        _parentPid = parentPid;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    public async Task RunAsync()
    {
        StartParentWatcher();

        try
        {
            var pipeSecurity = CreatePipeSecurity();
            using var serverStream = NamedPipeServerStreamAcl.Create(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0,
                pipeSecurity);

            using var idleCts = new CancellationTokenSource(TimeSpan.FromSeconds(IpcConstants.DefaultIdleTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, idleCts.Token);

            await serverStream.WaitForConnectionAsync(linkedCts.Token).ConfigureAwait(false);

            // Validar PID do cliente via API do Windows
            if (!GetNamedPipeClientProcessId(serverStream.SafePipeHandle, out var clientPid) || clientPid != _parentPid)
            {
                serverStream.Disconnect();
                return;
            }

            using var reader = new StreamReader(serverStream, leaveOpen: true);
            using var writer = new StreamWriter(serverStream, leaveOpen: true) { AutoFlush = true };

            // Handshake de autenticação
            var handshakeLine = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(handshakeLine))
            {
                return;
            }

            var handshake = JsonSerializer.Deserialize<IpcHandshakeRequest>(handshakeLine);
            if (handshake is null || handshake.Token != _token || handshake.ClientProcessId != _parentPid)
            {
                var failResponse = new IpcHandshakeResponse(false, "Token de autenticação IPC inválido.");
                await writer.WriteLineAsync(JsonSerializer.Serialize(failResponse)).ConfigureAwait(false);
                return;
            }

            var okResponse = new IpcHandshakeResponse(true, null);
            await writer.WriteLineAsync(JsonSerializer.Serialize(okResponse)).ConfigureAwait(false);

            // Loop de processamento de comandos com timeout de ociosidade
            while (!_cts.IsCancellationRequested && serverStream.IsConnected)
            {
                using var requestIdleCts = new CancellationTokenSource(TimeSpan.FromSeconds(IpcConstants.DefaultIdleTimeoutSeconds));
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, requestIdleCts.Token);

                string? line;
                try
                {
                    line = await reader.ReadLineAsync(requestCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Timeout de ociosidade alcançado
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                var request = JsonSerializer.Deserialize<ElevatedRequest>(line);
                if (request is null)
                {
                    continue;
                }

                if (request.Type == ElevatedOperationType.Shutdown)
                {
                    var shutdownResp = new ElevatedResponse { RequestId = request.Id, Success = true };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(shutdownResp)).ConfigureAwait(false);
                    break;
                }

                var response = await ElevatedDispatcher.DispatchAsync(request, _cts.Token).ConfigureAwait(false);
                var responseJson = JsonSerializer.Serialize(response);
                await writer.WriteLineAsync(responseJson).ConfigureAwait(false);
            }
        }
        catch
        {
            // Encerra silenciosamente em caso de erro de conexão ou cancelamento
        }
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        var ps = new PipeSecurity();
        using var currentIdentity = WindowsIdentity.GetCurrent();
        if (currentIdentity.User is not null)
        {
            ps.AddAccessRule(new PipeAccessRule(currentIdentity.User, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        }

        ps.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        ps.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return ps;
    }

    private void StartParentWatcher()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var parent = Process.GetProcessById(_parentPid);
                await parent.WaitForExitAsync(_cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Processo pai não existe mais ou erro ao monitorar
            }
            finally
            {
                _cts.Cancel();
            }
        });
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
