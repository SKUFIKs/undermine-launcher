using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UnderMine.Models;
using System.Threading.Tasks;
using UnderMine.Network.AuthService;
using System.Text.RegularExpressions;
using Avalonia.Input;
using UnderMine.Network.Services;
using UnderMine.Services;


namespace UnderMine.IU;

public partial class AuthWindow : Window
{
    private readonly IdentityService _identityService;
    private UserApiService _userApiService;
    private UserData _userData = null!;
    private readonly ContextData _contextData;
    private LoginResult _loginResult = null!;
    private CheckEquipment _checkEquipment = null!;

    public AuthWindow()
    {
        InitializeComponent();
        _contextData = new ContextData();
        var loginResult = new LoginResult();
        _identityService = new IdentityService(_contextData);
        _userApiService = new UserApiService(_contextData, loginResult);
        _ = LoadUserSettings();
        PassBox.PasswordChar = '•';
        SystemScan();
        ShouldShowEULADialog();
    }

    private async void SystemScan()
    {
        var systemInfo = new SystemInfo();
        var check = new CheckEquipment(systemInfo);
        var info = await check.CheckOperatingSystemAsync();
        Console.WriteLine($"Объем памяти: {info.TotalMemory} МБ");
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

        var secondWindow = new MenuWindow(_contextData, _loginResult);
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

        _userApiService = new UserApiService(_contextData, loginResult);
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


        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirmPassword))
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
            InfoPanel.IsVisible = true;
            InfoBox.Text = result.ErrorMessage;
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
    private async void AcceptButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (AgreementCheckBox.IsChecked == true)
            {
                var userData = await UserData.Load();
                if (userData != null)
                {
                    await userData.SaveEULAAcceptance(true);
                    
                    NavigateToMainWindow();
                }
            }
            else
            {
                
            }
        }

        private async void RefusalButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var userData = await UserData.Load();
            if (userData != null)
            {
                await userData.SaveEULAAcceptance(false);
            }
            CloseApplication();
        }

        public static async Task<bool> ShouldShowEULADialog()
        {
            return !await UserData.CheckEULAAccepted();
        }
        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void AgreementCheckBox_OnChecked(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                bool eulaAccepted = checkBox.IsChecked == true;
                EluaChecker(eulaAccepted);
                UpdateButtonsState(eulaAccepted);
            }
        }

        private void AgreementCheckBox_OnUnchecked(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                bool eulaAccepted = checkBox.IsChecked == true;
                EluaChecker(eulaAccepted);
                UpdateButtonsState(eulaAccepted);
            }
        }

        private void UpdateButtonsState(bool eulaAccepted)
        {
            if (AcceptButton != null)
                AcceptButton.IsEnabled = eulaAccepted;
            
            if (RefusalButton != null)
                RefusalButton.IsEnabled = true;
        }

        private async void EluaChecker(bool eula)
        {
            Console.WriteLine($"EULA checkbox state: {eula}");
            await Task.CompletedTask;
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
