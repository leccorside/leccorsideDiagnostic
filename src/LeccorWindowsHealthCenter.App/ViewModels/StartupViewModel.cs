using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>Uma entrada de inicialização como a tela mostra. <see cref="Problem"/> repete o critério das regras da varredura.</summary>
public sealed partial class StartupRow : ObservableObject, IPathRow
{
    public StartupRow(StartupEntry entry, StartupImpactLevel impact)
    {
        Entry = entry;
        Impact = impact;
    }

    public StartupEntry Entry { get; }

    public StartupImpactLevel Impact { get; }

    public string Name => Entry.Name;

    public string? Path => Entry.ExecutablePath;

    public string? Command => Entry.Command;

    /// <summary>Desativado fica vazio (o status já diz); "não medido" curto, explicado na legenda da tela.</summary>
    public string ImpactText => Impact switch
    {
        StartupImpactLevel.Disabled => string.Empty,
        StartupImpactLevel.NotMeasured => "não medido",
        _ => StartupImpact.Text(Impact),
    };

    public bool IsHighImpact => Impact == StartupImpactLevel.High;

    public bool IsMeasured => Impact is StartupImpactLevel.Low or StartupImpactLevel.Medium or StartupImpactLevel.High;

    public bool IsEnabled => Entry.Enabled == true;

    /// <summary>"Desativado no Gerenciador de Tarefas" em vez de "Desativado · desativado no…".</summary>
    public string StatusText => Entry.Enabled switch
    {
        true => Entry.StateDetail is { } detail ? $"Ativo · {detail}" : "Ativo",
        false when Entry.StateDetail is { Length: > 0 } detail && detail.StartsWith("desativado", StringComparison.CurrentCultureIgnoreCase) =>
            char.ToUpper(detail[0], System.Globalization.CultureInfo.CurrentCulture) + detail[1..],
        false => Entry.StateDetail is { } detail ? $"Desativado · {detail}" : "Desativado",
        _ => "estado não lido",
    };

    /// <summary>Componente do próprio Windows: o problema é informativo (a regra emite Info), sem cor de alerta.</summary>
    public bool IsInformational => Entry.IsWindowsComponent;

    public string SourceText => Entry.Source switch
    {
        StartupSourceKind.RegistryRun => $"Registro · {(Entry.CurrentUserOnly ? "só este usuário" : "todos os usuários")}",
        StartupSourceKind.RegistryRunOnce => "Registro (uma vez)",
        StartupSourceKind.StartupFolder => $"Pasta Inicializar · {(Entry.CurrentUserOnly ? "só este usuário" : "todos os usuários")}",
        StartupSourceKind.ScheduledTask => $"Tarefa agendada · {Entry.Location}",
        _ => $"Serviço · {Entry.Location}",
    };

    /// <summary>Mesmos critérios de StartupMissingExecutableRule e StartupTempLocationRule.</summary>
    public string? Problem =>
        Entry.Enabled == true && Entry.ExecutableExists == false && Entry.IsWindowsComponent ? "O arquivo foi removido por uma atualização do Windows; a tarefa falha sem efeito."
        : Entry.Enabled == true && Entry.ExecutableExists == false ? "O arquivo não existe mais: resto de programa desinstalado."
        : Entry.Enabled == true && StartupCommand.IsTemporaryLocation(Entry.ExecutablePath) ? "Inicia a partir de uma pasta temporária: confira fabricante e assinatura."
        : null;

    public bool IsMissing => Entry.Enabled == true && Entry.ExecutableExists == false;

    /// <summary>Onde desativar com segurança, conforme a origem (esta tela não altera nada).</summary>
    public string Advice => Entry.IsWindowsComponent
        ? "Não é preciso fazer nada. Evite desativar tarefas do próprio Windows: a próxima atualização pode corrigi-la ou removê-la."
        : Entry.Source switch
    {
        StartupSourceKind.ScheduledTask => "Para desativar: Agendador de Tarefas (taskschd.msc), na pasta indicada acima, ou pelo programa que a criou.",
        StartupSourceKind.Service => "Para desativar: Serviços (services.msc) → tipo de inicialização Manual. Confirme antes que o programa do serviço foi desinstalado.",
        _ => "Para desativar: Gerenciador de Tarefas (Ctrl+Shift+Esc) → Aplicativos de inicialização, ou pelo programa que a criou.",
    };

