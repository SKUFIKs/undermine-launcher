
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnderMine.Core;
using UnderMine.Fonts.Models;
using UnderMine.Models;
using UnderMine.Network;
using UnderMine.Services;

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

    private readonly IGameInstallerFactory _installerFactory;
    private readonly UserApiService _apiService;

    private static readonly IBrush DefaultColor = Brushes.Orange;
    private static readonly IBrush CompleteColor = Brush.Parse("#3C6");
    private readonly ProgressReporter _reporter;

    public MenuWindow(ContextData contextData, LoginResult loginResult, ProgressReporter reporter)
    {
        InitializeComponent();

        var pathService = new PathService();
        var httpClient = new System.Net.Http.HttpClient();
        var fileDownloader = new FileDownloader(httpClient);

        _progressManager = new ProgressManager((val, msg) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Loader.Progress = Math.Clamp(val, 0, 100);
                PercentText.Text = $"{Math.Round(Loader.Progress)}%";
                LogBox.Content = msg;
            });
        });
        var hytaleService = new HytaleLauncherService(
        fileDownloader,
        _progressManager,
        reporter,
        pathService
    );

        _installerFactory = new GameInstallerFactory(
            new FileDownloader(httpClient),
            new MinecraftLauncherService(Path.Combine(pathService.GetStorageDirectory().Value, "versions")),
            reporter,
            new ProfileService(),
            new UnderMine.Core.ForgeInstaller(Path.Combine(pathService.GetStorageDirectory().Value, "versions")),
            _progressManager,
            new ModLoaderService(httpClient, reporter, _progressManager),
            new ConfigService(httpClient),
            new SettingsService(pathService),
            pathService,
        hytaleService
        );

        var apiService = new UserApiService(contextData, loginResult, reporter);
        _apiService = apiService;
        DataContext = new ServerSelectionService(_installerFactory, apiService);

        _contextData = contextData;
        _loginResult = loginResult;
        _reporter = reporter;

        _reporter.OnLog += UpdateConsoleLabel;

        _ = InitializeMemorySliderAsync();
        InitializeServersAsync();
        Load_Settings();
        Installation();

        LogLabel.IsVisible = false;
    }

    /// <summary>
    ///  перенести в models
    /// <summary>
    public class NewsPart
    {
        public string Type { get; set; } = "Run";
        public string? Text { get; set; }
        public bool IsBold { get; set; }
        public string? Color { get; set; }
        public int? FontSize { get; set; }
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

    /// <summary>
    ///  сделать динамичной(получение ссылок через апи)
    /// <summary>


    private void TelegramLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://t.me/minecraft_urfu_underground_bot",
            UseShellExecute = true
        });
    }

    /// <summary>
    ///  хз че это
    /// <summary>
    private void Installation()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        InstallationPath.Text = baseDirectory;
    }

   
    private async void Load_Settings()
    {
        _userData = await UserData.Load() ?? throw new InvalidOperationException();
        MemorySlider.Value = _userData.MemorySettings;
        JavaPathTextBox.Text = _userData.JavaPath;

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
            var response = await _apiService.GetUserDataAsync();
            if (response == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                UsernameLabel.Content = response.Username;

                if (response.News != null)
                    RenderNewsParts(NewsTextBlock, response.News);

                if (response.ProfileData != null)
                    RenderNewsParts(ProfileTextBlock, response.ProfileData);
            });
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }
    }
    private void RenderNewsParts(TextBlock targetBlock, List<NewsPart> parts)
{
    targetBlock.Inlines.Clear();

    foreach (var part in parts)
    {
        if (part.Type == "LineBreak")
        {
            targetBlock.Inlines.Add(new LineBreak());
        }
        else if (part.Type == "Link")
        {
            var linkText = new TextBlock
            {
                Text = part.Text,
                Foreground = Brush.Parse(part.Color ?? "#747474"),
                FontWeight = FontWeight.Bold,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            linkText.PointerPressed += TelegramLink_PointerPressed;
            targetBlock.Inlines.Add(new InlineUIContainer { Child = linkText });
        }
        else
        {
            var run = new Run(part.Text)
            {
                FontWeight = part.IsBold ? FontWeight.Bold : FontWeight.Normal
            };

            if (!string.IsNullOrEmpty(part.Color))
                run.Foreground = Brush.Parse(part.Color);

            if (part.FontSize.HasValue)
                run.FontSize = part.FontSize.Value;

            targetBlock.Inlines.Add(run);
        }
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
    private async void JavaPathBrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage != null)
        {
            var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите исполняемый файл Java",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Java Executable") { Patterns = new[] { "java.exe", "javaw.exe" } } }
            });

            if (result.Count > 0)
            {
                JavaPathTextBox.Text = result[0].Path.LocalPath;
            }
        }
    }
    private async void StartingTheGame(int serverId)
    {

        PlayButton.IsEnabled = false;
        ProgressBar.IsVisible = true;
        PlayButton.Content = "Запуск...";
        LogLabel.IsVisible = true;

        try
        {
            if (this.DataContext is ServerSelectionService vm && vm.SelectedServer != null)
            {
                var selected = vm.SelectedServer;
                _reporter.Log($"[INFO] Подготовка к установке {selected.Name}");

                var config = selected.Config;
                config.username = _userData?.Username ?? "Player";

                // ВАЖНО: Вызываем метод ИНТЕРФЕЙСА, а не создаем инсталлер вручную
                await _installerFactory.InstallAndRunAsync(config);
            }
        }
        catch (Exception ex)
        {
            HandleException("Ошибка запуска", ex);
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                PlayButton.IsEnabled = true;
                ProgressBar.IsVisible = false;
                PlayButton.Content = "Играть";
                LogLabel.IsVisible = false;

                SettingsButton.IsEnabled = true;
                ProfileButton.IsEnabled = true;
                ServerSelectionButton.IsEnabled = true;
                NewsButton.IsEnabled = true;
            });
        }
    }


    private void SelectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ServerSelectionService vm)
        {
            if (vm.SelectedServer != null)
            {
                var selected = vm.SelectedServer;

                SelectServer.IsVisible = false;
                WaitingСhoice.IsVisible = false;
                PlayServer.IsVisible = true;

                ServerId = selected.Id;

                UpdateServerDescription(selected.Description);
            }
        }
    }
    /// <summary>
    ///  объединить со starting game
    /// <summary>
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
    /// <summary>
    ///  убрать статичные настройки
    /// <summary>
    private async void DefaultButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _userData.MemorySettings = 6144;
        await _userData.Save();

        MemorySlider.Value = _userData.MemorySettings;

    }
    private void UpdateServerDescription(List<NewsPart>? description)
    {
        ServerDescriptionText.Inlines.Clear();
        if (description == null) return;

        foreach (var part in description)
        {
            if (part.Type == "LineBreak")
            {
                ServerDescriptionText.Inlines.Add(new LineBreak());
            }
            else
            {
                var run = new Run(part.Text)
                {
                    FontWeight = part.IsBold ? FontWeight.Bold : FontWeight.Normal
                };

                if (!string.IsNullOrEmpty(part.Color))
                    run.Foreground = Brush.Parse(part.Color);

                if (part.FontSize.HasValue)
                    run.FontSize = part.FontSize.Value;

                ServerDescriptionText.Inlines.Add(run);
            }
        }
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

    /// <summary>
    ///  заменить textbox на label или др
    /// <summary>
    /// 
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