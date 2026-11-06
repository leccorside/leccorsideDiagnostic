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

public sealed record NavigationItem(string Title, string Glyph, object Page, bool IsFooter = false)
{
    /// <summary>O ListBoxItem usa o ToString como nome acessível: o leitor de tela lê só o título.</summary>
    public override string ToString() => Title;
}

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
        ProcessesViewModel processes,
        StartupViewModel startup,
        ExplorerViewModel explorer,
        ServicesViewModel services,
        DriversViewModel drivers,
        AppsViewModel apps,
        WindowsViewModel windows,
        WindowsUpdateViewModel windowsUpdate,
        EventsViewModel events,
        NetworkViewModel network,
        SafeCleanViewModel safeClean,
        HistoryViewModel history,
        SettingsViewModel settings)
    {
        _scan = scan;

        Items =
        [
            new("Dashboard", "", dashboard),
            new("Varredura", "", scan),
            new("Performance", "", performance),
            new("Processos", "", processes),
            new("Windows Explorer", "", explorer),
            new("Armazenamento", "", storage),
            new("Hardware", "", hardware),
            new("Drivers", "", drivers),
            new("Inicialização", "", startup),
            new("Serviços", "", services),
            new("Aplicativos", "", apps),
            new("Windows", "", windows),
            new("Windows Update", "", windowsUpdate),
            new("Eventos", "", events),
            new("Rede", "", network),
            Planned("Segurança", "", "Status do Windows Security/Defender e persistências suspeitas.", "MVP 4"),
            Planned("Desenvolvimento", "", "Docker, WSL, Hyper-V, Node.js, Java, .NET, Git e IDEs.", "MVP 4"),
            new("Limpeza", "", safeClean),
            Planned("Correções", "", "Correções seguras e análise individual de mudanças MEDIUM/HIGH.", "MVP 3"),
            new("Histórico", "", history),
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

        _scan.NavigateRequested += (_, targetPage) =>
        {
            NavigateToPage(targetPage);
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

    public void NavigateToPage(string pageTitle)
    {
        var item = MainItems.FirstOrDefault(i => string.Equals(i.Title, pageTitle, StringComparison.OrdinalIgnoreCase))
                ?? FooterItems.FirstOrDefault(i => string.Equals(i.Title, pageTitle, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            if (MainItems.Contains(item))
            {
                SelectedMainItem = item;
            }
            else
            {
                SelectedFooterItem = item;
            }
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