    public string AccessibleName => $"{Name}, {StatusText}{(ImpactText.Length > 0 ? $", impacto {ImpactText}" : string.Empty)}{(Problem is { } p ? $". {p}" : string.Empty)}";

    [ObservableProperty] private string _publisher = string.Empty;
    [ObservableProperty] private string _signatureText = string.Empty;
    [ObservableProperty] private bool _signatureInvalid;

    public void ApplyIdentity(FileIdentity identity)
    {
        Publisher = identity.Exists ? identity.Publisher ?? "fabricante não informado" : string.Empty;
        SignatureText = identity.Exists ? identity.SignatureText : string.Empty;
        SignatureInvalid = identity.Signature == SignatureStatus.Invalid;
    }
}

public sealed record StartupGroup(string Title, string Caption, IReadOnlyList<StartupRow> Rows);

/// <summary>
/// Inicialização (seção 19), somente leitura: o que o Windows inicia no logon/boot, ativo ou não, com impacto estimado
/// pelo consumo atual, fabricante e assinatura. Desativar com backup e desfazer fica para a tela Correções.
/// </summary>
public sealed partial class StartupViewModel : ObservableObject, IPageLifecycle
{
    private readonly IStartupInventoryProvider _inventory;
    private readonly IProcessInventoryProvider _processes;
    private readonly IFileIdentityProvider _identities;
    private readonly IShellLauncher _shell;
    private readonly IFolderPicker _clipboard;
    private readonly UiPreferences _preferences;
    private readonly ILogger<StartupViewModel> _logger;
    private List<StartupRow> _all = [];
    private bool _loaded;

    public StartupViewModel(
        IStartupInventoryProvider inventory,
        IProcessInventoryProvider processes,
        IFileIdentityProvider identities,
        IShellLauncher shell,
        IFolderPicker clipboard,
        UiPreferences preferences,
        ILogger<StartupViewModel> logger)
    {
        _inventory = inventory;
        _processes = processes;
        _identities = identities;
        _shell = shell;
        _clipboard = clipboard;
        _preferences = preferences;
        _logger = logger;
        preferences.PropertyChanged += (_, _) => OnPropertyChanged(nameof(AdvancedModeWithWarnings));
    }

    public bool AdvancedModeWithWarnings => _preferences.AdvancedMode && Warnings.Count > 0;

