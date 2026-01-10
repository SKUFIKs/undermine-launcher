using System;
using UnderMine.Core.Interfaces;
using UnderMine.Core.Services;
using UnderMine.Models;
using UnderMine.Services.Location;
using UnderMine.Services.Logger;
using UnderMine.Services.ProgressBar;
using ForgeInstaller = UnderMine.Core.Services.ForgeInstaller;
using MyFabricInstaller = UnderMine.Core.Services.FabricInstaller;

namespace UnderMine.Core
{
    public interface IGameInstallerFactory
    {
        InstalGame1Server CreateInstaller(ServerInfo serverInfo, ServerInfo playerInfo);
    }

    public class GameInstallerFactory : IGameInstallerFactory
    {
        private readonly FileDownloader _downloader;
        private readonly MinecraftLauncherService _launcherService;
        private readonly ProgressReporter _progress;
        private readonly ProfileService _profileService;
        private readonly ForgeInstaller _forgeInstaller;
        private readonly ProgressManager _progressManager;
        private readonly ModLoaderService _modLoader;
        private readonly ConfigService _configService;
        private readonly ISettingsService _settingsService;
        private readonly PathService _pathService;
        private readonly MyFabricInstaller _fabricInstaller;

        public GameInstallerFactory(
        FileDownloader downloader,
        MinecraftLauncherService launcherService,
        ProgressReporter progress,
        ProfileService profileService,
        ForgeInstaller forgeInstaller,
        ProgressManager progressManager,
        ModLoaderService modLoader,
        ConfigService configService,
        ISettingsService settingsService,
        PathService pathService)
        {
            _downloader = downloader;
            _launcherService = launcherService;
            _progress = progress;
            _profileService = profileService;
            _forgeInstaller = forgeInstaller;
            _fabricInstaller = new MyFabricInstaller();
            _progressManager = progressManager;
            _modLoader = modLoader;
            _configService = configService;
            _settingsService = settingsService;
            _pathService = pathService;
        }

        public InstalGame1Server CreateInstaller(ServerInfo serverConfig, ServerInfo playerInfo)
        {
            var finalSettings = new ServerInfo
            {
                server = serverConfig.server,
                version = serverConfig.version,
                loader = serverConfig.loader,
                ManifestLink = serverConfig.ManifestLink,
                InstallerLink = serverConfig.InstallerLink,
                ClientLink = serverConfig.ClientLink,
                BootloaderType = serverConfig.BootloaderType,
                username = playerInfo.username,
                LibraryExpectedChecksum = serverConfig.LibraryExpectedChecksum,
                VersionExpectedChecksum = serverConfig.VersionExpectedChecksum,
                AssetsExpectedChecksum = serverConfig.AssetsExpectedChecksum,
                ConfLink = serverConfig.ConfLink,
                ShadersLink = serverConfig.ShadersLink,
                KubejsLink = serverConfig.KubejsLink
            };

            return new InstalGame1Server(
                finalSettings,
                _downloader,
                _launcherService,
                _progress,
                _profileService,
                _forgeInstaller,
                _fabricInstaller,
                _progressManager,
                _modLoader, 
                _configService,
                _settingsService,
                _pathService
            );
        }
    }
}