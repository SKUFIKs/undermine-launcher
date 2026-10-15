using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Net.Http;
using UnderMine.Core.Network;
using UnderMine.Core.Game;
using UnderMine.Models;
using UnderMine.Services;
using Velopack;
using DotNetEnv;

namespace UnderMine;

class Program
{
    public static IHost? Host { get; private set; }
    public static IServiceProvider Services => Host?.Services ?? throw new InvalidOperationException("Host not initialized");

    [STAThread]
    public static void Main(string[] args)
    {
        // Load .env file
        DotNetEnv.Env.Load();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/launcher-.log",
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        try
        {
            Log.Information("Starting UnderMine Launcher");

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Build DI container
            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    // Configuration
                    services.Configure<ContextData>(context.Configuration.GetSection("Api"));
                    services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContextData>>().Value);

                    // HTTP Client
                    services.AddSingleton<HttpClient>(sp =>
                    {
                        var contextData = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContextData>>().Value;
                        var client = new HttpClient
                        {
                            BaseAddress = new Uri(contextData.BaseUrl),
                            Timeout = TimeSpan.FromSeconds(30)
                        };
                        return client;
                    });

                    // Services
                    services.AddSingleton<SystemService>();
                    services.AddSingleton<ProgressService>();
                    services.AddSingleton<ProfileManager>();
                    services.AddSingleton<AuthService>();
                    services.AddSingleton<UserClient>();
                    services.AddSingleton<ClientSyncService>();
                    services.AddSingleton<ProcessLauncher>();
                    services.AddSingleton<JavaRuntimeService>();
                    services.AddSingleton<InstallGame>();
                    services.AddLogging();
                })
                .Build();

            // Resolve services
            var contextData = Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContextData>>().Value;
            var progressService = Services.GetRequiredService<ProgressService>();
            var authService = Services.GetRequiredService<AuthService>();
            var userClient = Services.GetRequiredService<UserClient>();
            var installGame = Services.GetRequiredService<InstallGame>();

            VelopackApp.Build().Run();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
