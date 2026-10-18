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

    public static readonly DependencyProperty BaselineProperty = DependencyProperty.Register(
        nameof(Baseline), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Máximo de pontos exibidos (os mais recentes). O eixo X sempre cobre o período disponível — a legenda
    /// da tela informa qual é ("Últimos 3 min"); poucos minutos numa escala fixa de 30 min virariam um traço ilegível.
    /// </summary>
    public int Capacity { get; set; } = 60;

    /// <summary>Linha de base sutil (0%) — deixa a área do gráfico visível mesmo com valores baixos.</summary>
    public Brush? Baseline
    {
        get => (Brush?)GetValue(BaselineProperty);
        set => SetValue(BaselineProperty, value);
    }

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
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        if (Baseline is not null)
        {
            var basePen = new Pen(Baseline, 1);
            basePen.Freeze();
            drawingContext.DrawLine(basePen, new Point(0, ActualHeight - 0.5), new Point(ActualWidth, ActualHeight - 0.5));
        }

        var values = Values;
        if (values is null || values.Count < 2)
        {
            return;
        }

        if (values.Count > Capacity)
        {
            values = values.Skip(values.Count - Capacity).ToArray();
        }

        var step = ActualWidth / (values.Count - 1);
        const double offset = 0;
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
