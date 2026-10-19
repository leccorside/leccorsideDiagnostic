using CommunityToolkit.Mvvm.ComponentModel;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>Página ainda não implementada: informa honestamente em qual etapa ela chega.</summary>
public sealed class PlaceholderViewModel
{
    public PlaceholderViewModel(string title, string description, string plannedFor)
    {
        Title = title;
        Description = description;
        PlannedFor = plannedFor;
    }

    public string Title { get; }

    public string Description { get; }

    public string PlannedFor { get; }
}

public sealed record NavigationItem(string Title, string Glyph, object Page, bool IsFooter = false);

/// <summary>Shell: sidebar com o menu principal (seção 8) e a página atual.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ScanViewModel _scan;
    private bool _windowVisible = true;

    public MainViewModel(
        DashboardViewModel dashboard,
        ScanViewModel scan,
        PerformanceViewModel performance,
        HardwareViewModel hardware,
        StorageViewModel storage,
        SettingsViewModel settings)
    {
        _scan = scan;

        Items =
        [
            new("Dashboard", "", dashboard),
            new("Varredura", "", scan),
            new("Performance", "", performance),
            Planned("Processos", "", "Gerenciador avançado de processos com assinatura, árvore e proteção de processos críticos.", "MVP 1"),
            Planned("Windows Explorer", "", "Shell extensions, handlers, caches e detector de pastas lentas.", "MVP 2"),
            new("Armazenamento", "", storage),
            new("Hardware", "", hardware),
            Planned("Drivers", "", "Dispositivos com erro, drivers ausentes e eventos recorrentes.", "MVP 2"),
            Planned("Inicialização", "", "Registry Run/RunOnce, pastas de Startup e tarefas agendadas, com rollback.", "MVP 1"),
            Planned("Serviços", "", "Serviços falhando, órfãos ou de aplicações inexistentes.", "MVP 2"),
            Planned("Aplicativos", "", "Inventário de software, crashes recorrentes e versões duplicadas.", "MVP 2"),
            Planned("Windows", "", "Integridade do Windows (DISM, SFC, CHKDSK) e Windows Update.", "MVP 1"),
            Planned("Eventos", "", "Event Viewer agrupado por origem/ID com recorrência nas últimas 24h.", "MVP 1"),
            Planned("Rede", "", "Gateway, DNS, latência, perda de pacotes e adaptadores.", "MVP 2"),
            Planned("Segurança", "", "Status do Windows Security/Defender e persistências suspeitas.", "MVP 4"),
            Planned("Desenvolvimento", "", "Docker, WSL, Hyper-V, Node.js, Java, .NET, Git e IDEs.", "MVP 4"),
            Planned("Limpeza", "", "Limpeza segura por categorias: Seguro, Revisar e Avançado.", "MVP 2"),
            Planned("Correções", "", "Correções seguras e análise individual de mudanças MEDIUM/HIGH.", "MVP 3"),
            Planned("Histórico", "", "Histórico de correções com Desfazer e comparação antes/depois.", "MVP 3"),
            Planned("Relatórios", "", "Exportação em HTML, JSON, CSV e PDF.", "MVP 5"),
            new("Configurações", "", settings, IsFooter: true),
        ];

        MainItems = Items.Where(i => !i.IsFooter).ToList();
        FooterItems = Items.Where(i => i.IsFooter).ToList();
        _selectedMainItem = Items[0];
        _currentPage = dashboard;

        dashboard.ScanRequested += async (_, plan) =>
        {
            SelectedMainItem = Items[1];
            await _scan.StartAsync(plan);
        };
    }

    public IReadOnlyList<NavigationItem> Items { get; }

    public IReadOnlyList<NavigationItem> MainItems { get; }

    public IReadOnlyList<NavigationItem> FooterItems { get; }

    // Duas listas (menu e rodapé) com seleções independentes: a página atual segue a última seleção não nula.
    [ObservableProperty]
    private NavigationItem? _selectedMainItem;

    [ObservableProperty]
    private NavigationItem? _selectedFooterItem;

    [ObservableProperty]
    private object _currentPage;

    partial void OnSelectedMainItemChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            SelectedFooterItem = null;
            Navigate(value);
        }
    }

    partial void OnSelectedFooterItemChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            SelectedMainItem = null;
            Navigate(value);
        }
    }

    private void Navigate(NavigationItem item)
    {
        if (ReferenceEquals(item.Page, CurrentPage))
        {
            return;
        }

        (CurrentPage as IPageLifecycle)?.Deactivate();
        CurrentPage = item.Page;
        if (_windowVisible)
        {
            (CurrentPage as IPageLifecycle)?.Activate();
        }
    }

    /// <summary>
    /// Só a página visível amostra; minimizada, nada amostra (seção 58: CPU ociosa &lt; 1%).
    /// O histórico de fundo (MetricsHistoryService) continua, por ser barato e necessário às regras.
    /// </summary>
    public void SetWindowVisible(bool visible)
    {
        _windowVisible = visible;
        if (visible)
        {
            (CurrentPage as IPageLifecycle)?.Activate();
        }
        else
        {
            (CurrentPage as IPageLifecycle)?.Deactivate();
        }
    }

    private static NavigationItem Planned(string title, string glyph, string description, string plannedFor) =>
        new(title, glyph, new PlaceholderViewModel(title, description, plannedFor));
}