    partial void OnWarningsChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(AdvancedModeWithWarnings));

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _capturedText = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private IReadOnlyList<StartupGroup> _groups = [];
    [ObservableProperty] private IReadOnlyList<StartupRow> _problems = [];
    [ObservableProperty] private IReadOnlyList<string> _warnings = [];
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private bool _showWindowsItems;
    [ObservableProperty] private bool _showDisabled = true;
    [ObservableProperty] private string _hiddenText = string.Empty;
    [ObservableProperty] private string _actionMessage = string.Empty;
    [ObservableProperty] private bool _noResults;

    partial void OnSearchChanged(string value) => ApplyView();

    partial void OnShowWindowsItemsChanged(bool value) => ApplyView();

    partial void OnShowDisabledChanged(bool value) => ApplyView();

    public void Activate()
    {
        if (!_loaded && !IsLoading)
        {
            _loaded = true;
            _ = LoadAsync(forceRefresh: false);
        }
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync(forceRefresh: true);

    [RelayCommand]
    private void Reveal(string? path)
    {
        if (path is not null && !_shell.Reveal(path))
        {
            ActionMessage = "Não foi possível mostrar o arquivo no Explorer: ele não existe mais ou foi movido.";
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
    private void CopyCommand(string? command)
    {
        if (!string.IsNullOrEmpty(command))
        {
            ActionMessage = _clipboard.CopyToClipboard(command) ? "Linha de comando copiada." : "A área de transferência está em uso por outro programa.";
        }
    }

    private async Task LoadAsync(bool forceRefresh)
    {
        IsLoading = true;
        Error = null;
        try
        {
            var inventory = await _inventory.GetAsync(CancellationToken.None, forceRefresh);
            var running = await Task.Run(_processes.Capture);
            CapturedText = $"Lido em {Fmt.DateTime(inventory.CapturedAt)}. Somente leitura: nada é alterado nesta tela.";
            Warnings = inventory.Warnings;
            _all = inventory.Entries.Select(e => new StartupRow(e, StartupImpact.Estimate(e, running.Processes))).ToList();

            var active = _all.Count(r => r.IsEnabled);
            var high = _all.Count(r => r.IsHighImpact);
            var problems = _all.Count(r => r.Problem is not null && !r.IsInformational);
            Summary = string.Join(" · ", new[]
            {
                $"{Fmt.Count(active, "item ativo inicia", "itens ativos iniciam")} com o Windows",
                high > 0 ? Fmt.Count(high, "de alto impacto", "de alto impacto") : null,
                problems > 0 ? Fmt.Count(problems, "com problema", "com problema") : null,
            }.Where(s => s is not null));
            ApplyView();
            _ = LoadIdentitiesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao ler a inicialização");
            Error = "Não foi possível ler os itens de inicialização. Use “Ler novamente”; o detalhe técnico ficou no log.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadIdentitiesAsync()
    {
        foreach (var group in _all.Where(r => r.Path is not null && r.Entry.ExecutableExists == true).GroupBy(r => r.Path!, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var identity = await _identities.GetAsync(group.Key, CancellationToken.None);
                foreach (var row in group)
                {
                    row.ApplyIdentity(identity);
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "Identidade de {Path} indisponível", group.Key);
            }
        }
    }

    private void ApplyView()
    {
        var term = Search.Trim();
        bool Matches(StartupRow r) => term.Length == 0
            || r.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || (r.Command?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || r.Publisher.Contains(term, StringComparison.CurrentCultureIgnoreCase);

        // Problemas sempre aparecem, mesmo de itens do Windows ou desativados: são o diagnóstico da tela.
        Problems = _all.Where(r => r.Problem is not null).OrderBy(r => r.IsInformational).ToList();
        var visible = _all.Where(r => (ShowWindowsItems || !r.Entry.IsWindowsComponent) && (ShowDisabled || r.IsEnabled) && Matches(r)).ToList();
        var hiddenWindows = _all.Count(r => r.Entry.IsWindowsComponent && !ShowWindowsItems);
        HiddenText = hiddenWindows > 0 && !ShowWindowsItems ? $"{Fmt.Count(hiddenWindows, "item do próprio Windows oculto", "itens do próprio Windows ocultos")}." : string.Empty;

        static IOrderedEnumerable<StartupRow> Order(IEnumerable<StartupRow> rows) =>
            rows.OrderByDescending(r => r.IsEnabled).ThenByDescending(r => r.Impact).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase);

        Groups = new[]
        {
            new StartupGroup("Aplicativos", "Registro (Run/RunOnce) e pastas Inicializar: o que o Gerenciador de Tarefas mostra em Aplicativos de inicialização.",
                Order(visible.Where(r => r.Entry.Source is StartupSourceKind.RegistryRun or StartupSourceKind.RegistryRunOnce or StartupSourceKind.StartupFolder)).ToList()),
            new StartupGroup("Tarefas agendadas", "Tarefas que rodam no logon ou no boot.",
                Order(visible.Where(r => r.Entry.Source == StartupSourceKind.ScheduledTask)).ToList()),
            new StartupGroup("Serviços automáticos", "Serviços que o Windows inicia no boot, antes do logon.",
                Order(visible.Where(r => r.Entry.Source == StartupSourceKind.Service)).ToList()),
        }.Where(g => g.Rows.Count > 0).ToList();
        NoResults = _all.Count > 0 && Groups.Count == 0;
    }
}
