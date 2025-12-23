using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using UnderMine.Core; // Важно: Пространство имен первого сервера
using UnderMine.Core.Services;
using UnderMine.Fonts.Models;
using UnderMine.Models;
using UnderMine.Network.Services;
using UnderMine.Services;
using UnderMine.Services.Location;
using UnderMine.Services.Logger;
using UnderMine.Services.ProgressBar;
using UnderMine.Services.SystemInfo.MemorySettings;

namespace UnderMine.IU;

public partial class MenuWindow : Window
{
    private int ServerId { get; set; }

    // Добавлено пропущенное поле для первого сервера
    private InstalGame1Server _instalGame1Server = null!;

    private readonly ContextData _contextData;
    private readonly LoginResult _loginResult;
    private SliderValues _sliderValues = null!;
    private MemorySliderConfiguration _memoryConfig = null!;
    private ProgressManager _progressManager = null!;
    private UserData _userData = null!;
    private ServerInfo _serverInfo = null!;
    private string _logContent = "";

    // Поле для хранения текущего инсталлера (если нужно для общей логики)
    private object _installer;

    private static readonly IBrush DefaultColor = Brushes.Orange;
    private static readonly IBrush CompleteColor = Brush.Parse("#3C6");

    public MenuWindow(ContextData contextData, LoginResult loginResult)
    {
        InitializeComponent();
        DataContext = new MenuWindowViewModel();
        _contextData = contextData;
        _loginResult = loginResult;

        _ = InitializeMemorySliderAsync();
        InitializeServersAsync();
        Load_Settings();
        Installation();

        Console.SetOut(new Logger(UpdateConsoleLabel));

        MemoryTextBox.Text = $"{MemorySlider.Value:F0} MB";
        MemorySlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(MemorySlider.Value))
            {
                MemoryTextBox.Text = $"{MemorySlider.Value:F0} MB";
            }
        };
    }

    private void UpdateConsoleLabel(string text)
    {
        string cleanText = text.Trim();

        if (string.IsNullOrEmpty(cleanText)) return;

        if (cleanText.Length > 120)
            cleanText = cleanText.Substring(0, 117) + "...";

        LogLabel.Content = cleanText;
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

    private void AddLogMessage(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _logContent = message;
            LogBox.Content = _logContent;
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

        // Убедитесь, что ThisAssembly настроен в проекте, иначе закомментируйте
        string versionFromGitTag = ThisAssembly.Git.Tag;
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
            var api = new UserApiService(_contextData, _loginResult);
            _serverInfo = await api.GetServerInfo();

            if (!_serverInfo.Success)
            {
                AddLogMessage($"Ошибка {_serverInfo.ErrorMessage}");
                ErrorPanel.IsVisible = true;
                ErrorBox.Text = $"Ошибка: {_serverInfo.ErrorMessage}";
                return;
            }


            // 2. Инициализация ProgressReporter
            // ВАЖНО: Мы подписываемся на события репортера, так как сам InstalGame1Server не имеет событий
            // 2. Инициализация ProgressReporter

            // 2. Настройка центрального ProgressBar (Панель и текст внутри неё)
            // Создаем менеджер, который будет управлять Loader (ProgressView) и LogBox (Label)
            

            var pathService = new PathService();
            // Получаем базовый путь один раз для всех
            string rootPath = pathService.GetStorageDirectory().Value;
            string baseGamePath = Path.Combine(rootPath, "versions", "TechnoMagic");

            if (!Directory.Exists(baseGamePath)) Directory.CreateDirectory(baseGamePath);

            _progressManager = new ProgressManager((val, msg) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (!ProgressBar.IsVisible) ProgressBar.IsVisible = true;

                        double actualPercent = Math.Clamp(val, 0, 100);

                        string newText = $"{Math.Round(actualPercent)}%";

                        if (PercentText.Text != newText)
                        {
                            PercentText.Text = newText;

                            PercentText.InvalidateVisual();
                        }

                        LogBox.Content = msg;
                        Loader.Progress = actualPercent;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка UI: {ex.Message}");
                    }
                });
            });

            var httpClient = new System.Net.Http.HttpClient();
            var progressReporter = new ProgressReporter();
            

            // Передаем правильный путь в ConfigService
            var configService = new ConfigService(httpClient, baseGamePath);

            var downloader = new FileDownloader(httpClient);
            var forgeInstallerService = new UnderMine.Core.Services.ForgeInstaller(baseGamePath);
            var launcherService = new MinecraftLauncherService(baseGamePath);
            var profileService = new ProfileService();
            var modLoader = new ModLoaderService(httpClient, progressReporter, _progressManager);
            var settingsService = new SettingsService(pathService);

            _instalGame1Server = new InstalGame1Server(
                _serverInfo,
                downloader,
                launcherService,
                progressReporter,
                profileService,
                forgeInstallerService,
                _progressManager, // Используем поле класса здесь тоже
                modLoader,
                configService,
                settingsService,
                pathService
            );

            _instalGame1Server.InitProgress();

            UsernameLabel.Content = _userData.Username;
            _installer = _instalGame1Server;
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

        // Блокируем кнопки
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

                // Здесь происходит установка и запуск.
                // Ошибки перехватываются блоком catch ниже.
                // Сообщения идут через ProgressReporter, настроенный в InitializeServersAsync.
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
            // Перехват исключений от InstalGame1Server
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

        // Переподписываемся или просто обновляем значение
        // (Обработчик ValueChanged уже должен работать)
    }

    private void OverviewSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        var pathService = new PathService();
        AppPath storage = pathService.GetStorageDirectory();

        string directoryPath = storage.Value; // Убрал Path.Combine, если storage.Value уже путь

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