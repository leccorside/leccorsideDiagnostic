using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LeccorWindowsHealthCenter.App.ViewModels;

namespace LeccorWindowsHealthCenter.App.Views;

/// <summary>Foco na confirmação de encerramento, anúncio do resultado e Ctrl+C/Delete na lista.</summary>
public partial class ProcessesView : UserControl
{
    private ProcessesViewModel? _viewModel;

    public ProcessesView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = e.NewValue as ProcessesViewModel;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        };
        ProcessList.PreviewKeyDown += OnListKeyDown;
        ProcessList.SizeChanged += (_, e) => ApplyCompact(e.NewSize.Width < CompactWidth);
        // Detalhes nunca passam de 45% da tela: o resto rola dentro do cartão (ver comentário na XAML).
        SizeChanged += (_, e) => DetailsCard.MaxHeight = Math.Max(200, e.NewSize.Height * 0.45);
    }

    /// <summary>Abaixo desta largura de lista a coluna Fabricante sai (continua nos detalhes do processo).</summary>
    private const double CompactWidth = 860;

    private bool? _compact;

    private void ApplyCompact(bool compact)
    {
        if (_compact == compact)
        {
            return;
        }

        _compact = compact;
        Resources["PublisherWidth"] = new GridLength(compact ? 0 : 200);
        PublisherHeader.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ActionsPanel.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
        ActionsPanel.Margin = compact ? new Thickness(0, 12, 0, 0) : new Thickness(0);
        Grid.SetRow(ActionsPanel, compact ? 1 : 0);
        Grid.SetColumn(ActionsPanel, compact ? 0 : 2);
        Grid.SetColumnSpan(ActionsPanel, compact ? 3 : 1);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.PropertyName == nameof(ProcessesViewModel.ConfirmingEnd) && _viewModel.ConfirmingEnd)
        {
            // A caixa inteira à vista (o foco sozinho rola só até o botão e deixa a borda de baixo cortada).
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                ConfirmEndButton.Focus();
                ConfirmBox.BringIntoView(new Rect(0, 0, ConfirmBox.ActualWidth, ConfirmBox.ActualHeight + 12));
            });
        }
        else if (e.PropertyName == nameof(ProcessesViewModel.ActionMessage) && !string.IsNullOrEmpty(_viewModel.ActionMessage))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                (UIElementAutomationPeer.FromElement(ActionText) ?? UIElementAutomationPeer.CreatePeerForElement(ActionText))
                    ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged));
        }
    }

    /// <summary>Ctrl+C copia o caminho; Delete pede a confirmação de encerramento (nunca encerra direto).</summary>
    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.Selected is not { } item)
        {
            return;
        }

        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _viewModel.CopyPathCommand.Execute(item.Path);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _viewModel.RequestEndCommand.CanExecute(null))
        {
            _viewModel.RequestEndCommand.Execute(null);
            e.Handled = true;
        }
    }
}
