using System.Globalization;
using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.Scheduling;
using LeccorWindowsHealthCenter.Application.Settings;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Scheduling;

/// <summary>
/// Implementa o agendamento de varreduras utilizando o utilitário nativo schtasks.exe do Windows (seção 60).
/// </summary>
public sealed class WindowsTaskSchedulerService : IScanSchedulerService
{
    public const string TaskName = "LeccorWindowsHealthCenterScan";
    private const string ConfigKey = "schedule.frequency";

    private readonly ICommandExecutor _commandExecutor;
    private readonly IConfigurationRepository _configurationRepository;
    private readonly ILogger<WindowsTaskSchedulerService> _logger;

    public WindowsTaskSchedulerService(
        ICommandExecutor commandExecutor,
        IConfigurationRepository configurationRepository,
        ILogger<WindowsTaskSchedulerService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScheduleStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var savedFreqStr = await _configurationRepository.GetValueAsync(ConfigKey, cancellationToken).ConfigureAwait(false);
        var frequency = ScheduledScanFrequency.Disabled;
        if (!string.IsNullOrEmpty(savedFreqStr) && Enum.TryParse<ScheduledScanFrequency>(savedFreqStr, true, out var parsed))
        {
            frequency = parsed;
        }

        var schtasksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "schtasks.exe");
        if (!File.Exists(schtasksPath))
        {
            return new ScheduleStatus
            {
                IsConfigured = false,
                Frequency = ScheduledScanFrequency.Disabled,
                Details = "schtasks.exe não encontrado"
            };
        }

        try
        {
            var request = new CommandRequest
            {
                ExecutablePath = schtasksPath,
                Arguments = ["/query", "/tn", TaskName, "/fo", "LIST", "/v"],
                Timeout = TimeSpan.FromSeconds(5)
            };

            var result = await _commandExecutor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode == 0)
            {
                var nextRun = ExtractValue(result.StandardOutput, "Próxima Hora de Execução", "Next Run Time");
                return new ScheduleStatus
                {
                    IsConfigured = true,
                    Frequency = frequency == ScheduledScanFrequency.Disabled ? ScheduledScanFrequency.Weekly : frequency,
                    TaskName = TaskName,
                    NextRun = nextRun,
                    Details = "Tarefa agendada ativa no Windows Task Scheduler."
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar status da tarefa agendada {TaskName}", TaskName);
        }

        return new ScheduleStatus
        {
            IsConfigured = false,
            Frequency = ScheduledScanFrequency.Disabled,
            Details = "Nenhuma varredura periódica configurada."
        };
    }

    public async Task ConfigureScheduleAsync(ScheduledScanFrequency frequency, CancellationToken cancellationToken = default)
    {
        if (frequency == ScheduledScanFrequency.Disabled)
        {
            await DisableScheduleAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var schtasksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "schtasks.exe");
        var appExePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "LeccorWindowsHealthCenter.exe");

        var scArg = frequency switch
        {
            ScheduledScanFrequency.Daily => "DAILY",
            ScheduledScanFrequency.Weekly => "WEEKLY",
            ScheduledScanFrequency.Monthly => "MONTHLY",
            _ => "WEEKLY"
        };

        var args = new List<string>
        {
            "/create",
            "/tn", TaskName,
            "/tr", $"\"{appExePath}\" --scheduled-scan",
            "/sc", scArg,
            "/st", "12:00",
            "/f"
        };

        if (frequency == ScheduledScanFrequency.Weekly)
        {
            args.Add("/d");
            args.Add("MON");
        }
        else if (frequency == ScheduledScanFrequency.Monthly)
        {
            args.Add("/d");
            args.Add("1");
        }

        try
        {
            var request = new CommandRequest
            {
                ExecutablePath = schtasksPath,
                Arguments = args,
                Timeout = TimeSpan.FromSeconds(10)
            };

            var result = await _commandExecutor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                _logger.LogWarning("schtasks retornou código {ExitCode}: {Error}", result.ExitCode, result.StandardError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar tarefa agendada no Windows");
        }

        await _configurationRepository.SetValueAsync(ConfigKey, frequency.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task DisableScheduleAsync(CancellationToken cancellationToken = default)
    {
        var schtasksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "schtasks.exe");

        try
        {
            var request = new CommandRequest
            {
                ExecutablePath = schtasksPath,
                Arguments = ["/delete", "/tn", TaskName, "/f"],
                Timeout = TimeSpan.FromSeconds(5)
            };

            await _commandExecutor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Tarefa agendada {TaskName} já não existia ou falhou ao ser removida", TaskName);
        }

        await _configurationRepository.SetValueAsync(ConfigKey, ScheduledScanFrequency.Disabled.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private static string? ExtractValue(string output, params string[] fieldNames)
    {
        if (string.IsNullOrEmpty(output))
        {
            return null;
        }

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            foreach (var field in fieldNames)
            {
                if (line.StartsWith(field, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        return parts[1].Trim();
                    }
                }
            }
        }

        return null;
    }
}

