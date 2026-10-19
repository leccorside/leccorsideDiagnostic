using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public enum ProcessSortColumn
{
    Name,
    Pid,
    Cpu,
    Memory,
    Io,
    Threads,
    Handles,
    Publisher,
}

/// <summary>
/// Linha da lista de processos, atualizada NO LUGAR a cada leitura (trocar a lista inteira perderia seleção e rolagem).
/// Identidade = PID + horário de início (PID reaproveitado é outra linha).
/// </summary>
public sealed partial class ProcessItem : ObservableObject, IPathRow
{
    public ProcessItem(ProcessDetails details)
    {
        Pid = details.Pid;
        StartTime = details.StartTime;
        IsCritical = CriticalProcesses.IsCritical(details.Pid, details.Name);
        Update(details, null);
    }

    public int Pid { get; }

    public DateTimeOffset? StartTime { get; }

    public bool IsCritical { get; }

    public string PidText => Pid.ToString(CultureInfo.CurrentCulture);

    public string? Command => null;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _parentPid;
    [ObservableProperty] private string? _path;
    [ObservableProperty] private string _user = Fmt.NotAvailable;
    [ObservableProperty] private double _cpuValue = -1;
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private long _memoryValue = -1;
    [ObservableProperty] private string _memoryText = "—";
    [ObservableProperty] private double _ioValue = -1;
    [ObservableProperty] private string _ioText = "—";
    [ObservableProperty] private int _threads;
    [ObservableProperty] private int _handles;
    [ObservableProperty] private string _publisher = string.Empty;
    [ObservableProperty] private string _signatureText = string.Empty;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private bool _signatureInvalid;
    [ObservableProperty] private bool _identityLoaded;
    [ObservableProperty] private int _depth;
    [ObservableProperty] private ProcessDetails? _details;

    /// <summary>Recuo do modo árvore (16 px por nível).</summary>
    public System.Windows.Thickness Indent => new(Depth * 16, 0, 0, 0);

    partial void OnDepthChanged(int value) => OnPropertyChanged(nameof(Indent));

    /// <summary>Nome acessível: o leitor de tela lê a linha inteira, não só o nome.</summary>
    public string AccessibleName => $"{Name}, PID {PidText}, CPU {CpuText}, memória {MemoryText}{(IsCritical ? ", processo protegido do Windows" : string.Empty)}";

    public void Update(ProcessDetails details, ProcessRate? rate)
    {
        Details = details;
        Name = details.Name;
        ParentPid = details.ParentPid;
        Path = details.ExecutablePath ?? Path;
        User = details.User ?? Fmt.NotAvailable;
        Threads = details.ThreadCount ?? 0;
        Handles = details.HandleCount ?? 0;
        MemoryValue = details.PrivateBytes ?? -1;
        MemoryText = details.PrivateBytes is { } bytes ? Fmt.Bytes(bytes) : "—";
        CpuValue = rate?.CpuPercent ?? -1;
        CpuText = rate?.CpuPercent switch
        {
            null => "—",
            < 0.05 => "0%",
            < 1 and var small => small.ToString("0.0", CultureInfo.CurrentCulture) + "%",
            var cpu => cpu.Value.ToString("0", CultureInfo.CurrentCulture) + "%",
        };
        IoValue = rate?.IoBytesPerSecond ?? -1;
        IoText = rate?.IoBytesPerSecond switch
        {
            null => "—",
            < 1024 => "0 KB/s",
            var io => $"{Fmt.Bytes(io)}/s",
        };
        OnPropertyChanged(nameof(AccessibleName));
    }

    public void ApplyIdentity(FileIdentity identity)
    {
        Publisher = identity.Publisher ?? (identity.Exists ? "fabricante não informado" : string.Empty);
        SignatureText = identity.SignatureText;
        SignatureInvalid = identity.Signature == SignatureStatus.Invalid;
        Description = identity.Description;
        IdentityLoaded = true;
    }
}

/// <summary>
/// Processos (seção 18): lista ao vivo (2 s, só com a tela visível) com CPU, memória privada, E/S, threads, handles,
/// fabricante e assinatura; ordenação, pesquisa, árvore e detalhes. Encerrar pede confirmação e é recusado para os
/// processos críticos do Windows.
/// </summary>
public sealed partial class ProcessesViewModel : ObservableObject, IPageLifecycle
{
    private readonly IProcessInventoryProvider _inventory;
    private readonly IFileIdentityProvider _identities;
    private readonly IShellLauncher _shell;
    private readonly IFolderPicker _clipboard;
    private readonly ILogger<ProcessesViewModel> _logger;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<(int Pid, DateTimeOffset? Start), ProcessItem> _items = [];
    private readonly Dictionary<string, FileIdentity?> _identityByPath = new(StringComparer.OrdinalIgnoreCase);
    private ProcessInventorySnapshot? _previous;
    private bool _busy;

