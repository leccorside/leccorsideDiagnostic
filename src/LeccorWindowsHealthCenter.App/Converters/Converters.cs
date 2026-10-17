using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LeccorWindowsHealthCenter.App.Converters;

// Cores semânticas não usam conversor: ficam nos estilos SemanticText/SeverityDot/ScoreRing (Styles.xaml),
// com DynamicResource, para acompanhar a troca de tema em tempo de execução.

/// <summary>Visível quando o valor é true / não-nulo / número &gt; 0 / coleção não vazia. "Invert" no parâmetro inverte.</summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            bool b => b,
            int i => i > 0,
            double d => d > 0,
            string s => s.Length > 0,
            System.Collections.ICollection c => c.Count > 0,
            _ => true,
        };

        if (parameter is "Invert")
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converte "87" (texto do score) em double para o RingGauge; "—" vira null.</summary>
public sealed class ScoreToValueConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s && double.TryParse(s, NumberStyles.Float, culture, out var v) ? v : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Compara um enum com o parâmetro (para RadioButtons).</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}
