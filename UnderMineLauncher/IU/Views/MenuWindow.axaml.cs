using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Core.Game;
using UnderMine.Core.Network;
using UnderMine.Models;
using UnderMine.Services;
using Microsoft.Extensions.Logging;

namespace UnderMine.IU;

public partial class MenuWindow : Window
{
    private static readonly System.Net.Http.HttpClient _sharedHttpClient = new();

    private int ServerId { get; set; }
    private readonly ContextData _contextData;
    private readonly LoginResult _loginResult;
    private readonly ProgressService _reporter;
    private readonly UserClient _apiService;
    private readonly InstallGame _installGame;
    private ProfileManager _userData = null!;
    private readonly ILogger<MenuWindow> _logger;

    private UserDto? _userInfo;

    private CancellationTokenSource? _installCts;
    private bool _isInstalling;

    public MenuWindow(ContextData contextData, LoginResult loginResult, ProgressService reporter, InstallGame installGame, ILogger<MenuWindow> logger)
    {
        InitializeComponent();

        _contextData = contextData;
        _loginResult = loginResult;
        _installGame = installGame;
        _reporter = reporter;
        _logger = logger;

        _apiService = new UserClient(_sharedHttpClient, _contextData, _reporter);

        _reporter.OnLog += UpdateConsoleLabel;
        _reporter.OnProgressUpdate += UpdateProgress;

        LogLabel.IsVisible = true;
        PlayServer.IsVisible = true; // Servers — стартовая вкладка, показываем её сразу

        _ = InitWindowAsync();
    }

