using CmlLib.Core.ProcessBuilder;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Models;
using UnderMine.Services;
using Velopack.Sources;

namespace UnderMine.Core
{
    public class HytaleLauncherService
    {
        private readonly FileDownloader _downloader;
        private readonly ProgressManager _progressManager;
        private readonly ProgressReporter _progress;
        private readonly PathService _pathService;
        private readonly FileDownloader _fileDownloader;
        private readonly HttpClient _httpClient;

        private readonly string _gamePath;
        private readonly string _launcherMetaDir;
        private readonly string _jreDir;

        private string OnlineFixUrl => "https://www.dropbox.com/scl/fi/odz6xvs8ocpc944fx8b0x/online.zip?rlkey=guuham6fht342enju55z1t1no&st=hgkx7yyc&dl=1";

        public HytaleLauncherService(
            FileDownloader downloader,
            ProgressManager pm,
            ProgressReporter pr,
            PathService ps)
        {
            _downloader = downloader;
            _progressManager = pm;
            _progress = pr;
            _pathService = ps;
            _httpClient = new HttpClient();
            _fileDownloader = new FileDownloader(_httpClient);
            string baseStorage = _pathService.GetStorageDirectory().Value;
            _gamePath = Path.Combine(baseStorage, "versions", "Hytale");
            _launcherMetaDir = Path.Combine(_gamePath, ".launcher_meta");
            _jreDir = Path.Combine(_gamePath, "runtime", "jre");

            Directory.CreateDirectory(_launcherMetaDir);
            Directory.CreateDirectory(Path.Combine(_launcherMetaDir, "cache"));
        }
        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private async Task DownloadAndInstallHytaleFixAsync(CancellationToken ct)
        {
            // Используем путь игры, определенный в конструкторе (_gamePath)
            string rootPath = _gamePath;
            string dropboxUrl = OnlineFixUrl;
            string tempZip = Path.Combine(_launcherMetaDir, "cache", "hytale_fix.zip");

            _progress.Log($"[Fix] Установка онлайн-фикса...");

            try
            {
                // 1. Загрузка (занимает с 0.7 до 0.9 внутри стадии HytaleGame)
                await _fileDownloader.DownloadFileAsync(dropboxUrl, tempZip, ct, (p) =>
                {
                    double localProgress = 0.7 + (p / 100.0 * 0.2); // Масштаб 70-90%
                    _progressManager.Report("HytaleGame", localProgress, $"Загрузка фикса: {p:F0}%");
                });

                // 2. Распаковка (занимает с 0.9 до 1.0 внутри стадии HytaleGame)
                using (ZipArchive archive = ZipFile.OpenRead(tempZip))
                {
                    float total = archive.Entries.Count;
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var entry = archive.Entries[i];
                        string destPath = Path.GetFullPath(Path.Combine(rootPath, entry.FullName));
                        if (!destPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)) continue;

                        if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(destPath);
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                            entry.ExtractToFile(destPath, overwrite: true);
                        }

                        double localUnpack = 0.9 + ((i + 1) / total * 0.1); // Масштаб 90-100%
                        _progressManager.Report("HytaleGame", localUnpack, "Распаковка фикса...");
                    }
                }

                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
            catch (Exception ex)
            {
                _progress.Log($"[Fix] Ошибка: {ex.Message}", "ERROR");
                throw;
            }
        }


        private void InitProgress()
        {
            _progressManager.RegisterStage("HytaleJRE", 0.20);
            _progressManager.RegisterStage("HytaleGame", 0.60);
            _progressManager.RegisterStage("HytaleLaunch", 0.20);
        }

        public async Task InstallAndRunAsync(ServerInfo info, CancellationToken ct = default, bool headless = false)
        {
            // Загружаем настройки (память и т.д.) по аналогии с MC
            var settings = await UserData.Load() ?? throw new InvalidOperationException("Настройки пользователя не найдены");

            try
            {
                _progress.Log($"=== Подготовка Hytale ===");
                InitProgress();

                // 1. JRE (0% -> 20% общего бара)
                _progressManager.Report("HytaleJRE", 0.0, "Проверка Java...");
                string javaExe = await EnsureJreAsync(info.loader ?? "release", ct);
                _progressManager.Report("HytaleJRE", 1.0, "Java готова");

                // 2. ИГРА И ФИКС (20% -> 80% общего бара)
                _progressManager.Report("HytaleGame", 0.0, "Синхронизация файлов...");

                // Butler займет от 0.0 до 0.7 внутри стадии HytaleGame
                await EnsureGameAsync(info, ct);

                // Если нужен фикс, он продолжит с 0.7 до 1.0 внутри HytaleGame
                if (info.BootloaderType?.ToLower() == "hytale")
                {
                    await DownloadAndInstallHytaleFixAsync(ct);
                }

                // Принудительно закрываем стадию, если фикса не было
                _progressManager.Report("HytaleGame", 1.0, "Файлы игры синхронизированы");

                // 3. ЗАПУСК (80% -> 100% общего бара)
                _progressManager.Report("HytaleLaunch", 0.0, "Запуск...");
                var process = await BuildProcessAsync(javaExe, info, settings);

                if (headless)
                {
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                }

                var processWrapper = new ProcessWrapper(process);
                _progressManager.Report("HytaleLaunch", 1.0, "Игра запущена");
                _progress.Log("Игра запущена. Кнопка 'Играть' заблокирована до закрытия окна.");

                // Запуск и ожидание (блокирует метод, но не UI)
                processWrapper.StartWithEvents();
                await processWrapper.WaitForExitTaskAsync().ConfigureAwait(false);

                _progress.Log("Процесс Hytale завершен. Возврат в лаунчер.");
            }
            catch (Exception ex)
            {
                _progress.Log($"[ОШИБКА HYTALE]: {ex.Message}", "ERROR");
                _progress.Log(ex.StackTrace);
                throw;
            }
        }

        public async Task<Process> BuildProcessAsync(string javaExe, ServerInfo info, UserData settings)
        {
            return await Task.Run(() =>
            {
                string clientExe = Path.Combine(_gamePath, "Client", "HytaleClient.exe");
                if (!File.Exists(clientExe)) clientExe = Path.Combine(_gamePath, "HytaleClient.exe");

                string userDataDir = Path.Combine(_gamePath, "UserData");
                Directory.CreateDirectory(userDataDir);

                string sessionToken = Guid.NewGuid().ToString("N");

                // Формируем аргументы. Можно добавить MemorySettings из settings, если Hytale это поддерживает
                string arguments = $"--app-dir \"{_gamePath}\" " +
                                   $"--java-exec \"{javaExe}\" " +
                                   $"--user-dir \"{userDataDir}\" " +
                                   $"--auth-mode offline " +
                                   $"--uuid {Guid.NewGuid():N} " +
                                   $"--name {info.username} " +
                                   $"--identity-token {sessionToken} " +
                                   $"--session-token {sessionToken}";

                _progress.Log($"[DEBUG] Запуск: {clientExe}");
                _progress.Log($"[DEBUG] Аргументы: {arguments}");

                return new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = clientExe,
                        Arguments = arguments,
                        WorkingDirectory = Path.GetDirectoryName(clientExe),
                        UseShellExecute = false
                    }
                };
            });
        }

        private async Task<string> EnsureJreAsync(string branch, CancellationToken ct)
        {
            var javaFiles = Directory.Exists(_jreDir)
                ? Directory.GetFiles(_jreDir, "java.exe", SearchOption.AllDirectories)
                : Array.Empty<string>();

            if (javaFiles.Length > 0) return javaFiles[0];

            _progress.Log("[JRE] Загрузка манифеста JRE...");
            string cachePath = Path.Combine(_launcherMetaDir, "cache", "jre_meta.json");
            await _downloader.DownloadFileAsync($"https://launcher.hytale.com/version/{branch}/jre.json", cachePath, ct);

            var jreData = JsonConvert.DeserializeObject<JreData>(File.ReadAllText(cachePath));
            var platform = jreData.DownloadUrl["windows"]["amd64"]; // Упрощенно для примера

            string zipPath = Path.Combine(_launcherMetaDir, "cache", "jre.zip");
            await _downloader.DownloadFileAsync(platform.Url, zipPath, ct, p => {
                _progressManager.Report("HytaleJRE", (p / 100.0) * 0.8, $"Загрузка JRE: {p:F0}%");
            });

            _progress.Log("[JRE] Распаковка...");
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, _jreDir, true), ct);

            return Directory.GetFiles(_jreDir, "java.exe", SearchOption.AllDirectories)[0];
        }

        private async Task EnsureGameAsync(ServerInfo info, CancellationToken ct)
        {
            int targetVersion = int.Parse(info.version ?? "1");
            string versionFile = Path.Combine(_gamePath, ".version");
            int installedVersion = 0;

            if (File.Exists(versionFile)) int.TryParse(File.ReadAllText(versionFile), out installedVersion);

            if (installedVersion == targetVersion && IsGameInstalled()) return;

            // Установка Butler
            string butlerPath = await EnsureButlerAsync(ct);

            // Загрузка патча
            string branch = info.loader ?? "release";
            string pwrPath = Path.Combine(_launcherMetaDir, "cache", $"{targetVersion}.pwr");
            string pwrUrl = $"https://game-patches.hytale.com/patches/windows/amd64/{branch}/0/{targetVersion}.pwr";

            await _downloader.DownloadFileAsync(pwrUrl, pwrPath, ct, p => {
                _progressManager.Report("HytaleGame", (p / 100.0) * 0.7, $"Загрузка патча: {p:F0}%");
            });

            // Применение патча
            _progress.Log("[Game] Применение патча Butler...");
            await ApplyPatchAsync(butlerPath, pwrPath);

            File.WriteAllText(versionFile, targetVersion.ToString());
        }

        private async Task ApplyPatchAsync(string butlerPath, string pwrPath)
        {
            string stagingDir = Path.Combine(_launcherMetaDir, "cache", "staging");
            if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
            Directory.CreateDirectory(stagingDir);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = butlerPath,
                    Arguments = $"apply --staging-dir \"{stagingDir}\" \"{pwrPath}\" \"{_gamePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) throw new Exception("Butler не смог применить патч.");
        }

        private async Task<string> EnsureButlerAsync(CancellationToken ct)
        {
            string butlerDir = Path.Combine(_launcherMetaDir, "butler");
            string butlerExe = Path.Combine(butlerDir, "butler.exe");
            if (File.Exists(butlerExe)) return butlerExe;

            string zipPath = Path.Combine(_launcherMetaDir, "cache", "butler.zip");
            await _downloader.DownloadFileAsync("https://broth.itch.zone/butler/windows-amd64/LATEST/archive/default", zipPath, ct);
            ZipFile.ExtractToDirectory(zipPath, butlerDir, true);
            return butlerExe;
        }

        private bool IsGameInstalled()
        {
            return (File.Exists(Path.Combine(_gamePath, "Client", "HytaleClient.exe")) ||
                    File.Exists(Path.Combine(_gamePath, "HytaleClient.exe"))) &&
                   File.Exists(Path.Combine(_gamePath, ".version"));
        }
    }
}