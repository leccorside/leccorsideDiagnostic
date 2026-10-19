using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LeccorWindowsHealthCenter.App.ViewModels;

namespace LeccorWindowsHealthCenter.App.Views;

/// <summary>Ctrl+C na linha focada copia o caminho do executável.</summary>
public partial class StartupView : UserControl
{
    public StartupView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control
            && Keyboard.FocusedElement is FrameworkElement { DataContext: StartupRow row }
            && DataContext is StartupViewModel viewModel)
        {
            viewModel.CopyPathCommand.Execute(row.Path ?? row.Command);
            e.Handled = true;
        }
    }
}
