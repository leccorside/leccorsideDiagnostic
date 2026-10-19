using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.Startup;

/// <summary>
/// Inicialização (seção 19), somente leitura e sem administrador: Run/RunOnce (usuário, máquina e 32 bits), estado de
/// "Desativado" do Gerenciador de Tarefas (StartupApproved), pastas Inicializar, tarefas agendadas de logon/boot e
/// serviços automáticos. Cada fonte é isolada: uma falha vira aviso e as demais seguem.
/// </summary>
public sealed class StartupInventoryProvider : IStartupInventoryProvider, IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string Wow64RunKey = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    // Tipos de gatilho e ação do Agendador de Tarefas (TASK_TRIGGER_TYPE2 / TASK_ACTION_TYPE).
    private const int TriggerBoot = 8;
    private const int TriggerLogon = 9;
    private const int ActionExec = 0;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly ILogger<StartupInventoryProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private StartupInventory? _cached;

    public StartupInventoryProvider(ILogger<StartupInventoryProvider> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public void Dispose() => _gate.Dispose();

    public async Task<StartupInventory> GetAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _cached is not null && _timeProvider.GetUtcNow() - _cached.CapturedAt < CacheDuration)
            {
                return _cached;
            }

            var started = _timeProvider.GetTimestamp();
            _cached = await Task.Run(() => Collect(cancellationToken), cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Inicialização lida em {DurationMs} ms ({Entries} entradas, {Warnings} avisos)",
                (long)_timeProvider.GetElapsedTime(started).TotalMilliseconds, _cached.Entries.Count, _cached.Warnings.Count);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private StartupInventory Collect(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var entries = new List<StartupEntry>();
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

        Run(() => ReadRegistry(entries, Registry.CurrentUser, "HKCU", RunKey, StartupSourceKind.RegistryRun, true, Registry.CurrentUser, "Run"), "Registro HKCU Run", warnings);
        Run(() => ReadRegistry(entries, Registry.CurrentUser, "HKCU", RunOnceKey, StartupSourceKind.RegistryRunOnce, true, null, null), "Registro HKCU RunOnce", warnings);
        Run(() => ReadRegistry(entries, machine, "HKLM", RunKey, StartupSourceKind.RegistryRun, false, machine, "Run"), "Registro HKLM Run", warnings);
        Run(() => ReadRegistry(entries, machine, "HKLM", RunOnceKey, StartupSourceKind.RegistryRunOnce, false, null, null), "Registro HKLM RunOnce", warnings);
        Run(() => ReadRegistry(entries, machine, "HKLM", Wow64RunKey, StartupSourceKind.RegistryRun, false, machine, "Run32"), "Registro HKLM Run (32 bits)", warnings);
        cancellationToken.ThrowIfCancellationRequested();

        Run(() => ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), true, Registry.CurrentUser), "Pasta Inicializar do usuário", warnings);
        Run(() => ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), false, machine), "Pasta Inicializar de todos os usuários", warnings);
        cancellationToken.ThrowIfCancellationRequested();

        Run(() => ReadScheduledTasks(entries, cancellationToken), "Tarefas agendadas", warnings);
        Run(() => ReadServices(entries, warnings, cancellationToken), "Serviços", warnings);

        foreach (var warning in warnings)
        {
            _logger.LogWarning("Inicialização incompleta: {Warning}", warning);
        }

        return new StartupInventory { Entries = entries, Warnings = warnings, CapturedAt = _timeProvider.GetUtcNow() };
    }

    private static void Run(Action read, string source, List<string> warnings)
    {
        try
        {
            read();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException
                                       or System.Runtime.InteropServices.COMException or InvalidCastException or InvalidOperationException
                                       or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            warnings.Add($"{source}: {ex.Message}");
        }
    }

    private static void ReadRegistry(List<StartupEntry> entries, RegistryKey hive, string hiveName, string keyPath, StartupSourceKind kind, bool user, RegistryKey? approvedHive, string? approvedSubkey)
    {
        using var key = hive.OpenSubKey(keyPath);
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetValueNames().Where(n => n.Length > 0))
        {
            var command = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
            var disabled = approvedHive is not null && approvedSubkey is not null ? IsDisabled(approvedHive, approvedSubkey, name) : null;
            entries.Add(Build(
                $"{hiveName}\\{keyPath}\\{name}", name, kind, $"{hiveName}\\{keyPath}", user, command,
                enabled: disabled is null ? true : !disabled.Value,
                stateDetail: disabled == true ? "desativado no Gerenciador de Tarefas" : kind == StartupSourceKind.RegistryRunOnce ? "roda uma vez no próximo logon" : null));
        }
    }

    private static void ReadStartupFolder(List<StartupEntry> entries, string folder, bool user, RegistryKey approvedHive)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder).Where(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)))
        {
            var command = Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ? ShortcutCommand(file) : $"\"{file}\"";
            var disabled = IsDisabled(approvedHive, "StartupFolder", Path.GetFileName(file));
            entries.Add(Build(
                $"folder:{file}", Path.GetFileNameWithoutExtension(file), StartupSourceKind.StartupFolder, folder, user, command,
                enabled: disabled != true,
                stateDetail: disabled == true ? "desativado no Gerenciador de Tarefas" : null));
        }
    }

    /// <summary>Destino de um atalho .lnk (WScript.Shell): só lê o atalho, não o executa.</summary>
    private static string? ShortcutCommand(string lnk)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type is null)
        {
            return null;
        }

        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(lnk);
            string target = shortcut.TargetPath;
            string arguments = shortcut.Arguments;
            return string.IsNullOrWhiteSpace(target) ? null : $"\"{target}\" {arguments}".Trim();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    /// <summary>Tarefas com gatilho de logon ou boot e ação "iniciar programa" (Agendador de Tarefas via COM).</summary>
    private static void ReadScheduledTasks(List<StartupEntry> entries, CancellationToken cancellationToken)
    {
        var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new InvalidOperationException("Agendador de Tarefas indisponível");
        dynamic service = Activator.CreateInstance(type)!;
        try
        {
            service.Connect();
            Walk(service.GetFolder("\\"));
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(service);
        }

        void Walk(dynamic folder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (dynamic task in folder.GetTasks(1)) // 1 = TASK_ENUM_HIDDEN: inclui tarefas ocultas
            {
                try
                {
                    var definition = task.Definition;
                    var startsAtLogon = false;
                    foreach (dynamic trigger in definition.Triggers)
                    {
                        int triggerType = trigger.Type;
                        if (triggerType is TriggerLogon or TriggerBoot)
                        {
                            startsAtLogon = true;
                            break;
                        }
                    }

                    if (!startsAtLogon)
                    {
                        continue;
                    }

                    foreach (dynamic action in definition.Actions)
                    {
                        int actionType = action.Type;
                        if (actionType != ActionExec)
                        {
                            continue;
                        }

                        string path = task.Path;
                        string name = task.Name;
                        string exe = action.Path ?? string.Empty;
                        string args = action.Arguments ?? string.Empty;
                        bool enabled = task.Enabled;
                        var command = exe.StartsWith('"') ? $"{exe} {args}" : $"\"{exe.Trim()}\" {args}";
                        entries.Add(Build(
                            $"task:{path}", name, StartupSourceKind.ScheduledTask, path, false, command.Trim(),
                            enabled, enabled ? null : "tarefa desativada",
                            windowsComponent: path.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase)));
                        break; // uma linha por tarefa
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Tarefa sem permissão de leitura: as demais seguem.
                }
            }

            foreach (dynamic sub in folder.GetFolders(0))
            {
                Walk(sub);
            }
        }
    }

    private static void ReadServices(List<StartupEntry> entries, List<string> warnings, CancellationToken cancellationToken)
    {
        foreach (var service in WmiQuery.Query(WmiQuery.Cimv2, "SELECT Name, DisplayName, PathName, StartMode, DelayedAutoStart FROM Win32_Service WHERE StartMode = 'Auto'", warnings, cancellationToken))
        {
            var name = WmiQuery.Text(service, "Name");
            if (name is null)
            {
                continue;
            }

            entries.Add(Build(
                $"service:{name}", WmiQuery.Text(service, "DisplayName") ?? name, StartupSourceKind.Service, name, false,
                WmiQuery.Text(service, "PathName"), enabled: true,
                stateDetail: WmiQuery.Bool(service, "DelayedAutoStart") == true ? "automático (atrasado)" : "automático"));
        }
    }

    /// <summary>StartupApproved: primeiro byte ímpar (0x01, 0x03…) = desativado; par (0x02, 0x06) = ativo.</summary>
    private static bool? IsDisabled(RegistryKey hive, string subkey, string name)
    {
        using var key = hive.OpenSubKey($"{ApprovedKey}\\{subkey}");
        return key?.GetValue(name) is byte[] { Length: > 0 } data ? (data[0] & 1) == 1 : null;
    }

    private static StartupEntry Build(string id, string name, StartupSourceKind kind, string location, bool user, string? command, bool? enabled, string? stateDetail, bool windowsComponent = false)
    {
        var exe = StartupCommand.ExecutablePath(command);
        return new StartupEntry
        {
            Id = id,
            Name = name,
            Source = kind,
            Location = location,
            CurrentUserOnly = user,
            Command = command,
            ExecutablePath = exe,
            ExecutableExists = exe is null ? null : File.Exists(exe),
            Enabled = enabled,
            StateDetail = stateDetail,
            // powershell/cmd/mshta… e drivers no DriverStore não contam como "do Windows": ficariam ocultos por padrão.
            IsWindowsComponent = windowsComponent || StartupCommand.IsWindowsOwned(command),
        };
    }
}
