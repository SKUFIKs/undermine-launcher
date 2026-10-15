using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes; // Добавлено для Path
using Avalonia.Media;
using System;

namespace UnderMine.IU.Controls;

public partial class ToastControl : UserControl
{
    private bool _isToastShowing = false;

    // Ссылки на элементы управления
    private Border? _toastBorder;
    private TextBlock? _toastText;
    private Path? _toastIcon;

    private readonly string _errorIconData = "M11,15H13V17H11V15M11,7H13V13H11V7M12,2C6.47,2 2,6.47 2,12C2,17.53 6.47,22 12,22C17.53,22 22,17.53 22,12C22,6.47 17.53,2 12,2Z";
    private readonly string _successIconData = "M12,2C6.5,2 2,6.5 2,12C2,17.5 6.5,22 12,22C17.5,22 22,17.5 22,12C22,6.5 17.5,2 12,2M10,17L5,12L6.41,10.59L10,14.17L17.59,6.58L19,8L10,17Z";

    public ToastControl()
    {
        // Если InitializeComponent все еще подчеркнут, попробуйте пересобрать проект (Build).
        // В Avalonia он создается автоматически.
        InitializeComponent();

        // Находим элементы вручную, чтобы избежать ошибок контекста
        _toastBorder = this.FindControl<Border>("ToastBorder");
        _toastText = this.FindControl<TextBlock>("ToastText");
        _toastIcon = this.FindControl<Path>("ToastIcon");
    }

    public async Task ShowMessage(string message, bool isError = true)
    {
        if (_toastBorder == null || _toastText == null || _toastIcon == null) return;

        _toastText.Text = message;

        if (isError)
        {
            _toastBorder.Background = SolidColorBrush.Parse("#D32F2F");
            _toastIcon.Data = Geometry.Parse(_errorIconData);
        }
        else
        {
            _toastBorder.Background = SolidColorBrush.Parse("#4CAF50");
            _toastIcon.Data = Geometry.Parse(_successIconData);
        }

        if (_isToastShowing) return;
        _isToastShowing = true;

        _toastBorder.Margin = new Thickness(0, 30, 0, 0);
        _toastBorder.Opacity = 1;

        await Task.Delay(3000);

        _toastBorder.Margin = new Thickness(0, -100, 0, 0);
        _toastBorder.Opacity = 0;

        await Task.Delay(300);
        _isToastShowing = false;
    }
}