using Avalonia;
using System;
using UnderMine.Models;
using UnderMine.Network.AuthService;
using UnderMine.Services;
using UnderMine.Services.SystemInfo.MemorySettings;
using Velopack;

namespace UnderMine;

class Program
{
    public static ContextData? GlobalContextData { get; private set; }
    public static ServerInfo? ServerInfo { get; private set; }
    public static IdentityService? AuthService { get; private set; }
    public static LoginResult? LoginResult { get; private set; }
    public static LoginResult? SystemInfo { get; private set; }
    public static MemorySliderConfiguration? SliderValues { get; private set; } 

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }
    
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}