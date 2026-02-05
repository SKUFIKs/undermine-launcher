using System;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Models;
using UnderMine.Services;
using ForgeInstaller = UnderMine.Core.ForgeInstaller;
using MyFabricInstaller = UnderMine.Core.FabricInstaller;

namespace UnderMine.Core
{
    public interface IGameInstallerFactory
    {
 
        Task InstallAndRunAsync(ServerInfo serverConfig, CancellationToken ct = default);
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
        private readonly HytaleLauncherService _hytaleService;

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
        PathService pathService,
        HytaleLauncherService hytaleService)
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
            _hytaleService = hytaleService;
        }

        public async Task InstallAndRunAsync(ServerInfo serverConfig, CancellationToken ct = default)
        {
            string bootloader = serverConfig.BootloaderType?.ToLower() ?? "forge";

            if (bootloader == "hytale")
            {
                // Исправлено: заменено InstallAndLaunchAsync на InstallAndRunAsync
                await _hytaleService.InstallAndRunAsync(serverConfig, ct);
            }
            else
            {
                var mcInstaller = CreateInstaller(serverConfig, serverConfig);
                await mcInstaller.InstallAndRunAsync(ct);
            }
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