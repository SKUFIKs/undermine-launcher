using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace UnderMine.Services.Logger;

public partial class ProgressView : UserControl
{

    private static readonly IBrush DefaultColor = Brushes.Orange;
    private static readonly IBrush CompleteColor = Brush.Parse("#3C6");
    private static readonly IPen BackgroundPen = new Pen(Brush.Parse("#5C5C5C"), 5);

    private Pen _activePen;

    private StreamGeometry _arcGeometry;

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ProgressView, double>(nameof(Progress), coerce: CoerceProgress);

    private static double CoerceProgress(AvaloniaObject sender, double value) => Math.Clamp(value, 0, 100);

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }
  
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ProgressProperty)
        {
            UpdateGeometry();
            InvalidateVisual();
        }
    }

    public ProgressView()
    {
        InitializeComponent();
        _activePen = new Pen(DefaultColor, 5, lineCap: PenLineCap.Round);
    }

    private void UpdateGeometry()
    {
        double val = Progress;

        var targetBrush = val >= 100 ? CompleteColor : DefaultColor;
        if (_activePen.Brush != targetBrush)
        {
            _activePen = new Pen(targetBrush, 5, lineCap: PenLineCap.Round);
        }

        if (val < 100 && val > 0)
        {
            double w = Bounds.Width > 0 ? Bounds.Width : 43;
            double h = Bounds.Height > 0 ? Bounds.Height : 43;
            double radius = (Math.Min(w, h) - 5) / 2;
            double cx = w / 2;
            double cy = h / 2;

            _arcGeometry = new StreamGeometry();
            using var ctx = _arcGeometry.Open();

            double sweepAngle = (val / 100.0) * 360.0;
            double startAngleRad = -Math.PI / 2;
            double sweepAngleRad = sweepAngle * Math.PI / 180.0;

            Point startPoint = new Point(
                cx + radius * Math.Cos(startAngleRad),
                cy + radius * Math.Sin(startAngleRad));

            Point endPoint = new Point(
                cx + radius * Math.Cos(startAngleRad + sweepAngleRad),
                cy + radius * Math.Sin(startAngleRad + sweepAngleRad));

            ctx.BeginFigure(startPoint, false);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        else
        {
            _arcGeometry = null;
        }
    }
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateGeometry();
    }

    public override void Render(DrawingContext context)
    {
        double thickness = 5;
        double w = Bounds.Width;
        double h = Bounds.Height;
        double cx = w / 2;
        double cy = h / 2;
        double radius = (Math.Min(w, h) - thickness) / 2;

        context.DrawEllipse(null, BackgroundPen, new Point(cx, cy), radius, radius);

        if (Progress <= 0) return;

        if (Progress >= 100)
        {
            context.DrawEllipse(null, _activePen, new Point(cx, cy), radius, radius);
        }
        else if (_arcGeometry != null)
        {
            context.DrawGeometry(null, _activePen, _arcGeometry);
        }
    }

    // Добавьте этот метод в класс ProgressView для устранения ошибки CS0103.
    // Этот метод обычно генерируется XAML-дизайнером, но если у вас нет соответствующего .axaml файла или вы не используете XAML, оставьте его пустым.
    private void InitializeComponent()
    {
        // Если у вас есть файл ProgressView.axaml, раскомментируйте следующую строку:
        // AvaloniaXamlLoader.Load(this);
    }
}