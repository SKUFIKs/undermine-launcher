using System;
using Avalonia.Controls;
using Avalonia.Media;

namespace UnderMine.Services;

public partial class ProgressView : UserControl
{
    public ProgressView()
    {
        InitializeComponent();
        UpdateUI();
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set
        {
            _progress = Math.Clamp(value, 0, 100);
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        PercentText.Text = $"{_progress:F0}%";

        double angle = _progress / 100 * 360;
        double radius = 18; // радиус круга
        double cx = 21.5;
        double cy = 21.5;

        // Для 0% просто очищаем дугу
        if (_progress <= 0)
        {
            ProgressPath.Data = null;
            return;
        }

        double radians = angle * Math.PI / 180;
        double x = cx + radius * Math.Sin(radians);
        double y = cy - radius * Math.Cos(radians);

        int largeArcFlag = angle > 180 ? 1 : 0;

        string pathData = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "M {0} {1} A {2} {2} 0 {3} 1 {4} {5}",
            cx, cy - radius, radius, largeArcFlag, x, y);

        ProgressPath.Data = Geometry.Parse(pathData);
    }

    public void SetStatus(string text)
    {
        
    }
}