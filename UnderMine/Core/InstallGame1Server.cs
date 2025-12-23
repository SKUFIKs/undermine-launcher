using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ProcessBuilder;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Core.Interfaces;
using UnderMine.Core.Services;
using UnderMine.Models;
using UnderMine.Services.Location;
using UnderMine.Services.ProgressBar;
using ForgeInstaller = UnderMine.Core.Services.ForgeInstaller;

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
        private readonly ProfileService _profileService;
        private readonly ModLoaderService _modLoader;
        private readonly ConfigService _configService;
        private readonly ISettingsService _settingsService;

        // Эти поля вызывали NullReference, если использовался не тот конструктор
        private readonly string _gamePath;
        private readonly string _modsPath;
        private readonly string _tempPath;

        // ОСТАВЛЯЕМ ОДИН ГЛАВНЫЙ КОНСТРУКТОР
        public InstalGame1Server(
            ServerInfo serverInfo,
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
            _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _launcherService = launcherService ?? throw new ArgumentNullException(nameof(launcherService));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _forgeInstaller = forgeInstaller ?? throw new ArgumentNullException(nameof(forgeInstaller));
            _progressManager = progressManager ?? throw new ArgumentNullException(nameof(progressManager));
            _modLoader = modLoader ?? throw new ArgumentNullException(nameof(modLoader));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            string baseStorage = pathService.GetStorageDirectory().Value;
            _gamePath = Path.Combine(baseStorage, "versions", "TechnoMagic");
            _modsPath = Path.Combine(_gamePath, "mods");
            _tempPath = Path.Combine(_gamePath, "temp");

            InitProgress();
        }

        public void InitProgress()
        {
            _progressManager.RegisterStage("Profile", 0.05); 
            _progressManager.RegisterStage("Examination", 0.15); 
            _progressManager.RegisterStage("Forge", 0.35); 
            _progressManager.RegisterStage("Mods", 0.35);
            _progressManager.RegisterStage("Configs", 0.05); 
            _progressManager.RegisterStage("Launch", 0.05); 
        }

        public async Task InstallAndRunAsync(CancellationToken ct = default,bool headless = false)
        {
            var settings = await UserData.Load() ?? throw new InvalidOperationException("User settings not found");
            string mcVersion = _serverInfo.VersionMc1 ?? throw new InvalidOperationException("Minecraft version not specified");
            string forgeVersion = _serverInfo.ForgeV1 ?? throw new InvalidOperationException("Forge version not specified");

            try
            {
                _progressManager.Report("Profile", 0.0, "Подготовка...");
                _progress.Log($"Подготовка...");

                if (!Directory.Exists(_tempPath)) Directory.CreateDirectory(_tempPath);

                string installerPath = Path.Combine(_tempPath, $"forge-{mcVersion}-{forgeVersion}-installer.jar");
                string forgeVersionDir = Path.Combine(_gamePath, "versions", $"{mcVersion}-forge-{forgeVersion}");

                _progressManager.Report("Profile", 0.5, "Создание профиля...");
                _progress.Log($"Создание профиля...");

                await _profileService.GenerateLauncherProfileAsync(_gamePath, mcVersion, forgeVersion);

                _progressManager.Report("Profile", 1.0, "Успешно");
                _progress.Log($"Успешно. Профиль создан.");

                var VersionExpectedChecksum = _serverInfo.VersionExpectedChecksum;
                var LibraryExpectedChecksum = _serverInfo.LibraryExpectedChecksum;
                var AssetsExpectedChecksum = _serverInfo.LibraryExpectedChecksum;

                _progressManager.Report("Examination", 0.0, "Проверка файлов...");

                _progressManager.Report("Examination", 0.3, "Проверка библиотек...");
                _progress.Log($"Проверка библиотек...");

                string librariesFolder = Path.Combine(_gamePath, "libraries");
                bool isLibOk = await CheckingFile.VerifyFolderIntegrityByZipHash(
                    librariesFolder,
                    LibraryExpectedChecksum
                );

                _progressManager.Report("Examination", 0.5, "Проверка ресурсов...");
                _progress.Log($"Проверка ресурсов...");

                string AssetsFolder = Path.Combine(_gamePath, "assets");
                bool isAssetsOk = await CheckingFile.VerifyFolderIntegrityByZipHash(
                    AssetsFolder,
                    AssetsExpectedChecksum
                );

                _progressManager.Report("Examination", 0.6, "Проверка версий...");
                _progress.Log($"Проверка версий");

                string versionsFolder = Path.Combine(_gamePath, "versions");
                bool isVersionsOk = await CheckingFile.VerifyFolderIntegrityByZipHash(
                    versionsFolder,
                    VersionExpectedChecksum
                );

                _progressManager.Report("Examination", 1.0, "Все файлы проверены.");
                _progress.Log($"Все файлы проверены...");

                if (!isLibOk || !isAssetsOk)
                {
                    _progressManager.Report("Forge", 0.0, "Проверка установшика...");
                    _progress.Log($"Проверка установшика...");

                    if (!File.Exists(installerPath))
                    {
                        _progressManager.Report("Forge", 0.1, "Загрузка установщика...");

                        await _downloader.DownloadFileAsync(_serverInfo.InstallerLink, installerPath, ct);

                        _progressManager.Report("Forge", 0.2, "Установщик загружен");
                        _progress.Log($"Установщик загружен успешно");
                    }
 
                    _progressManager.Report("Forge", 0.3, "Установка игры...");
                    _progress.Log($"Установщик загружен. Начинаем установку...");

                    var forgeProgress = new Progress<int>(percent =>
                    {
                        _progressManager.Report("Forge", percent / 100.0, $"Установка игры...");
                    });

                    bool isForgeOk = await _forgeInstaller.InstallAsync(installerPath, forgeProgress, ct);
                    if (!isForgeOk) throw new Exception("Установка завершилась ошибкой.");

                    _progressManager.Report("Forge", 1.0, "Игра установлена");
                    _progress.Log($"Игра установлена...");
                }

                _progressManager.Report("Mods", 0.0, "Загрузка модов..."); 
                try
                {
                    await _modLoader.InstallOrUpdateModsAsync(_modsPath, _serverInfo.ManifestLink1, ct);
                }
                catch (Exception ex)
                {
                    _progress.Log($"[Критическая ошибка модов]: {ex.Message}");
                    throw;
                }
                _progressManager.Report("Mods", 1.0, "Моды загружены");

                _progress.Log($"Моды установлены");

                _progressManager.Report("Configs", 0.0, "Загрузка конфигов...");
                _progress.Log($"Загрузка конфигов...");
                
                await _configService.InstallAllConfigsAsync(_serverInfo);

                _progressManager.Report("Configs", 0.3, "Конфиги загружены");
                _progress.Log($"Конфиги загружены...");

                _progressManager.Report("Configs", 0.5, "Настройка...");
                _progress.Log($"Настройка...");

                await _settingsService.LoadDefaultSettingsAsync(_gamePath);

                _progressManager.Report("Configs", 1.0, "Подготовка к запуску");
                _progress.Log($"Подготовка к запуску...");


                _progressManager.Report("Launch", 0.7, "Запуск игры...");
                _progress.Log($"Запуск игры...");

                string fullVersionName = $"{mcVersion}-forge-{forgeVersion}";
                var process = await _launcherService.BuildProcessAsync(fullVersionName, _serverInfo, settings.MemorySettings);

                if (File.Exists(installerPath)) try { File.Delete(installerPath); } catch { }

                if (headless)
                {
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                }

                var processWrapper = new ProcessWrapper(process);
                _progressManager.Report("Launch", 1.0, "Игра запущена");
                _progress.Log($"Игра запущена...");
                processWrapper.StartWithEvents();
                await processWrapper.WaitForExitTaskAsync().ConfigureAwait(false);
                _progress.Log($"");
            }
            catch (OperationCanceledException)
            {
                _progress.Log("Установка отменена.");
            }
            catch (Exception ex)
            {
                _progress.Log($"Ошибка: {ex.Message}");
                throw;
            }
        }
    }
}