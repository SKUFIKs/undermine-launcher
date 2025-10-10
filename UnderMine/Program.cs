using Avalonia;
using System;
using UnderMine.Models;
using UnderMine.Network.AuthService;

namespace UnderMine;

class Program
{
    public static Config GlobalConfig { get; private set; }
    public static ServerInfo ServerInfo { get; private set; }
    public static IdentityService AuthService { get; private set; }
    public static LoginResult LoginResult { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }
    
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}