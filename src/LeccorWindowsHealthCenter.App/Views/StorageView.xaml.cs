using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LeccorWindowsHealthCenter.App.ViewModels;

namespace LeccorWindowsHealthCenter.App.Views;

/// <summary>
/// Só comportamento de interface que o ViewModel não alcança: foco de teclado (não se perde ao analisar ou navegar),
/// anúncio do leitor de tela (início/fim/cancelamento da análise), rolagem até a análise e Ctrl+C na linha focada.
/// </summary>
public partial class StorageView : UserControl
{
    private StorageViewModel? _viewModel;

    public StorageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.FolderListChanged -= OnFolderListChanged;
        }

        _viewModel = e.NewValue as StorageViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.FolderListChanged += OnFolderListChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        // Resumo dos alertas ("2 problemas críticos…") e confirmações fora da análise ("Caminho copiado") são anunciados.
        // O progresso contínuo da análise não é, para não soterrar o leitor de tela.
        if (e.PropertyName == nameof(StorageViewModel.AlertSummary) && !string.IsNullOrEmpty(_viewModel.AlertSummary))
        {
            Announce(AlertSummaryText);
            return;
        }

        if (e.PropertyName == nameof(StorageViewModel.AnalysisStatus) && !_viewModel.IsAnalyzing)
        {
            Announce(StatusText);
            return;
        }

        if (e.PropertyName != nameof(StorageViewModel.IsAnalyzing))
        {
            return;
        }

        if (_viewModel.IsAnalyzing)
        {
            // Com muitos volumes o painel fica longe do botão: título + linha de progresso vêm para a vista.
            AnalysisTitle.BringIntoView(new Rect(0, 0, AnalysisTitle.ActualWidth, AnalysisTitle.ActualHeight + 140));

            // O botão "Analisar" fica desabilitado e perderia o foco: ele vai para "Cancelar análise".
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => CancelButton.Focus());
        }

        // Início, fim ou cancelamento: o leitor de tela anuncia o status.
        Announce(StatusText);
    }

    private void Announce(UIElement element) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            (UIElementAutomationPeer.FromElement(element) ?? UIElementAutomationPeer.CreatePeerForElement(element))
                ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged));

    /// <summary>Depois de analisar ou entrar numa pasta, o foco vai para a primeira linha (antes sumia com a lista trocada).</summary>
    private void OnFolderListChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FindFirst<Button>(FolderList)?.Focus());

    /// <summary>Ctrl+C numa linha com caminho copia o caminho completo (o menu de contexto mostra o atalho).</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control
            && Keyboard.FocusedElement is FrameworkElement { DataContext: IPathRow { Path: { } path } }
            && Keyboard.FocusedElement is not TextBox
            && _viewModel is not null)
        {
            _viewModel.CopyPathCommand.Execute(path);
            e.Handled = true;
        }
    }

    private static T? FindFirst<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            if (FindFirst<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