    private async void ShowError(string? msg)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (MainToast != null)
            {
                await MainToast.ShowMessage(msg ?? "Unknown error", isError: true);
            }
        });
    }

    private async void ShowSuccess(string msg)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (MainToast != null)
            {
                await MainToast.ShowMessage(msg, isError: false);
            }
        });
    }

    private async Task InitWindowAsync()
    {
        var settingsTask = LoadSettingsAsync();
        var userDataTask = LoadUserDataAsync();
        var serverListTask = LoadServerList();

        await Task.WhenAll(settingsTask, userDataTask, serverListTask);
    }

    public async Task LoadUserDataAsync()
    {
        try
        {
            _userInfo = await _apiService.GetUserInfo(_loginResult);

            if (_userInfo == null)
                throw new InvalidOperationException("User data not received.");

            UsernameLabel.Content = _userInfo.Username;
        }
        catch (Exception ex)
        {
            HandleException("Error loading user data", ex);
        }
    }

    private void ProfileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        HideAllPanels();
        ProfilePanel.IsVisible = true;
    }

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        HideAllPanels();
        SettingsPanel.IsVisible = true;
    }

    private void ServerSelectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        HideAllPanels();
        PlayServer.IsVisible = true;
    }

    private void HideAllPanels()
    {
        SettingsPanel.IsVisible = false;
        ProfilePanel.IsVisible = false;
        PlayServer.IsVisible = false;
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ProfilePanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        PlayServer.IsVisible = false;
    }

    private async Task LoadServerList()
    {
        try
        {
            var serverLists = await _apiService.GetServerList(_loginResult, CancellationToken.None);

            Dispatcher.UIThread.Post(() =>
            {
                ServerListBox.ItemsSource = serverLists;
                EmptyServersText.IsVisible = serverLists is null || serverLists.Count == 0;
            });
        }
        catch (Exception ex)
        {
            HandleException("Error loading server list", ex);
            EmptyServersText.IsVisible = true;
        }
    }

    private async void PlayButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // While installing, the Play button doubles as a Cancel button.
        if (_isInstalling)
        {
            CancelInstall();
            return;
        }

        if (ServerListBox.SelectedItem is not ServerList selectedServer)
        {
            ShowError("Select a server from the list before launching.");
            return;
        }

        if (_userInfo == null)
        {
            ShowError("User data not loaded yet. Wait a moment...");
            await LoadUserDataAsync();
            if (_userInfo == null) return;
        }

        if (!int.TryParse(selectedServer.Id, out int parsedId))
        {
            ShowError("Server data error: invalid ID.");
            return;
        }

        _installCts = new CancellationTokenSource();

        try
        {
            BeginInstallUI();
            this.ServerId = parsedId;

            var options = new LauncherOptions
            {
                MaxMemoryMb = _userData?.MemorySettings ?? 4096,
                JavaPath = string.IsNullOrWhiteSpace(JavaPathTextBox.Text) ? null : JavaPathTextBox.Text.Trim(),
                InstallationPath = string.IsNullOrWhiteSpace(InstallationPath.Text) ? null : InstallationPath.Text.Trim(),
                LauncherVersion = $"{ThisAssembly.Git.BaseTag}"
            };

            bool launchResult = await _installGame.InstallAndLaunchGame(
                _contextData,
                _loginResult,
                parsedId,
                _userInfo,
                options,
                _installCts.Token);

            if (launchResult)
                ShowSuccess($"{selectedServer.ServerName} launched!");
            else if (!_installCts.IsCancellationRequested)
                ShowError("Launch failed. Check the logs.");
        }
        catch (Exception ex)
        {
            HandleException("Critical error during launch", ex);
        }
        finally
        {
            EndInstallUI();
        }
    }

    /// <summary>Switches the UI into "installing" mode: overlay on, Play button becomes Cancel.</summary>
    private void BeginInstallUI()
    {
        _isInstalling = true;
        SetNavigationEnabled(false);
        PlayButton.IsEnabled = true;
        PlayButton.Content = "CANCEL";
        ProgressBar.IsVisible = true;
        Loader.Progress = 0;
        PercentText.Text = "0%";
        LogBox.Content = "Preparing...";
    }

    private void EndInstallUI()
    {
        _isInstalling = false;
        _installCts?.Dispose();
        _installCts = null;

        SetNavigationEnabled(true);
        PlayButton.IsEnabled = true;
        PlayButton.Content = "PLAY";
        ProgressBar.IsVisible = false;
    }

    private void CancelInstall()
    {
        try { _installCts?.Cancel(); } catch { /* ignored */ }

        PlayButton.Content = "CANCELING...";
        PlayButton.IsEnabled = false;
        LogBox.Content = "Cancelling...";
    }

    /// <summary>Receives 0..100 install progress and updates the overlay.</summary>
    private void UpdateProgress(string message, double value, string tag)
    {
        if (!ProgressBar.IsVisible) ProgressBar.IsVisible = true;

        Loader.Progress = value;
        PercentText.Text = $"{(int)Math.Round(value)}%";

        if (!string.IsNullOrWhiteSpace(message))
            LogBox.Content = message;
    }

    private void SelectButton_OnClick(object? sender, RoutedEventArgs e) { }

    private async Task LoadSettingsAsync()
    {
        _userData = await ProfileManager.Load() ?? new ProfileManager();

        JavaPathTextBox.Text = _userData.JavaPath;

        InstallationPath.Text = string.IsNullOrWhiteSpace(_userData.InstallationPath)
            ? new SystemService().GetStorageDirectory().Value
            : _userData.InstallationPath;

        Version.Content = $"{ThisAssembly.Git.BaseTag}";

        await InitializeMemorySliderAsync();

        // Если сохранённое значение вне диапазона слайдера — возвращаемся к дефолту (и зажимаем)
        var memory = _userData.MemorySettings;
        if (memory < MemorySlider.Minimum || memory > MemorySlider.Maximum)
            memory = 6144;
        memory = (int)Math.Clamp(memory, MemorySlider.Minimum, MemorySlider.Maximum);

        _userData.MemorySettings = memory;
        MemorySlider.Value = memory;
        MemoryTextBox.Text = memory.ToString();

        MemorySlider.ValueChanged += (s, e) =>
        {
            _userData.MemorySettings = (int)e.NewValue;
            MemoryTextBox.Text = ((int)e.NewValue).ToString();
        };
    }

    /// <summary>
    /// Настраивает слайдер памяти и подписи шкалы по данным системы (см. SystemService.InitializeMemorySliderAsync).
    /// </summary>
    private async Task InitializeMemorySliderAsync()
    {
        try
        {
            var scale = await new SystemService().InitializeMemorySliderAsync();
            if (scale.Labels.Count < 5)
                return;

            var min = ParseLabel(scale.FirstValue, MemorySlider.Minimum);
            var max = ParseLabel(scale.FifthValue, MemorySlider.Maximum);
            if (max <= min)
                return;

            MemorySlider.Minimum = min;
            MemorySlider.Maximum = max;

            var step = scale.Step > 0 ? scale.Step : (max - min) / 4;
            MemorySlider.TickFrequency = step;
            MemorySlider.SmallChange = step;
            MemorySlider.LargeChange = step;

            FirstValue.Content = scale.FirstValue;
            SecondValue.Content = scale.SecondValue;
            ThirdValue.Content = scale.ThirdValue;
            FourthValue.Content = scale.FourthValue;
            FifthValue.Content = scale.FifthValue;
        }
        catch
        {
            // Системные значения недоступны — остаются значения по умолчанию из разметки
        }
    }

    private static double ParseLabel(string label, double fallback)
        => double.TryParse(label, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_userData == null) return;

        _userData.MemorySettings = (int)Math.Clamp(MemorySlider.Value, MemorySlider.Minimum, MemorySlider.Maximum);
        _userData.JavaPath = string.IsNullOrWhiteSpace(JavaPathTextBox.Text) ? null : JavaPathTextBox.Text.Trim();
        _userData.InstallationPath = string.IsNullOrWhiteSpace(InstallationPath.Text) ? null : InstallationPath.Text.Trim();

        await _userData.Save();
        ShowSuccess("Settings saved.");
    }

    private async void DefaultButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_userData == null) return;

        var memory = (int)Math.Clamp(6144, MemorySlider.Minimum, MemorySlider.Maximum);

        _userData.MemorySettings = memory;
        _userData.JavaPath = null;
        _userData.InstallationPath = null;
        await _userData.Save();

        MemorySlider.Value = memory;
        MemoryTextBox.Text = memory.ToString();
        JavaPathTextBox.Text = string.Empty;
        InstallationPath.Text = new SystemService().GetStorageDirectory().Value;

        ShowSuccess("Settings reset to defaults.");
    }

    private async void JavaPathBrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage != null)
        {
            var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Java executable",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Java Executable") { Patterns = new[] { "java.exe", "javaw.exe" } } }
            });

            if (result.Count > 0)
            {
                JavaPathTextBox.Text = result[0].Path.LocalPath;
            }
        }
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

    private void SetNavigationEnabled(bool enabled)
    {
        SettingsButton.IsEnabled = enabled;
        ProfileButton.IsEnabled = enabled;
        ServerSelectionButton.IsEnabled = enabled;
        ServerListBox.IsEnabled = enabled;
    }

    private void HandleException(string context, Exception ex)
    {
        _reporter.Log($"[{context}] Error: {ex.Message}", "CRITICAL");

        ShowError($"{context}: {ex.Message}");
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    private async void OverviewSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        try
        {
            var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the game installation folder",
                AllowMultiple = false
            });

            if (result.Count > 0)
                InstallationPath.Text = result[0].Path.LocalPath;
        }
        catch (Exception ex)
        {
            HandleException("Failed to select installation folder", ex);
        }
    }

    private void LogsSettings_OnClick(object? sender, RoutedEventArgs e) { }

    private void ExitButton_OnClick(object? sender, RoutedEventArgs e) => Close();
}
