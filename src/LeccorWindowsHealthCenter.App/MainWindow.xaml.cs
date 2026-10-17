using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.App.ViewModels;

namespace LeccorWindowsHealthCenter.App;

public partial class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;

    private readonly MainViewModel _viewModel;
    private readonly ThemeService _themeService;

    public MainWindow(MainViewModel viewModel, DashboardViewModel dashboard, ThemeService themeService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _themeService = themeService;
        DataContext = viewModel;

        SourceInitialized += (_, _) => ApplyTitleBarTheme();
        themeService.ThemeChanged += (_, _) => ApplyTitleBarTheme();

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        StateChanged += (_, _) => _viewModel.SetWindowVisible(WindowState != WindowState.Minimized);
        Loaded += async (_, _) =>
        {
            _viewModel.SetWindowVisible(true);
            await dashboard.LoadLastScanAsync();
        };
        Closing += (_, _) => _viewModel.SetWindowVisible(false);
    }

    /// <summary>Animação discreta de entrada de página (seção 43): fade + leve deslocamento, 180 ms.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentPage) || SystemParameters.ClientAreaAnimation == false)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        if (PageHost.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = ease });
        }
    }

    /// <summary>Barra de título escura no tema escuro (Windows 10 20H1+ e Windows 11; ignorado em versões sem suporte).</summary>
    private void ApplyTitleBarTheme()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = _themeService.IsDark ? 1 : 0;
        _ = NativeMethods.DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    private static class NativeMethods
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
