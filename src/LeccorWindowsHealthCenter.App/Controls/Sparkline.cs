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

    public static readonly DependencyProperty TimestampsProperty = DependencyProperty.Register(
        nameof(Timestamps), typeof(IReadOnlyList<DateTimeOffset>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Horário de cada valor (opcional). Com ele, o eixo X é o tempo real e lacunas &gt; <see cref="MaxGap"/> interrompem a linha.</summary>
    public IReadOnlyList<DateTimeOffset>? Timestamps
    {
        get => (IReadOnlyList<DateTimeOffset>?)GetValue(TimestampsProperty);
        set => SetValue(TimestampsProperty, value);
    }

    /// <summary>Intervalo máximo entre amostras para a linha continuar (30 s — o mesmo das regras).</summary>
    public TimeSpan MaxGap { get; set; } = TimeSpan.FromSeconds(30);

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

        var times = Timestamps;
        var timed = times is not null && times.Count == values.Count;
        var skip = Math.Max(0, values.Count - Capacity);
        var count = values.Count - skip;
        if (count < 2)
        {
            return;
        }

        // Eixo X: pelo horário real quando há timestamps (a lacuna de uma suspensão aparece como lacuna);
        // senão, amostras igualmente espaçadas.
        var first = timed ? times![skip] : default;
        var span = timed ? (times![^1] - first).TotalMilliseconds : 0;
        double X(int i) => timed && span > 0
            ? (times![i] - first).TotalMilliseconds / span * ActualWidth
            : (i - skip) * ActualWidth / (count - 1);
        Point PointAt(int i) => new(X(i), ActualHeight - (Math.Clamp(values[i], 0, 100) / 100.0 * ActualHeight));

        // Segmentos contínuos: sem medição no intervalo, não se desenha linha (mesmo critério das regras).
        var segments = new List<(int Start, int End)>();
        var segmentStart = skip;
        for (var i = skip + 1; i < values.Count; i++)
        {
            if (timed && times![i] - times[i - 1] > MaxGap)
            {
                segments.Add((segmentStart, i - 1));
                segmentStart = i;
            }
        }

        segments.Add((segmentStart, values.Count - 1));

        var line = new StreamGeometry();
        var area = Fill is null ? null : new StreamGeometry();
        using (var lineCtx = line.Open())
        using (var areaCtx = area?.Open())
        {
            foreach (var (start, end) in segments)
            {
                lineCtx.BeginFigure(PointAt(start), isFilled: false, isClosed: false);
                for (var i = start + 1; i <= end; i++)
                {
                    lineCtx.LineTo(PointAt(i), isStroked: true, isSmoothJoin: true);
                }

                if (areaCtx is not null && end > start)
                {
                    areaCtx.BeginFigure(new Point(X(start), ActualHeight), isFilled: true, isClosed: true);
                    for (var i = start; i <= end; i++)
                    {
                        areaCtx.LineTo(PointAt(i), isStroked: false, isSmoothJoin: true);
                    }

                    areaCtx.LineTo(new Point(X(end), ActualHeight), isStroked: false, isSmoothJoin: false);
                }
            }
        }

        line.Freeze();
        if (area is not null)
        {
            area.Freeze();
            drawingContext.DrawGeometry(Fill, null, area);
        }

        var pen = new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        drawingContext.DrawGeometry(null, pen, line);
    }
}
