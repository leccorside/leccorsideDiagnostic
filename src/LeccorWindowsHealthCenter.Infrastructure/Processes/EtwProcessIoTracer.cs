using System.Collections.Concurrent;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Processes;

/// <summary>
/// Provedor ETW (Event Tracing for Windows) de kernel para separação de bytes de E/S de disco e rede (Pacote A).
/// Requer elevação administrativa para iniciar a sessão de rastreamento do kernel do Windows.
/// Em modo não elevado ou caso outra sessão de kernel esteja ativa, degrada graciosamente sem afetar o app.
/// </summary>
public sealed class EtwProcessIoTracer : IProcessIoTracer
{
    private const string SessionName = "LeccorWindowsHealthCenter_IoTrace";
    private readonly ILogger<EtwProcessIoTracer> _logger;
    private readonly ConcurrentDictionary<int, IoAccumulator> _counters = new();
    private TraceEventSession? _session;
    private Task? _processingTask;
    private bool _disposed;

    private sealed class IoAccumulator
    {
        private long _diskBytes;
        private long _networkBytes;

        public void AddDisk(long bytes) => Interlocked.Add(ref _diskBytes, bytes);

        public void AddNetwork(long bytes) => Interlocked.Add(ref _networkBytes, bytes);

        public ProcessIoBreakdown ToBreakdown() =>
            new(Interlocked.Read(ref _diskBytes), Interlocked.Read(ref _networkBytes));
    }

    public EtwProcessIoTracer(ILogger<EtwProcessIoTracer> logger)
    {
        _logger = logger;
    }

    public bool IsActive => _session is not null && !_disposed;

    public bool Start()
    {
        if (_disposed || IsActive)
        {
            return IsActive;
        }

        try
        {
            if (!TraceEventSession.IsElevated() ?? false)
            {
                _logger.LogDebug("Rastreamento ETW de E/S requer privilégios de administrador: desativado.");
                return false;
            }

            // Garante que uma sessão anterior com o mesmo nome seja limpa
            using (var existing = new TraceEventSession(SessionName))
            {
                existing.Stop();
            }

            var session = new TraceEventSession(SessionName, TraceEventSessionOptions.Create)
            {
                StopOnDispose = true,
            };

            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.DiskIO |
                KernelTraceEventParser.Keywords.NetworkTCPIP);

            session.Source.Kernel.DiskIORead += data =>
            {
                if (data.ProcessID > 0)
                {
                    _counters.GetOrAdd(data.ProcessID, _ => new IoAccumulator()).AddDisk(data.TransferSize);
                }
            };

            session.Source.Kernel.DiskIOWrite += data =>
            {
                if (data.ProcessID > 0)
                {
                    _counters.GetOrAdd(data.ProcessID, _ => new IoAccumulator()).AddDisk(data.TransferSize);
                }
            };

            session.Source.Kernel.TcpIpSend += data =>
            {
                if (data.ProcessID > 0)
                {
                    _counters.GetOrAdd(data.ProcessID, _ => new IoAccumulator()).AddNetwork(data.size);
                }
            };

            session.Source.Kernel.TcpIpRecv += data =>
            {
                if (data.ProcessID > 0)
                {
                    _counters.GetOrAdd(data.ProcessID, _ => new IoAccumulator()).AddNetwork(data.size);
                }
            };

            session.Source.Kernel.UdpIpSend += data =>
            {
                if (data.ProcessID > 0)
                {
                    _counters.GetOrAdd(data.ProcessID, _ => new IoAccumulator()).AddNetwork(data.size);
                }
            };

            session.Source.Kernel.UdpIpRecv += data =>
            {
                if (data.ProcessID > 0)
                {
                    _counters.GetOrAdd(data.ProcessID, _ => new IoAccumulator()).AddNetwork(data.size);
                }
            };

            _session = session;
            _processingTask = Task.Run(() =>
            {
                try
                {
                    session.Source.Process();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Loop de eventos da sessão ETW foi interrompido.");
                }
            });

            _logger.LogInformation("Sessão ETW '{SessionName}' iniciada com sucesso.", SessionName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Não foi possível iniciar a sessão ETW de E/S: {Message}", ex.Message);
            _session?.Dispose();
            _session = null;
            return false;
        }
    }

    public void Stop()
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            _session.Stop();
            _session.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao encerrar sessão ETW.");
        }
        finally
        {
            _session = null;
        }
    }

    public ProcessIoBreakdown? GetProcessIo(int processId)
    {
        if (_counters.TryGetValue(processId, out var accumulator))
        {
            return accumulator.ToBreakdown();
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _counters.Clear();
    }
}

