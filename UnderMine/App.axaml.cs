using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using System.Net.Http;
using UnderMine.IU;
using Velopack;

namespace UnderMine;

public partial class App : Application
{
    private static readonly HttpClient SharedClient = new HttpClient();
    public override void Initialize()
    {
        VelopackApp.Build().Run();
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new AuthWindow();
            
        }

        base.OnFrameworkInitializationCompleted();
    }
}