    public ProcessesViewModel(
        IProcessInventoryProvider inventory,
        IFileIdentityProvider identities,
        IShellLauncher shell,
        IFolderPicker clipboard,
        ILogger<ProcessesViewModel> logger)
    {
        _inventory = inventory;
        _identities = identities;
        _shell = shell;
        _clipboard = clipboard;
        _logger = logger;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public ObservableCollection<ProcessItem> Processes { get; } = [];

    [ObservableProperty] private string _summary = "Lendo processos…";
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private bool _treeMode;
    [ObservableProperty] private ProcessSortColumn _sortColumn = ProcessSortColumn.Cpu;
    [ObservableProperty] private bool _sortDescending = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanEnd), nameof(ProtectionText), nameof(SelectedParent), nameof(SelectedStart), nameof(SelectedCpuTime))]
    [NotifyCanExecuteChangedFor(nameof(RequestEndCommand))]
    private ProcessItem? _selected;

    [ObservableProperty] private bool _confirmingEnd;
    [ObservableProperty] private string _actionMessage = string.Empty;

    public bool HasSelection => Selected is not null;

    public bool CanEnd => Selected is { IsCritical: false };

    public string ProtectionText => Selected is { IsCritical: true } item ? $"Protegido: {CriticalProcesses.Reason(item.Name)}" : string.Empty;

    public string SelectedParent => Selected is null ? string.Empty
        : _items.Values.FirstOrDefault(p => p.Pid == Selected.ParentPid) is { } parent ? $"{parent.Name} (PID {parent.PidText})"
        : Selected.ParentPid > 0 ? $"PID {Selected.ParentPid} (já encerrado)" : Fmt.NotAvailable;

    public string SelectedStart => Fmt.DateTime(Selected?.StartTime);

    public string SelectedCpuTime => Selected?.Details?.TotalProcessorTime is { } cpu ? Fmt.Duration(cpu) : Fmt.NotAvailable;

    partial void OnSearchChanged(string value) => ApplyView();

    partial void OnTreeModeChanged(bool value) => ApplyView();

    partial void OnSelectedChanged(ProcessItem? value)
    {
        ConfirmingEnd = false;
        ActionMessage = string.Empty;
    }

    public void Activate()
    {
        _timer.Start();
        _ = RefreshAsync();
    }

    public void Deactivate()
    {
        _timer.Stop();
        _previous = null; // taxas recomeçam ao voltar (intervalo longo daria médias enganosas)
    }

    [RelayCommand]
    private void SortBy(ProcessSortColumn column)
    {
        if (SortColumn == column)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = column is not (ProcessSortColumn.Name or ProcessSortColumn.Publisher or ProcessSortColumn.Pid);
        }

        TreeMode = false;
        ApplyView();
    }

    [RelayCommand]
    private void Reveal(string? path)
    {
        if (path is not null && !_shell.Reveal(path))
        {
            ActionMessage = "Não foi possível mostrar o arquivo no Explorer (ele pode ter sido movido).";
        }
    }

    [RelayCommand]
    private void ShowProperties(string? path)
    {
        if (path is not null && !_shell.ShowProperties(path))
        {
            ActionMessage = "Não foi possível abrir as propriedades do arquivo.";
        }
    }

    [RelayCommand]
    private void CopyPath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            ActionMessage = _clipboard.CopyToClipboard(path) ? "Caminho copiado." : "A área de transferência está em uso por outro programa.";
        }
    }

    [RelayCommand]
    private void CopyCommand(string? command) => CopyPath(command);

    [RelayCommand(CanExecute = nameof(CanEnd))]
    private void RequestEnd()
    {
        ConfirmingEnd = true;
        ActionMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelEnd() => ConfirmingEnd = false;

    [RelayCommand]
    private async Task ConfirmEnd()
    {
        if (Selected is not { } item)
        {
            return;
        }

        ConfirmingEnd = false;
        var outcome = _inventory.Terminate(item.Pid, item.Name, item.StartTime);
        ActionMessage = outcome switch
        {
            TerminateOutcome.Ended => $"{item.Name} (PID {item.PidText}) foi encerrado.",
            TerminateOutcome.Protected => CriticalProcesses.Reason(item.Name),
            TerminateOutcome.AccessDenied => $"Sem permissão para encerrar {item.Name}: ele pertence a outro usuário, roda como administrador ou é protegido pelo Windows.",
            TerminateOutcome.NotFound => $"{item.Name} já tinha terminado (ou o PID agora é de outro processo). Nada foi encerrado.",
            _ => $"O Windows não encerrou {item.Name}. Veja o log para detalhes.",
        };
        _logger.LogInformation("Pedido de encerramento de {Name} (PID {Pid}): {Outcome}", item.Name, item.Pid, outcome);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            var snapshot = await Task.Run(_inventory.Capture);
            var rates = _previous is null ? [] : ProcessRates.Compute(_previous, snapshot, Environment.ProcessorCount);
            _previous = snapshot;
            Merge(snapshot, rates);
            ApplyView();
            var cpuKnown = snapshot.Processes.Count(p => p.TotalProcessorTime is not null);
            Summary = $"{Fmt.Count(snapshot.Processes.Count, "processo", "processos")} · atualiza a cada 2 s · "
                + (cpuKnown < snapshot.Processes.Count
                    ? $"{Fmt.Count(snapshot.Processes.Count - cpuKnown, "processo protegido sem CPU medida", "processos protegidos sem CPU medida")} (exigem administrador)"
                    : "CPU de todos os processos medida");
            _ = LoadIdentitiesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao ler processos");
            Summary = "Não foi possível ler os processos agora. Tente de novo em instantes.";
        }
        finally
        {
            _busy = false;
        }
    }

    private void Merge(ProcessInventorySnapshot snapshot, Dictionary<int, ProcessRate> rates)
    {
        var alive = new HashSet<(int, DateTimeOffset?)>();
        foreach (var details in snapshot.Processes)
        {
            var key = (details.Pid, details.StartTime);
            alive.Add(key);
            rates.TryGetValue(details.Pid, out var rate);
            if (_items.TryGetValue(key, out var item))
            {
                item.Update(details, rates.ContainsKey(details.Pid) ? rate : null);
            }
            else
            {
                item = new ProcessItem(details);
                _items[key] = item;
                if (details.ExecutablePath is { } path && _identityByPath.TryGetValue(path, out var known) && known is not null)
                {
                    item.ApplyIdentity(known);
                }
            }
        }

        foreach (var gone in _items.Keys.Where(k => !alive.Contains(k)).ToList())
        {
            _items.Remove(gone);
        }
    }

    /// <summary>Fabricante e assinatura em segundo plano (alguns ms por arquivo, com cache por caminho).</summary>
    private async Task LoadIdentitiesAsync()
    {
        var pending = _items.Values.Where(i => !i.IdentityLoaded && i.Path is not null && !_identityByPath.ContainsKey(i.Path!))
            .Select(i => i.Path!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var path in pending)
        {
            _identityByPath[path] = null; // em andamento
            try
            {
                var identity = await _identities.GetAsync(path, CancellationToken.None);
                _identityByPath[path] = identity;
                foreach (var item in _items.Values.Where(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    item.ApplyIdentity(identity);
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "Identidade de {Path} indisponível", path);
            }
        }
    }

    /// <summary>Ordena/filtra sem recriar as linhas: só move as que mudaram de posição (seleção e rolagem ficam).</summary>
    private void ApplyView()
    {
        var term = Search.Trim();
        IEnumerable<ProcessItem> visible = _items.Values;
        if (term.Length > 0)
        {
            visible = visible.Where(p => p.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || p.PidText == term
                || (p.Path?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
                || p.Publisher.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }

        var ordered = TreeMode && term.Length == 0 ? TreeOrder(visible.ToList()) : Sort(visible).ToList();
        if (!TreeMode || term.Length > 0)
        {
            foreach (var item in ordered)
            {
                item.Depth = 0;
            }
        }

        for (var i = Processes.Count - 1; i >= 0; i--)
        {
            if (!ordered.Contains(Processes[i]))
            {
                Processes.RemoveAt(i);
            }
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = Processes.IndexOf(ordered[i]);
            if (current < 0)
            {
                Processes.Insert(i, ordered[i]);
            }
            else if (current != i)
            {
                Processes.Move(current, i);
            }
        }
    }

    private IEnumerable<ProcessItem> Sort(IEnumerable<ProcessItem> items)
    {
        Func<ProcessItem, IComparable> key = SortColumn switch
        {
            ProcessSortColumn.Pid => p => p.Pid,
            ProcessSortColumn.Cpu => p => p.CpuValue,
            ProcessSortColumn.Memory => p => p.MemoryValue,
            ProcessSortColumn.Io => p => p.IoValue,
            ProcessSortColumn.Threads => p => p.Threads,
            ProcessSortColumn.Handles => p => p.Handles,
            ProcessSortColumn.Publisher => p => p.Publisher,
            _ => p => p.Name.ToUpperInvariant(),
        };

        // Desempate pelo nome e PID: a ordem não "pisca" entre leituras quando o valor empata (ex.: 0% de CPU).
        var sorted = SortDescending ? items.OrderByDescending(key) : items.OrderBy(key);
        return sorted.ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Pid);
    }

    /// <summary>Árvore pai → filhos (pai já encerrado = raiz). Filhos em ordem de nome.</summary>
    private static List<ProcessItem> TreeOrder(List<ProcessItem> items)
    {
        var byPid = items.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.First());
        var children = items
            .Where(p => p.ParentPid != p.Pid && byPid.TryGetValue(p.ParentPid, out var parent) && parent.StartTime <= p.StartTime)
            .GroupBy(p => p.ParentPid)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
        var childSet = children.Values.SelectMany(c => c).ToHashSet();
        var result = new List<ProcessItem>(items.Count);

        void Add(ProcessItem item, int depth)
        {
            if (result.Contains(item))
            {
                return; // ciclo improvável (PID reaproveitado): evita laço infinito
            }

            item.Depth = Math.Min(depth, 12);
            result.Add(item);
            if (children.TryGetValue(item.Pid, out var kids))
            {
                foreach (var kid in kids)
                {
                    Add(kid, depth + 1);
                }
            }
        }

        foreach (var root in items.Where(p => !childSet.Contains(p)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Add(root, 0);
        }

        return result;
    }
}
