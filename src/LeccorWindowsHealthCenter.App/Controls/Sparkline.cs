using System.Windows;
using System.Windows.Media;

namespace LeccorWindowsHealthCenter.App.Controls;

/// <summary>Gráfico de linha leve (0–100%) para o histórico de CPU/RAM. Desenho direto, sem bibliotecas de gráfico.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Quantidade de pontos que a largura representa (o gráfico "anda" da direita para a esquerda).</summary>
    public int Capacity { get; set; } = 60;

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var values = Values;
        if (values is null || values.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var step = ActualWidth / Math.Max(1, Capacity - 1);
        var offset = ActualWidth - ((values.Count - 1) * step);
        Point PointAt(int i) => new(offset + (i * step), ActualHeight - (Math.Clamp(values[i], 0, 100) / 100.0 * ActualHeight));

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(PointAt(0), isFilled: false, isClosed: false);
            for (var i = 1; i < values.Count; i++)
            {
                ctx.LineTo(PointAt(i), isStroked: true, isSmoothJoin: true);
            }
        }

        line.Freeze();

        if (Fill is not null)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(offset, ActualHeight), isFilled: true, isClosed: true);
                for (var i = 0; i < values.Count; i++)
                {
                    ctx.LineTo(PointAt(i), isStroked: false, isSmoothJoin: true);
                }

                ctx.LineTo(new Point(ActualWidth, ActualHeight), isStroked: false, isSmoothJoin: false);
            }

            area.Freeze();
            drawingContext.DrawGeometry(Fill, null, area);
        }

        var pen = new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        drawingContext.DrawGeometry(null, pen, line);
    }
}
