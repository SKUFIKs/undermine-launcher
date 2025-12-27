
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnderMine.Core; 
using UnderMine.Core.Services;
using UnderMine.Fonts.Models;
using UnderMine.Models;
using UnderMine.Network;
using UnderMine.Services;
using UnderMine.Services.Location;
using UnderMine.Services.Logger;
using UnderMine.Services.ProgressBar;
using UnderMine.Services.SystemInfo.MemorySettings;

namespace UnderMine.IU;

public partial class MenuWindow : Window
{
    private int ServerId { get; set; }

    private InstalGame1Server _instalGame1Server = null!;

    private readonly ContextData _contextData;
    private readonly LoginResult _loginResult;
    private SliderValues _sliderValues = null!;
    private MemorySliderConfiguration _memoryConfig = null!;
    private ProgressManager _progressManager = null!;
    private UserData _userData = null!;
    private ServerInfo _serverInfo = null!;
    private string _logContent = "";

    private object _installer;

    private static readonly IBrush DefaultColor = Brushes.Orange;
    private static readonly IBrush CompleteColor = Brush.Parse("#3C6");
    private readonly ProgressReporter _reporter;

    public MenuWindow(ContextData contextData, LoginResult loginResult, ProgressReporter reporter)
    {
        InitializeComponent();
        DataContext = new MenuWindowViewModel();
        _contextData = contextData;
        _loginResult = loginResult;
        _reporter = reporter; 

        _reporter.OnLog += UpdateConsoleLabel;

        _ = InitializeMemorySliderAsync();
        InitializeServersAsync();
        Load_Settings();
        Installation();

        LogLabel.IsVisible = false;


        MemoryTextBox.Text = $"{MemorySlider.Value:F0} MB";
        MemorySlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(MemorySlider.Value))
                MemoryTextBox.Text = $"{MemorySlider.Value:F0} MB";
        };
    }

    private void UpdateConsoleLabel(string text)
    {

        Dispatcher.UIThread.Post(() =>
        {
            string cleanText = text.Trim();
            if (string.IsNullOrEmpty(cleanText)) return;

            if (cleanText.Length > 120)
                cleanText = cleanText.Substring(0, 117) + "...";

            LogLabel.Content = cleanText;
        });
    }

    private void TitleBar_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    private void TelegramLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://t.me/minecraft_urfu_underground_bot",
            UseShellExecute = true
        });
    }

    private void Installation()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        InstallationPath.Text = baseDirectory;
    }

    private async void Load_Settings()
    {
        _userData = await UserData.Load() ?? throw new InvalidOperationException();
        MemorySlider.Value = _userData.MemorySettings;

        MemorySlider.ValueChanged += (s, e) =>
        {
            _userData.MemorySettings = (int)e.NewValue;
        };

        string versionFromGitTag = ThisAssembly.Git.BaseTag;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Version.Content = $"{versionFromGitTag}   ";
        });
    }

    private async Task InitializeMemorySliderAsync()
    {
        var check = new CheckEquipment(new SystemInfo());
        var systemInfo = await check.CheckOperatingSystemAsync();

        _memoryConfig = new MemorySliderConfiguration(systemInfo);
        _sliderValues = await _memoryConfig.InitializeMemorySliderAsync();

        MemorySlider.Minimum = int.Parse(_sliderValues.FirstValue);
        MemorySlider.TickFrequency = _sliderValues.Step;
        MemorySlider.Maximum = int.Parse(_sliderValues.FifthValue);

        FirstValue.Content = _sliderValues.FirstValue;
        SecondValue.Content = _sliderValues.SecondValue;
        ThirdValue.Content = _sliderValues.ThirdValue;
        FourthValue.Content = _sliderValues.FourthValue;
        FifthValue.Content = _sliderValues.FifthValue;
    }

    private async void InitializeServersAsync()
    {
        try
        {
            var api = new UserApiService(_contextData, _loginResult, _reporter);
            _serverInfo = await api.GetServerInfo();

            if (!_serverInfo.Success)
            {
                HandleException("Ошибка получения данных сервера", new Exception(_serverInfo.ErrorMessage));
                return;
            }

            var pathService = new PathService();
            string baseGamePath = Path.Combine(pathService.GetStorageDirectory().Value, "versions", "TechnoMagic");
            if (!Directory.Exists(baseGamePath)) Directory.CreateDirectory(baseGamePath);

            _progressManager = new ProgressManager((val, msg) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressBar.IsVisible = true;
                    Loader.Progress = Math.Clamp(val, 0, 100);
                    PercentText.Text = $"{Math.Round(Loader.Progress)}%";
                    LogBox.Content = msg;
                });
            });

            var httpClient = new System.Net.Http.HttpClient();

            var modLoader = new ModLoaderService(httpClient, _reporter, _progressManager);
            var forgeInstaller = new UnderMine.Core.Services.ForgeInstaller(baseGamePath);

            _instalGame1Server = new InstalGame1Server(
                _serverInfo,
                new FileDownloader(httpClient),
                new MinecraftLauncherService(baseGamePath),
                _reporter, 
                new ProfileService(),
                forgeInstaller,
                _progressManager,
                modLoader,
                new ConfigService(httpClient, baseGamePath),
                new SettingsService(pathService),
                pathService
            );

            _instalGame1Server.InitProgress();
            UsernameLabel.Content = _userData.Username;
        }
        catch (Exception ex)
        {
            HandleException("Ошибка инициализации компонентов", ex);
        }
    }

    private void NewsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        NewsPanel.IsVisible = true;
        SelectServer.IsVisible = false;
        ProfilePanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        WaitingСhoice.IsVisible = false;
        PlayServer.IsVisible = false;
    }

    private void ServerSelectionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectServer.IsVisible = true;
        WaitingСhoice.IsVisible = true;
        ProfilePanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        NewsPanel.IsVisible = false;
        PlayServer.IsVisible = false;
    }

    private void ProfileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ProfilePanel.IsVisible = true;
        SettingsPanel.IsVisible = false;
        NewsPanel.IsVisible = false;
        SelectServer.IsVisible = false;
        WaitingСhoice.IsVisible = false;
        PlayServer.IsVisible = false;
    }

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SettingsPanel.IsVisible = true;
        ProfilePanel.IsVisible = false;
        NewsPanel.IsVisible = false;
        SelectServer.IsVisible = false;
        WaitingСhoice.IsVisible = false;
        PlayServer.IsVisible = false;
    }

    private void ExitButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseErrorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ErrorPanel.IsVisible = false;
        ErrorBox.Text = string.Empty;
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        NewsPanel.IsVisible = true;
        SelectServer.IsVisible = false;
        ProfilePanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        WaitingСhoice.IsVisible = false;
        PlayServer.IsVisible = false;
    }

    private async void StartingTheGame(int serverId)
    {
        PlayButton.IsEnabled = false;
        ProgressBar.IsVisible = true;
        PlayButton.Content = "Запуск";
        LogLabel.IsVisible = true;
        SettingsButton.IsEnabled = false;
        ProfileButton.IsEnabled = false;
        ServerSelectionButton.IsEnabled = false;
        NewsButton.IsEnabled = false;

        try
        {
            if (serverId == 1)
            {
                if (_instalGame1Server == null)
                {
                    throw new InvalidOperationException("Сервер 1 не был инициализирован.");
                }

                await _instalGame1Server.InstallAndRunAsync();
            }
            else if (serverId == 2)
            {
                //if (_instalGame2Server != null)
                  //  await _instalGame2Server.InstallAndRunAsync();
            }
        }
        catch (Exception ex)
        {
            HandleException("Ошибка при запуске игры", ex);
        }
        finally
        {
            PlayButton.IsEnabled = true;
            ProgressBar.IsVisible = false;
            PlayButton.Content = "Играть";
            SettingsButton.IsEnabled = true;
            ProfileButton.IsEnabled = true;
            ServerSelectionButton.IsEnabled = true;
            NewsButton.IsEnabled = true;
            LogLabel.IsVisible = false;
        }
    }

    private void SelectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MenuWindowViewModel vm)
        {
            if (vm.SelectedServer != null)
            {
                var serverId = vm.SelectedServer.Id;
                if (serverId == 1 || serverId == 2)
                {
                    SelectServer.IsVisible = false;
                    WaitingСhoice.IsVisible = false;
                    PlayServer.IsVisible = true;
                    ServerId = serverId;

                    if (serverId == 2) Console.WriteLine(serverId);
                }
            }
        }
    }

    private void PlayButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var serverId = ServerId;
        StartingTheGame(serverId);
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_userData != null)
            await _userData.Save();
    }

    private async void DefaultButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _userData.MemorySettings = 6144;
        await _userData.Save();

        MemorySlider.Value = _userData.MemorySettings;

    }

    private void OverviewSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        var pathService = new PathService();
        AppPath storage = pathService.GetStorageDirectory();

        string directoryPath = storage.Value; 

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            HandleException("Не удалось открыть директорию настроек", ex);
        }
    }

    private void LogsSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        // Реализация открытия логов
    }

    private void HandleException(string userMessage, Exception ex)
    {
        Console.WriteLine($"[CRITICAL ERROR] {userMessage}: {ex}");

        Dispatcher.UIThread.Post(() =>
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = $"{userMessage}: {ex.Message}";
        });
    }
}