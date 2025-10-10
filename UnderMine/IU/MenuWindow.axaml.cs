using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UnderMine.Game.server_1;
using UnderMine.Game.server_2;
using UnderMine.Models;
using UnderMine.Network.Services;
using UnderMine.Services;

namespace UnderMine.IU;

public partial class MenuWindow : Window
{
    private int ServerId {get;set; }
    private InstalGame1Server _instalGame1Server = null!;
    private readonly InstalGame _instalGame2Server = null!;
    private readonly Config _config;
    private readonly LoginResult _loginResult;
    private SliderValues _sliderValues;
    private MemorySliderConfiguration _memoryConfig;

    public MenuWindow(Config config, LoginResult loginResult)
    {
        InitializeComponent();
        DataContext = new Fonts.MenuWindowViewModel();
        _config = config;
        _loginResult = loginResult;
        _ = InitializeMemorySliderAsync();
        InitializeServersAsync();
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
        var apiService = new UserApiService(_config, _loginResult);
        var serverInfo = await apiService.GetServerInfo();

        if (!serverInfo.Success)
        {
            Console.WriteLine("[MENU] Не удалось получить данные сервера: " + serverInfo.ErrorMessage);
            return;
        }

        _instalGame1Server = new InstalGame1Server(serverInfo);
        
    }

    private void NewsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        NewsPanel.IsVisible = true;
        SelectServer.IsVisible = false;
        ProfilePanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        WaitingСhoice.IsVisible = false;
    }

    private void ServerSelectionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectServer.IsVisible = true;
        WaitingСhoice.IsVisible = true;
        ProfilePanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        NewsPanel.IsVisible = false;
    }

    private void ProfileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ProfilePanel.IsVisible = true; 
        SettingsPanel.IsVisible = false;
        NewsPanel.IsVisible = false;
        SelectServer.IsVisible = false;
        WaitingСhoice.IsVisible = false;
    }

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SettingsPanel.IsVisible = true;
        ProfilePanel.IsVisible = false;
        NewsPanel.IsVisible = false;
        SelectServer.IsVisible = false;
        WaitingСhoice.IsVisible = false;
    }

    private void ExitButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        NewsPanel.IsVisible = true;
        SelectServer.IsVisible = false;
        ProfilePanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        WaitingСhoice.IsVisible = false;
    }

    private async void StartingTheGame(int serverId)
    {
        PlayButton.IsEnabled = false;

        try
        {
            if (serverId == 1)
                await _instalGame1Server.InstallAndRunAsync();
            else if (serverId == 2)
                await _instalGame2Server.InstallAndRunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[GAME] Ошибка при запуске игры: " + ex);
        }
        finally
        {
            PlayButton.IsEnabled = true;
        }
    }

    private void SelectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Fonts.MenuWindowViewModel vm)
        {
            if (vm.SelectedServer != null)
            {
                var serverId = vm.SelectedServer.ID;
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
}