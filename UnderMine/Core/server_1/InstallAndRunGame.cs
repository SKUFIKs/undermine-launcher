using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Newtonsoft.Json.Linq;
using UnderMine.Models;
using UnderMine.Services;
using System.Runtime.InteropServices;
using UnderMine.Services.Logger;

namespace UnderMine.Core.server_1
{
    public class InstalGame1Server : IDisposable
    {
        private readonly ServerInfo _serverInfo;
        private readonly MinecraftLauncher _launcher;
        private readonly string _baseDirectory;
        private readonly string _modsDirectory;
        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;

        private readonly SmoothProgressUpdater _smoothUpdater;

        public event Action<string>? OnMessage;
        public event Action<string, double, string>? OnProgress;
        public event Action<string, Exception>? OnError;


        public InstalGame1Server(ServerInfo serverInfo, IHttpClientFactory? httpClientFactory = null, HttpClient? httpClient = null)
        {
            _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));

            _baseDirectory = Path.Combine(GetBaseDirectory(), "versions", "TechnoMagic");
            _modsDirectory = Path.Combine(_baseDirectory, "mods");

            Directory.CreateDirectory(_baseDirectory);
            Directory.CreateDirectory(_modsDirectory);

            _launcher = new MinecraftLauncher(new MinecraftPath(_baseDirectory));

