using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnderMine.Core.Network;
using UnderMine.Models;
using UnderMine.Services;


namespace UnderMine.IU;

public partial class AuthWindow : Window
{
    private readonly AuthService _authService;
    private readonly ProgressService _reporter;
    private readonly ContextData _contextData;
    private ProfileManager _userData = null!;


    public AuthWindow()
    {
        InitializeComponent();

        _reporter = new ProgressService();
        _contextData = new ContextData();

        _authService = new AuthService(new System.Net.Http.HttpClient(), _contextData, _reporter);

        _ = InitAsync();

        PassBox.PasswordChar = '•';
    }
 
    private async Task InitAsync()
    {
        await LoadUserSettings();
        ShowVersion();
    }
    private void ShowVersion()
    {
        Version.Content = $"{ThisAssembly.Git.BaseTag}";
    }

    private async Task LoadUserSettings()
    {
        _userData = await ProfileManager.Load();
        if (_userData.SaveUser)
        {
            SaveUserBox.IsChecked = true;
            LogBox.Text = _userData.Username;
            PassBox.Text = _userData.Password; 
        }
    }

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
                _userData.Password = password;
                _userData.SaveUser = true;
                await _userData.Save();
            }

            var secondWindow = new MenuWindow(_contextData, result, _reporter);
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
            return new LoginResult { ErrorMessage = "Заполните все поля" };

        if (user.Length < 3 || pass.Length < 6)
            return new LoginResult { ErrorMessage = "Данные слишком короткие" };

        if (!Regex.IsMatch(user, @"^[a-zA-Z0-9]+$"))
            return new LoginResult { ErrorMessage = "Используйте только латиницу и цифры" };

        return new LoginResult { Success = true };
    }
    private void ShowError(string? msg)
    {
        ErrorPanel.IsVisible = true;
        ErrorBox.Text = msg ?? "Неизвестная ошибка";
    }
    private void SetUIEnabled(bool enabled)
    {
        AuthButton.IsEnabled = enabled;
        RegButton.IsEnabled = enabled; 
    }
    private void LogBox_OnTextInput(object? sender, TextInputEventArgs e)
    {
    }

    private async void RegButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AuthButton.IsEnabled = false;
        var username = RegLogBox.Text;
        var password = RegPassBox.Text;
        var confirmPassword = ConfPassBox.Text;


        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirmPassword))
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = "Проверьте правильность введённых данных. Все поля должны быть заполнены.";
            AuthButton.IsEnabled = true;
            return;
        }

        var result = await RegistrationMetod(username, password, confirmPassword);

        if (!result.Success)
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = result.ErrorMessage;
            AuthButton.IsEnabled = true;
            return;
        }

        if (result.Success)
        {
            InfoPanel.IsVisible = true;
            InfoBox.Text = result.ErrorMessage;
            AuthButton.IsEnabled = true;
        }
        AuthButton.IsEnabled = true;
    }

    public async Task<LoginResult> RegistrationMetod(string username, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Пустое имя пользователя или пароль."
            };
        }

        if (password != confirmPassword)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Пароли не совпадают."
            };
        }

        if (password.Length < 6 || username.Length < 3)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Никнейм или пароль слишком короткий."
            };
        }

        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9]+$") ||
            !Regex.IsMatch(password, @"^[a-zA-Z0-9]+$"))
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage =
                    "Вы используете недопустимые символы. Можно использовать только латинские буквы и цифры."
            };
        }

        var result = await _authService.Register(username, password);

        if (!result.Success)
        {
            return result;
        }

        return new LoginResult
        {
            Success = true,
            ErrorMessage = "Регистрация прошла успешно."
        };
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
    

    private void CloseErrorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ErrorPanel.IsVisible = false;
        ErrorBox.Text = string.Empty;
    }

    private void ShowPassBox_OnChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox)
        {
            if (ShowPassBox.IsChecked == true)
            {
                PassBox.PasswordChar = '\0';
            }
            else
            {
                PassBox.PasswordChar = '•';
            }
        }
    }

    private async void SaveUserBox_OnChecked(object? sender, RoutedEventArgs e)
    {
        if (SaveUserBox.IsChecked == true)
        {
            var username = LogBox.Text;
            var password = PassBox.Text;

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                if (_userData != null)
                {
                    _userData.Username = username;
                    _userData.Password = HashPassword(password); 
                    _userData.SaveUser = true;
                    await _userData.Save();
                }
            }
        }
        else
        {
            _userData!.SaveUser = false;
            _userData.Password = string.Empty;
            _userData.Username = string.Empty;
            await _userData.Save();
        }
    }
    /// <summary>
    ///  перенести в сохранение настроек профиля
    /// <summary>
    private string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            StringBuilder builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2")); 
            }
            return builder.ToString();
        }
    }
    
    
    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        InfoPanel.IsVisible = false;
        InfoBox.Text = string.Empty;
        RegPanel.IsVisible = false;
        LogPanel.IsVisible = true;
    }
    private async void AcceptButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (AgreementCheckBox.IsChecked == true)
            {
                var userData = await ProfileManager.Load();
                if (userData != null)
                {
                    NavigateToMainWindow();
                }
            }
            else
            {
                
            }
        }

        private async void RefusalButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var userData = await ProfileManager.Load();
            if (userData != null)
            {
            }
            CloseApplication();
        }
        
        private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e); 
            }
        }
        

        private void NavigateToMainWindow()
        {
            AgreementPanel.IsVisible = false;
        }

        private void CloseApplication()
        {
            Environment.Exit(0);
        }
        
}
