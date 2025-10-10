using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UnderMine.Models;
using System.Threading.Tasks;
using UnderMine.Network.AuthService;
using System.Text.RegularExpressions;
using Avalonia.Input;
using UnderMine.Network.Services;


namespace UnderMine.IU;

public partial class AuthWindow : Window
{
    private readonly IdentityService _identityService;
    private UserApiService _userApiService;
    private UserData _userData = null!;
    private readonly Config _config;
    private LoginResult _loginResult = null!;

    public AuthWindow()
    {
        InitializeComponent();
        _config = new Config();
        var loginResult = new LoginResult();
        _identityService = new IdentityService(_config);
        _userApiService = new UserApiService(_config, loginResult);
        _ = LoadUserSettings();
        PassBox.PasswordChar = '•';

    }

    private async Task LoadUserSettings()
    {
        _userData = await UserData.Load();
        var saveUser = _userData.SaveUser;
        SaveUserBox.IsChecked = saveUser;

        if (saveUser)
        {
            var username = _userData.Username;
            var password = _userData.Password;

            if (password == null || username == null)
            {
            }
            else
            {
                LogBox.Text = username;
                PassBox.Text = password;
            }
        }
    }

    private async void AuthButton_OnClick(object? sender, RoutedEventArgs e)
    {

        var username = LogBox.Text;
        var password = PassBox.Text;
        
        

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = "Проверьте правильность введённых данных. Все поля должны быть заполнены.";
            return;
        }

        var result = await Login(username, password);

        if (!result.Success)
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = result.ErrorMessage;
            return;
        }
        _loginResult = await Login(username, password);

        if (!_loginResult.Success)
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = _loginResult.ErrorMessage;
            return;
        }

        var secondWindow = new MenuWindow(_config, _loginResult);
        secondWindow.Show();
        this.Close();
        
    }

    private async Task<LoginResult> Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Пустое имя пользователя или пароль."
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
                    "Вы используете недопустимые символы. Для логина и пароля можно использовать только латинские буквы и цифры."
            };
        }
        
        if (SaveUserBox.IsChecked == true)
        {
            _userData.Username = username;
            _userData.Password = password;
            _userData.SaveUser = true;
            await _userData.Save();
        }
        
        var loginResult = await _identityService.Login(username, password);

        if (!loginResult.Success)
        {
            return loginResult;
        }
        
        _userApiService = new UserApiService(_config, loginResult);
        var serverInfo = await _userApiService.GetServerInfo();

        if (!serverInfo.Success)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = serverInfo.ErrorMessage
            };
        }

        return new LoginResult
        {
            Success = true,
            Token = loginResult.Token
        };
    }

    private void LogBox_OnTextInput(object? sender, TextInputEventArgs e)
    {

    }
    private async void RegButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var username = RegLogBox.Text;
        var password = RegPassBox.Text;
        var confirmPassword = ConfPassBox.Text;
        

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = "Проверьте правильность введённых данных. Все поля должны быть заполнены.";
            return;
        }

        var result = await RegistrationMetod(username, password, confirmPassword);

        if (!result.Success)
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = result.ErrorMessage;
            return;
        }

        if (result.Success)
        {
            InfoPanel.IsVisible= true;
            InfoBox.Text= result.ErrorMessage;
        }
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
        
        var result = await _identityService.Register(username, password);
        
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
                _userData.Username = username;
                _userData.Password = password;
                _userData.SaveUser = true;
                await _userData.Save();
            }
        }
        else
        {
            _userData.SaveUser = false;
            _userData.Password = String.Empty;
            _userData.Username = string.Empty;
            await _userData.Save();
        }
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        InfoPanel.IsVisible = false;
        InfoBox.Text = string.Empty;
        RegPanel.IsVisible = false;
        LogPanel.IsVisible = true;
    }
}


