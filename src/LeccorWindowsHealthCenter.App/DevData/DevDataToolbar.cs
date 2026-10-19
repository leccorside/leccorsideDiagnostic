#if DEBUG
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LeccorWindowsHealthCenter.App.ViewModels;

namespace LeccorWindowsHealthCenter.App.DevData;

/// <summary>
/// Seletor de dados de desenvolvimento (break-ui): fixo no centro inferior, cinza neutro, sem animação.
/// É "cromo" de desenvolvimento, não faz parte do design testado. SÓ EM DEBUG.
/// </summary>
public static class DevDataToolbar
{
    private static readonly (DevDataMode Mode, string Label)[] Segments =
    [
        (DevDataMode.Real, "Dados reais"),
        (DevDataMode.Worst, "Pior caso"),
        (DevDataMode.Empty, "Vazio"),
        (DevDataMode.One, "Um"),
        (DevDataMode.Many, "Muitos"),
    ];

    public static void Attach(Window window, DevDataSwitch sw, HardwareViewModel hardware, StorageViewModel storage)
    {
        if (window.Content is not Grid root)
        {
            return;
        }

        var track = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 14),
        };
        Grid.SetColumnSpan(track, Math.Max(1, root.ColumnDefinitions.Count));
        Panel.SetZIndex(track, 1000);

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (mode, label) in Segments)
        {
            var button = new Button
            {
                Content = label,
                Tag = mode,
                Padding = new Thickness(12, 4, 12, 4),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.Black,
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = PillTemplate(),
            };
            AutomationPropertiesHelper.SetName(button, $"Dados de desenvolvimento: {label}");
            button.Click += (_, _) =>
            {
                sw.Set(mode);
                _ = hardware.RefreshCommand.ExecuteAsync(null);
                storage.ResetAnalyses();
                _ = storage.RefreshCommand.ExecuteAsync(null);
            };
            panel.Children.Add(button);
        }

        void Paint()
        {
            foreach (Button b in panel.Children)
            {
                b.Background = (DevDataMode)b.Tag == sw.Mode ? Brushes.White : Brushes.Transparent;
            }
        }

        sw.Changed += (_, _) => Paint();
        Paint();
        track.Child = panel;
        root.Children.Add(track);
    }

    private static ControlTemplate PillTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        border.AppendChild(content);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static class AutomationPropertiesHelper
    {
        public static void SetName(DependencyObject element, string name) =>
            System.Windows.Automation.AutomationProperties.SetName(element, name);
    }
}
#endif
