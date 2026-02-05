using BenchmarkDotNet.Exporters;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ProcessBuilder;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.IU;
using UnderMine.Models;
using UnderMine.Services;
using ForgeInstaller = UnderMine.Core.ForgeInstaller;

namespace UnderMine.Core
{
    public class InstalGame1Server
    {
        private readonly FileDownloader _downloader;
        private readonly MinecraftLauncherService _launcherService;
        private readonly ProgressReporter _progress;
        private readonly ServerInfo _serverInfo;
        private readonly ProgressManager _progressManager;
        private readonly ForgeInstaller _forgeInstaller;
        private readonly FabricInstaller _fabricInstaller; 
        private readonly ProfileService _profileService;
        private readonly ModLoaderService _modLoader;
        private readonly ConfigService _configService;
        private readonly ISettingsService _settingsService;

        private readonly string _gamePath;
        private readonly string _modsPath;
        private readonly string _tempPath;

        public InstalGame1Server(
            ServerInfo serverInfo,
            FileDownloader downloader,
            MinecraftLauncherService launcherService,
            ProgressReporter progress,
            ProfileService profileService,
            ForgeInstaller forgeInstaller,
            FabricInstaller fabricInstaller, 
            ProgressManager progressManager,
            ModLoaderService modLoader,
            ConfigService configService,
            ISettingsService settingsService,
            PathService pathService)
        {
            _progressManager = progressManager;
            _serverInfo = serverInfo;
            _downloader = downloader;
            _launcherService = launcherService;
            _progress = progress;
            _profileService = profileService;
            _forgeInstaller = forgeInstaller;
            _fabricInstaller = fabricInstaller; 
            _modLoader = modLoader;
            _configService = configService;
            _settingsService = settingsService;

            string baseStorage = pathService.GetStorageDirectory().Value;
            _gamePath = Path.Combine(baseStorage, "versions", _serverInfo.server);
            _modsPath = Path.Combine(_gamePath, "mods");
            _tempPath = Path.Combine(_gamePath, "temp");

            InitProgress();
        }

        public void InitProgress()
        {
            string loaderName = _serverInfo.BootloaderType?.ToLower() == "fabric" ? "Fabric" : "Forge";
            _progressManager.RegisterStage("Profile", 0.05);
            _progressManager.RegisterStage("Examination", 0.20);
            _progressManager.RegisterStage(loaderName, 0.25); 
            _progressManager.RegisterStage("Mods", 0.30);
            _progressManager.RegisterStage("Configs", 0.15);
            _progressManager.RegisterStage("Launch", 0.05);
        }

