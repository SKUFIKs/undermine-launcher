
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
using System.Globalization;
using System.Threading.Tasks;
using UnderMine.Core.Network;
using UnderMine.Fonts.Models;
using UnderMine.Models;
using UnderMine.Services;

namespace UnderMine.IU;

public partial class MenuWindow : Window
{
    private static readonly System.Net.Http.HttpClient _sharedHttpClient = new();

    private int ServerId { get; set; }
    private readonly ContextData _contextData;
    private readonly LoginResult _loginResult;
    private readonly ProgressService _reporter;
    private readonly UserClient _apiService;
    private ProfileManager _userData = null!;
    private SystemService _memoryConfig = null!;
    private SliderValues _sliderValues = null!;

    public MenuWindow(ContextData contextData, LoginResult loginResult, ProgressService reporter)
    {
        InitializeComponent();

        //InitializeMemorySliderAsync();
        _contextData = contextData;
        _loginResult = loginResult;
        _reporter = reporter;

        // 1. Инициализируем сервисы, используя общий HttpClient
        var pathService = new SystemService();
        


        _apiService = new UserClient(contextData, loginResult, reporter);

        // Устанавливаем DataContext для биндинга списка серверов
        // DataContext = new ServerSelectionService(_installerFactory, _apiService);

        // 2. Подписки и начальная загрузка
        _reporter.OnLog += UpdateConsoleLabel;

        _ = InitWindowAsync();

        LogLabel.IsVisible = false;
    }

    private async Task InitWindowAsync()
    {
        Installation(); // Показываем путь
        await LoadSettingsAsync(); // Грузим настройки юзера
        await InitializeServersAsync(); // Грузим список серверов
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


    private async Task LoadSettingsAsync()
    {
        _userData = await ProfileManager.Load() ?? new ProfileManager();

        MemorySlider.Value = _userData.MemorySettings;
        JavaPathTextBox.Text = _userData.JavaPath;

        // Подписываемся на изменения
        MemorySlider.ValueChanged += (s, e) => _userData.MemorySettings = (int)e.NewValue;

        Version.Content = $"{ThisAssembly.Git.BaseTag}";
    }
    /*
    private async Task InitializeMemorySliderAsync()
    {
        // 1. Используем уже существующий репортер или создаем сканер напрямую
        // Предполагаем, что SystemScanner доступен
        var scanner = new SystemService();

        _memoryConfig = new MemorySliderConfiguration(scanner);
        _sliderValues = await _memoryConfig.InitializeMemorySliderAsync();

        // 2. Настройка ползунка (Slider в Avalonia)
        // Используем инвариантную культуру для парсинга, чтобы не упасть на запятых
        MemorySlider.Minimum = double.Parse(_sliderValues.FirstValue, CultureInfo.InvariantCulture);
        MemorySlider.Maximum = double.Parse(_sliderValues.FifthValue, CultureInfo.InvariantCulture);
        MemorySlider.TickFrequency = _sliderValues.Step;
        MemorySlider.IsSnapToTickEnabled = true; // Чтобы ползунок прилипал к делениям

        // 3. Обновление текстовых меток под ползунком
        FirstValue.Content = _sliderValues.FirstValue;
        SecondValue.Content = _sliderValues.SecondValue;
        ThirdValue.Content = _sliderValues.ThirdValue;
        FourthValue.Content = _sliderValues.FourthValue;
        FifthValue.Content = _sliderValues.FifthValue;
    }
    */
    private async Task InitializeServersAsync()
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
    


    private void SelectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        /*
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
        */
    }
    /// <summary>
    ///  объединить со starting game
    /// <summary>
    private async void PlayButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Блокируем UI
        SetGlobalUIEnabled(false);
        PlayButton.Content = "Запуск...";
        ProgressBar.IsVisible = true;
        LogLabel.IsVisible = true;
        /*
        try
        {
            if (DataContext is ServerSelectionService vm && vm.SelectedServer != null)
            {
                var selected = vm.SelectedServer;
                _reporter.Log($"[INFO] Запуск {selected.Name}...");

                var config = selected.Config;
                config.username = _userData?.Username ?? "Player";

                // Запускаем через фабрику
                await _installerFactory.InstallAndRunAsync(config);
            }
        }
        catch (Exception ex)
        {
            HandleException("Ошибка при запуске игры", ex);
        }
        finally
        {
            // Возвращаем UI в исходное состояние
            PlayButton.IsEnabled = true;
            PlayButton.Content = "Играть";
            ProgressBar.IsVisible = false;
            LogLabel.IsVisible = false;
            SetGlobalUIEnabled(true);
        }
        */
    }
    private void SetGlobalUIEnabled(bool enabled)
    {
        SettingsButton.IsEnabled = enabled;
        ProfileButton.IsEnabled = enabled;
        ServerSelectionButton.IsEnabled = enabled;
        NewsButton.IsEnabled = enabled;
        PlayButton.IsEnabled = enabled;
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
        var pathService = new SystemService();
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