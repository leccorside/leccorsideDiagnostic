using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LeccorWindowsHealthCenter.App.ViewModels;

namespace LeccorWindowsHealthCenter.App.Views;

public partial class StorageView : UserControl
{
    public StorageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged old)
        {
            old.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is INotifyPropertyChanged current)
        {
            current.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    // Com muitos volumes, o painel da análise fica longe do botão "Analisar": ao começar, ele vem para a vista.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StorageViewModel.IsAnalyzing) && sender is StorageViewModel { IsAnalyzing: true })
        {
            // Título + cartão com a linha de progresso e "Cancelar análise" (só o título ficaria colado na borda).
            AnalysisTitle.BringIntoView(new Rect(0, 0, AnalysisTitle.ActualWidth, AnalysisTitle.ActualHeight + 140));
        }
    }
}
