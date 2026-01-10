using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UnderMine.Core;
using UnderMine.IU;
using UnderMine.Models;
using UnderMine.Network;

namespace UnderMine.Fonts.Models;

public partial class MenuWindowViewModel : ObservableObject
{
    private readonly IGameInstallerFactory _installerFactory;
    private readonly UserApiService _apiService;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                PlayCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private Server? _selectedServer;

    public ObservableCollection<Server> Servers { get; } = new();

    public MenuWindowViewModel(IGameInstallerFactory installerFactory, UserApiService apiService)
    {
        _installerFactory = installerFactory;
        _apiService = apiService;

        PlayCommand = new AsyncRelayCommand(ExecutePlay, CanPlay);
        _ = LoadServersAsync(); 
    }

    private async Task LoadServersAsync()
    {
        IsLoading = true; 
        try
        {
            var dynamicServers = await _apiService.GetServersAsync();

            Servers.Clear();
            if (dynamicServers != null)
            {
                foreach (var s in dynamicServers)
                {
                    Servers.Add(s);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при загрузке серверов: {ex.Message}");
        }
        finally
        {
            IsLoading = false; 
        }
    }

    public IAsyncRelayCommand PlayCommand { get; }
    private bool CanPlay() => SelectedServer != null && !IsLoading;

    private async Task ExecutePlay()
    {
        if (SelectedServer == null) return;

        string finalUsername = SelectedServer.username ?? "Player";

        try
        {

            var finalConfig = SelectedServer.Config;


            finalConfig.username = finalUsername;

            Console.WriteLine($"[DEBUG] Запуск сервера: {SelectedServer.Name}");
            Console.WriteLine($"[DEBUG] Ссылка на манифест: {finalConfig.ManifestLink}");

            var installer = _installerFactory.CreateInstaller(finalConfig, finalConfig);

            await installer.InstallAndRunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка запуска: {ex.Message}");
        }
    }
}
public class Server
{
    public string Name { get; set; } = null!;
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string username { get; set; } = null!;
    public ServerInfo Config { get; set; } = new();

    public List<MenuWindow.NewsPart>? Description { get; set; }
}