        public async Task InstallAndRunAsync(CancellationToken ct = default, bool headless = false)
        {
            var settings = await UserData.Load() ?? throw new InvalidOperationException("User settings not found");


            string mcVersion = _serverInfo.version ?? throw new InvalidOperationException("Minecraft version not specified");
            string loaderVersion = _serverInfo.loader ?? throw new InvalidOperationException("Loader version not specified");
            string serverName = _serverInfo.server ?? throw new InvalidOperationException("Server name identifier not specified");
            string bootloader = _serverInfo.BootloaderType?.ToLower() ?? "forge";
            

            try
            {
                _progress.Log($"[DEBUG] Подготовка директории: {_gamePath}");
                _progress.Log($"[DEBUG] Манифест для загрузки: {_serverInfo.ManifestLink}");

                InitProgress();

                _progressManager.Report("Profile", 0.0, $"Подготовка {serverName}...");
                _progress.Log($"Начало установки: {serverName} ({bootloader})");


                if (!Directory.Exists(_tempPath)) Directory.CreateDirectory(_tempPath);


                string tempManifest = Path.Combine(_tempPath, "manifest.json");
                if (File.Exists(tempManifest)) File.Delete(tempManifest);

                if (bootloader == "forge")
                {
                    _progressManager.Report("Profile", 0.5, "Создание профиля...");
                    await _profileService.GenerateLauncherProfileAsync(_gamePath, mcVersion, loaderVersion);
                }
                _progressManager.Report("Profile", 1.0, "Профиль готов");
                _progress.Log($"Профиль обнаружен. Продалжаем установку...");

                _progressManager.Report("Examination", 0.0, "Проверка файлов...");
                _progress.Log($"Проверка файлов...");
                bool isLibOk = await CheckingFile.VerifyFolderIntegrityByZipHash(Path.Combine(_gamePath, "libraries"), _serverInfo.LibraryExpectedChecksum, _progress);
                //bool isAssetsOk = await CheckingFile.VerifyFolderIntegrityByZipHash(Path.Combine(_gamePath, "assets"), _serverInfo.AssetsExpectedChecksum, _progress);
                //bool isVersionsOk = await CheckingFile.VerifyFolderIntegrityByZipHash(Path.Combine(_gamePath, "versions"), _serverInfo.VersionExpectedChecksum, _progress);
                _progressManager.Report("Examination", 1.0, "Проверка завершена");

                string stageName = bootloader == "fabric" ? "Fabric" : "Forge";
                if (!isLibOk)
                {
                    _progress.Log($"{stageName} не обнаружен или поврежден. Начинаем установку...");
                    string installerPath = Path.Combine(_tempPath, $"{stageName}-installer.jar");

                    if (!File.Exists(installerPath))
                    {
                        _progressManager.Report(stageName, 0.1, "Загрузка файлов...");
                        _progress.Log($"Загрузка файлов...");
                        await _downloader.DownloadFileAsync(_serverInfo.InstallerLink, installerPath, ct);
                    }

                    bool installResult = false;
                    if (bootloader == "fabric")
                    {
                        _progressManager.Report(stageName, 0.5, "Запуск установки...");
                        _progress.Log($"Запуск установки...");
                        installResult = await _fabricInstaller.InstallAsync(_gamePath, mcVersion, loaderVersion, installerPath, ct, settings.JavaPath);
                    }
                    else
                    {
                        var forgeProgress = new Progress<int>(p => _progressManager.Report("Forge", p / 100.0, "Установка..."));
                        installResult = await _forgeInstaller.InstallAsync(
                            _gamePath,
                            installerPath,
                            _serverInfo.ClientLink,
                            forgeProgress,
                            ct,
                            _progressManager,
                            _serverInfo.LibraryExpectedChecksum,
                            _serverInfo.AssetsExpectedChecksum,
                            _serverInfo.VersionExpectedChecksum, settings.JavaPath);
                    }

                    if (!installResult) throw new Exception($"Критическая ошибка при установке {stageName}.");
                }
                _progressManager.Report(stageName, 1.0, $"Установка завершена.");
                _progress.Log($"Установка завершена.");

                _progressManager.Report("Mods", 0.0, "Проверка модов...");
                _progress.Log($"Проверка модов. Пожайлуста подождите.");

                if (string.IsNullOrEmpty(_serverInfo.ManifestLink))
                    throw new Exception("Ссылка на манифест модов пуста");

                await _modLoader.InstallOrUpdateModsAsync(_modsPath, _serverInfo.ManifestLink, ct);
                _progressManager.Report("Mods", 1.0, "Моды обновлены");
                _progress.Log($"Моды обновлены");

                _progressManager.Report("Configs", 0.1, "Загрузка конфигураций...");
                _progress.Log($"Загрузка конфигурации сервера...");
                await _settingsService.LoadDefaultSettingsAsync(_gamePath);
                await _configService.InstallAllConfigsAsync(_serverInfo, _gamePath);
                _progressManager.Report("Configs", 1.0, "Конфигурация установлена");
                _progress.Log($"Конфигурация установлена.");

                _progressManager.Report("Launch", 0.0, "Запуск игры...");
                _progress.Log($"Запуск игры. Этот процесс может занимать до 5-10 минут.");

                string fullVersionName = bootloader == "fabric"
                    ? $"fabric-loader-{loaderVersion}-{mcVersion}"
                    : $"{mcVersion}-forge-{loaderVersion}";

                var process = await _launcherService.BuildProcessAsync(fullVersionName, _serverInfo, settings.MemorySettings, _gamePath);

                if (headless)
                {
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                }

                var processWrapper = new ProcessWrapper(process);
                _progressManager.Report("Launch", 1.0, "Игра запущена");
                _progress.Log($"Игра запущена.");

                processWrapper.StartWithEvents();
                await processWrapper.WaitForExitTaskAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _progress.Log($"[ОШИБКА УСТАНОВКИ]: {ex.Message}");
                throw; 
            }
        }
    }
}