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
using UnderMine.Core.server_1;
using UnderMine.Fonts.Models;
using UnderMine.Game.server_2;
using UnderMine.Models;
using UnderMine.Network.Services;
using UnderMine.Services;
using UnderMine.Services.Logger;
using UnderMine.Services.SystemInfo.MemorySettings;


namespace UnderMine.IU;

public partial class MenuWindow : Window
{
    private int ServerId {get;set; }
    private InstalGame1Server _instalGame1Server = null!;
    private readonly InstalGame _instalGame2Server = null!;
    private readonly ContextData _contextData;
    private readonly LoginResult _loginResult;
    private SliderValues _sliderValues = null!;
    private MemorySliderConfiguration _memoryConfig = null!;
    private UserData _userData = null!;
    private ServerInfo _serverInfo = null!;
    private InstalGame1Server _installer;
    private string _logContent = "";
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
        if (text.Length > 120)
            text = text.Substring(0, 120) + "...";
        
        LogLabel.Content = text;
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

            _instalGame1Server = new InstalGame1Server(_serverInfo);

            _instalGame1Server.OnError += HandleException;

            _instalGame1Server.OnProgress += (task, percent, state) =>
            {
                Dispatcher.UIThread.Post(async () => 
                {
                    Loader.Progress = percent;

                    AddLogMessage($"{task} {state}");

                    if (percent == 25 || percent == 50 || percent == 90)
                    {
                        await Task.Delay(100); 
                    }
                });
            };

            _instalGame1Server.OnMessage += AddLogMessage;

            UsernameLabel.Content = _userData.Username;
            
            _installer = _instalGame1Server;
        }
        catch (Exception ex)
        {
            HandleException("Ошибка загрузки данных сервера", ex);
            return;
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
                    throw new InvalidOperationException("Сервер не был инициализирован.");
                }
                await _instalGame1Server.InstallAndRunAsync();
            }
            else if (serverId == 2)
                await _instalGame2Server.InstallAndRunAsync();
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
        }
    }

    private void SelectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MenuWindowViewModel vm)
        {
            if (vm.SelectedServer != null)
            {
                var serverId = vm.SelectedServer.Id;
                if (serverId == 1)
                {
                    SelectServer.IsVisible = false;
                    WaitingСhoice.IsVisible = false;
                    PlayServer.IsVisible = true;
                    ServerId = serverId;
                }
                else if  (serverId == 2)
                {
                    SelectServer.IsVisible = false;
                    WaitingСhoice.IsVisible = false;
                    PlayServer.IsVisible = true;
                    ServerId = serverId;
                    Console.WriteLine(serverId);
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
        
        MemorySlider.Value=_userData.MemorySettings;
        
        await _userData.Save();
        
        MemorySlider.ValueChanged += (s, e) =>
        {
            _userData.MemorySettings = (int)e.NewValue;
        };
    }

    private void OverviewSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        string directoryPath = GetBaseDirectory();

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
    private void LogsSettings_OnClick (object? sender, RoutedEventArgs e)
    {

    }

    private void HandleException(string userMessage, Exception ex)
    {
        Console.WriteLine($"[Error] {userMessage}: {ex}");

        Dispatcher.UIThread.Post(() =>
        {
            ErrorPanel.IsVisible = true;
            ErrorBox.Text = $"{userMessage}\n{ex.StackTrace}";
        });
    }

    private static string GetBaseDirectory()
    {
        const string AppName = "UnderMine";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return AppContext.BaseDirectory;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string dir = Path.Combine(home, "Library", "Application Support", AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }
        else
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dir = Path.Combine(home, ".config", AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

}