using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;

namespace UnderMine.Services.Logger;

public partial class ProgressView : UserControl
{
    private static readonly IBrush DefaultColor = Brushes.Orange;
    private static readonly IBrush CompleteColor = Brush.Parse("#3C6"); 

    private readonly StreamGeometry _geometry = new StreamGeometry();

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ProgressView, double>(
            nameof(Progress),
            defaultValue: 0,
            coerce: CoerceProgress 
        );

    static ProgressView()
    {
        ProgressProperty.Changed.AddClassHandler<ProgressView>((sender, args) =>
        {
            sender.UpdateUi();
        });
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    private static double CoerceProgress(AvaloniaObject control, double value)
    {
        return Math.Clamp(value, 0, 100);
    }

    public ProgressView()
    {
        InitializeComponent();
        ProgressPath.Data = _geometry;
        UpdateUi();
    }

    private void UpdateUi()
    {
        double currentProgress = Progress;

        if (ProgressPath != null)
        {
            ProgressPath.Stroke = currentProgress >= 100 ? CompleteColor : DefaultColor;
        }

        if (PercentText != null)
            PercentText.Text = $"{currentProgress:F0}%";

        

        double renderProgress = currentProgress >= 100 ? 99.999 : currentProgress;

        double angle = renderProgress / 100 * 360;

        double radius = 19.0;
        double cx = 21.5;
        double cy = 21.5;

        double startAngleRad = -Math.PI / 2;
        double endAngleRad = startAngleRad + (angle * Math.PI / 180);

        Point startPoint = new Point(
            cx + radius * Math.Cos(startAngleRad),
            cy + radius * Math.Sin(startAngleRad)
        );

        Point endPoint = new Point(
            cx + radius * Math.Cos(endAngleRad),
            cy + radius * Math.Sin(endAngleRad)
        );

        bool isLargeArc = angle > 180;

        using (var context = _geometry.Open())
        {
            context.BeginFigure(startPoint, false);
            context.ArcTo(
                endPoint,
                new Size(radius, radius),
                0, 
                isLargeArc,
                SweepDirection.Clockwise
            );
            context.EndFigure(false);
        }

        InvalidateVisual(); 
    }

    public void SetStatus(string text)
    {

    }
}