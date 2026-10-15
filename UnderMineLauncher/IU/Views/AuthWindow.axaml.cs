using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnderMine.Core.Game;
using UnderMine.Core.Network;
using UnderMine.Models;
using UnderMine.Services;
using Microsoft.Extensions.Logging;

namespace UnderMine.IU;

public partial class AuthWindow : Window
{
    private readonly AuthService _authService;
    private readonly ProgressService _reporter;
    private readonly ContextData _contextData;
    private readonly InstallGame _installGame;
    private readonly ProfileManager _userData;
    private readonly ILogger<AuthWindow> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public AuthWindow(
        AuthService authService,
        ProgressService reporter,
        ContextData contextData,
        InstallGame installGame,
        ProfileManager userData,
        ILoggerFactory loggerFactory)
    {
        InitializeComponent();

        _authService = authService;
        _reporter = reporter;
        _contextData = contextData;
        _installGame = installGame;
        _userData = userData;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AuthWindow>();

        _ = InitAsync();

        PassBox.PasswordChar = '•';
    }

    private async Task InitAsync()
    {
        await LoadUserSettings();
        ShowVersion();
    }

    private void ShowVersion() => Version.Content = $"{ThisAssembly.Git.BaseTag}";

    private async void AuthButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetUIEnabled(false);

        var username = LogBox.Text;
        var password = PassBox.Text;

        var validation = ValidateInput(username, password);
        if (!validation.Success)
        {
            ShowError(validation.ErrorMessage);
            SetUIEnabled(true);
            return;
        }

        var result = await _authService.Login(username, password);

        if (result.Success)
        {
            if (SaveUserBox.IsChecked == true && _userData != null)
            {
                _userData.Username = username;
                _userData.SaveUser = true;
                await _userData.Save();
            }

            var secondWindow = new MenuWindow(_contextData, result, _reporter, _installGame, _loggerFactory.CreateLogger<MenuWindow>());
            secondWindow.Show();
            this.Close();
        }
        else
        {
            ShowError(result.ErrorMessage);
            SetUIEnabled(true);
        }
    }

    private LoginResult ValidateInput(string? user, string? pass)
    {
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            return new LoginResult { ErrorMessage = "Fill in all fields" };

        if (user.Length < 3 || pass.Length < 6)
            return new LoginResult { ErrorMessage = "Data too short" };

        if (!Regex.IsMatch(user, @"^[a-zA-Z0-9]+$"))
            return new LoginResult { ErrorMessage = "Use only Latin letters and numbers" };

        return new LoginResult { Success = true };
    }

    private async void RegButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetUIEnabled(false);

        var username = RegLogBox.Text;
        var password = RegPassBox.Text;
        var confirmPassword = ConfPassBox.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Fill in all fields");
            SetUIEnabled(true);
            return;
        }

        var result = await RegistrationMetod(username, password, confirmPassword);

        if (!result.Success)
        {
            ShowError(result.ErrorMessage);
        }
        else
        {
            ShowSuccess("Registration successful!");

            await Task.Delay(1000);
            LoginButton_OnClick(null, null);
        }

        SetUIEnabled(true);
    }

    public async Task<LoginResult> RegistrationMetod(string username, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResult { Success = false, ErrorMessage = "Empty username or password." };
        }

        if (password != confirmPassword)
        {
            return new LoginResult { Success = false, ErrorMessage = "Passwords do not match." };
        }

        if (password.Length < 6 || username.Length < 3)
        {
            return new LoginResult { Success = false, ErrorMessage = "Nickname or password too short." };
        }

        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9]+$") || !Regex.IsMatch(password, @"^[a-zA-Z0-9]+$"))
        {
            return new LoginResult { Success = false, ErrorMessage = "You are using invalid characters. Only Latin letters and numbers are allowed." };
        }

        var result = await _authService.Register(username, password);

        if (!result.Success) return result;

        return new LoginResult { Success = true, ErrorMessage = "Registration successful." };
    }

    private async Task LoadUserSettings()
    {
        var loaded = await ProfileManager.Load();
        if (loaded.SaveUser)
        {
            SaveUserBox.IsChecked = true;
            LogBox.Text = loaded.Username;
        }
    }

    private async void SaveUserBox_OnChecked(object? sender, RoutedEventArgs e)
    {
        if (SaveUserBox.IsChecked == true)
        {
            var username = LogBox.Text;

            if (!string.IsNullOrEmpty(username))
            {
                if (_userData != null)
                {
                    _userData.Username = username;
                    _userData.SaveUser = true;
                    await _userData.Save();
                }
            }
        }
        else
        {
            _userData!.SaveUser = false;
            _userData.Username = string.Empty;
            await _userData.Save();
        }
    }

    private void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RegPanel.IsVisible = false;
        LogPanel.IsVisible = true;
    }

    private void Registration_OnClick(object? sender, RoutedEventArgs e)
    {
        RegPanel.IsVisible = true;
        LogPanel.IsVisible = false;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RegPanel.IsVisible = false;
        LogPanel.IsVisible = true;
    }

    private void NavigateToMainWindow()
    {
        AgreementPanel.IsVisible = false;
    }

    private async void ShowError(string? msg)
    {
        if (MainToast != null)
        {
            await MainToast.ShowMessage(msg ?? "Unknown error", isError: true);
        }
    }

    private async void ShowSuccess(string msg)
    {
        if (MainToast != null)
        {
            await MainToast.ShowMessage(msg, isError: false);
        }
    }

    private async void AcceptButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (AgreementCheckBox.IsChecked == true)
        {
            var userData = await ProfileManager.Load();
            if (userData != null) NavigateToMainWindow();
        }
    }

    private async void RefusalButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseApplication();
    }

    private void SetUIEnabled(bool enabled)
    {
        AuthButton.IsEnabled = enabled;
        RegButton.IsEnabled = enabled;
    }

    private void LogBox_OnTextInput(object? sender, TextInputEventArgs e) { }

    private void ShowPassBox_OnChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox)
        {
            PassBox.PasswordChar = ShowPassBox.IsChecked == true ? '\0' : '•';
        }
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    private void CloseApplication() => Environment.Exit(0);
}