            if (httpClientFactory != null)
            {
                _httpClient = httpClientFactory.CreateClient();
                _disposeHttpClient = false;
            }
            else if (httpClient is not null)
            {
                _httpClient = httpClient;
                _disposeHttpClient = false;
            }
            else
            {
                _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                _disposeHttpClient = true;
            }
            _smoothUpdater = new SmoothProgressUpdater(
                (message, progress, tag) => OnProgress?.Invoke(message, progress, tag)
            );
        }
        private void ReportError(string userMessage, Exception ex)
        {
            Console.WriteLine($"[ERROR] {userMessage}: {ex}"); 
            OnError?.Invoke(userMessage, ex); 
        }
        private static string GetBaseDirectory()
        {
            const string AppName = "UnderMine";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
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

        private async Task ReportStage(string message, double progress)
        {
            Console.WriteLine($"[Stage] {message} ({progress}%)");
            OnMessage?.Invoke(message);

            await _smoothUpdater.UpdateSmoothly(message, progress, "");
        }

        private void Log(string message)
        {
            Console.WriteLine($"[INFO] {message}");
            OnMessage?.Invoke(message);
        }

        public async Task InstallAndRunAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var settings = await UserData.Load() ?? throw new InvalidOperationException("User settings not found");
                string mcVersion = _serverInfo.VersionMc1 ?? throw new InvalidOperationException("Minecraft version not specified");
                string forgeVersion = _serverInfo.ForgeV1 ?? throw new InvalidOperationException("Forge version not specified");
                string manifestUrl = _serverInfo.ManifestLink1 ?? throw new InvalidOperationException("Manifest URL not specified");
                string installerLink = _serverInfo.InstallerLink ?? throw new InvalidOperationException("Installer link not specified");

                await ReportStage("Подготовка...", 5);

                await ReportStage("Установка игры...", 25);
                await LoadAndInstallForge(mcVersion, forgeVersion, installerLink, cancellationToken).ConfigureAwait(false);

                await ReportStage("Установка модов...", 50);
                await InstallMods(manifestUrl, cancellationToken).ConfigureAwait(false);

                await ReportStage("Установка модов завершена", 80);

                await ReportStage("Загрузка конфигов...", 90);
                await LoadConfigsAndSettings(cancellationToken).ConfigureAwait(false);

                await ReportStage("Конфигурация завершена...", 95);

                await ReportStage("Запуск игры...", 100);
                await LaunchGame(mcVersion, forgeVersion, settings.MemorySettings, cancellationToken).ConfigureAwait(false);

                await ReportStage("Установка завершена", 100);
            }
            catch (OperationCanceledException)
            {
                ReportError("Установка была отменена пользователем.", new Exception("Отмена"));
                throw; 
            }
            catch (Exception ex)
            {
                ReportError($"{ex.Message}", ex);
                throw; 
            }
        }

        private async Task LoadAndInstallForge(string mcVersion, string forgeVersion, string installerLink, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Log($"Установка Forge {forgeVersion} для {mcVersion}...");
                string forgeDirName = $"{mcVersion}-forge-{forgeVersion}";
                string forgeDir = Path.Combine(_baseDirectory, forgeDirName);
                string tempDir = Path.Combine(_baseDirectory, "temp");
                Directory.CreateDirectory(tempDir);

                string installerName = $"forge-{mcVersion}-{forgeVersion}-installer.jar";
                string installerPath = Path.Combine(tempDir, installerName);

                if (!File.Exists(installerPath))
                {
                    Log("Загрузка установщика...");
                    try
                    {
                        await DownloadFileAsync(_httpClient, installerLink, installerPath, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ReportError($"Не удалось загрузить установщик", ex);
                        throw; 
                    }
                }

                var profile = new ProfileGeneration();
                await profile.ProfileGenerate();

                if (!Directory.Exists(forgeDir))
                {
                    Log("Проверка файлов...");
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = $"-jar \"{installerPath}\" --installClient",
                        WorkingDirectory = _baseDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };

                    process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine($"[Forge] {e.Data}"); };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            Console.WriteLine($"[Error] {e.Data}");
                        }
                    }; 

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    var waitTask = process.WaitForExitAsync(cancellationToken);

                    var completedTask = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);
                    if (completedTask == timeoutTask)
                    {
                        try { process.Kill(true); } catch { }

                        var timeoutEx = new TimeoutException("Установка Forge заняла слишком много времени и была прервана.");
                        ReportError(timeoutEx.Message, timeoutEx);
                        throw timeoutEx;
                    }

                    if (process.ExitCode != 0)
                    {
                        var errorMsg = $"Установка завершилась с ошибкой (код {process.ExitCode})";
                        var exitEx = new Exception(errorMsg);
                        ReportError(errorMsg, exitEx);
                        throw exitEx;
                    }
                }

                Log("Игра установлена");
            }
            catch (OperationCanceledException)
            {
                Log("Установка Forge отменена.");
                throw;
            }
            catch (Exception ex)
            {
                
                ReportError($"[Error]: {ex.Message}", ex);
                throw; 
            }
        }

        private async Task InstallMods(string manifestUrl, CancellationToken cancellationToken)
        {
            using var modInstaller = new ModLoader();
            modInstaller.OnMessage += msg => OnMessage?.Invoke(msg);
            modInstaller.OnProgress += (_, percent, _) =>
            {
                double globalProgress = 50 + (percent * 0.30); 

                
                OnProgress?.Invoke("Установка модов", globalProgress, "");
            };

            await modInstaller.InstallOrUpdateMods(_modsDirectory, manifestUrl).ConfigureAwait(false);

           
            await ReportStage("Установка модов завершена", 80);
        }

        private async Task LoadConfigsAndSettings(CancellationToken cancellationToken)
        {
            var configLoader = new ConfigLoader(_serverInfo);
            await configLoader.ConfLoader();
            await configLoader.ShaderpacksLoader();
            await configLoader.ScriptsLoader();

            var settingsLoader = new SettingsLoader();
            await settingsLoader.LoadSettings();
        }

        private async Task LaunchGame(string mcVersion, string forgeVersion, int memorySettings, CancellationToken cancellationToken, bool headless = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string forgeVersionName = $"{mcVersion}-forge-{forgeVersion}";
            Console.WriteLine($"[Forge] Запуск игры. Этот процесс может длиться до 5-10 минут.");

            var launchOption = new MLaunchOption
            {
                MaximumRamMb = memorySettings,
                Session = MSession.CreateOfflineSession(_serverInfo.Username)
            };

            var forgeProcess = await _launcher.InstallAndBuildProcessAsync(forgeVersionName, launchOption).ConfigureAwait(false);

            if (headless)
            {
                forgeProcess.StartInfo.CreateNoWindow = true;
                forgeProcess.StartInfo.RedirectStandardOutput = true;
            }

            var processWrapper = new ProcessWrapper(forgeProcess);
            processWrapper.StartWithEvents();
            await processWrapper.WaitForExitTaskAsync().ConfigureAwait(false);
        }

        private async Task DownloadFileAsync(HttpClient client, string url, string path, CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

            await stream.CopyToAsync(file, 81920, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_disposeHttpClient)
                {
                    try { _httpClient.Dispose(); } catch { }
                }
            }
        }
    }
}
