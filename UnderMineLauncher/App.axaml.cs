using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using System.Net.Http;
using UnderMine.IU;
using UnderMine.Core.Network;
using UnderMine.Core.Game;
using UnderMine.Models;
using UnderMine.Services;
using Microsoft.Extensions.DependencyInjection;

namespace UnderMine;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var authWindow = new AuthWindow(
                Program.Services.GetRequiredService<Core.Network.AuthService>(),
                Program.Services.GetRequiredService<Services.ProgressService>(),
                Program.Services.GetRequiredService<Models.ContextData>(),
                Program.Services.GetRequiredService<Core.Game.InstallGame>(),
                Program.Services.GetRequiredService<Models.ProfileManager>(),
                Program.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            );
            desktop.MainWindow = authWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
