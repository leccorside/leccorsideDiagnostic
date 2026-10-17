using System.Windows;
using System.Windows.Media;

namespace LeccorWindowsHealthCenter.App.Controls;

/// <summary>Anel de progresso (0–100) usado no score de saúde. Sem valor, desenha apenas o trilho.</summary>
public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double?), typeof(RingGauge),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Thickness { get; set; } = 10;

    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush Track
    {
        get => (Brush)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness * 2)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - Thickness) / 2;

        var trackPen = new Pen(Track, Thickness);
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        if (Value is not { } value || value <= 0)
        {
            return;
        }

        var fraction = Math.Clamp(value, 0, 100) / 100.0;
        if (fraction >= 0.9999)
        {
            drawingContext.DrawEllipse(null, new Pen(Stroke, Thickness), center, radius, radius);
            return;
        }

        var angle = fraction * 360;
        var start = new Point(center.X, center.Y - radius);
        var radians = (angle - 90) * Math.PI / 180;
        var end = new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0, angle > 180, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        var pen = new Pen(Stroke, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        drawingContext.DrawGeometry(null, pen, geometry);
    }
}
