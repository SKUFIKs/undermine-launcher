using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using UnderMine.Models;
using UnderMine.Services;
using Newtonsoft.Json.Linq;
using UnderMine.IU;

namespace UnderMine.Game.server_1
{
    public class InstalGame1Server
    {
        private readonly ServerInfo _serverInfo;
        private readonly MinecraftLauncher _launcher;
        private readonly string _baseDirectory;
        private readonly string _modsDirectory;
        private bool _messageShown = false;

        public event Action<string>? OnMessage;
        public event Action<string, double, string>? OnProgress;

        public InstalGame1Server(ServerInfo serverInfo)
        {
            _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
            _baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "versions", "TechnoMagic");
            _modsDirectory = Path.Combine(_baseDirectory, "mods");

            Directory.CreateDirectory(_baseDirectory);
            Directory.CreateDirectory(_modsDirectory);

            _launcher = new MinecraftLauncher(new MinecraftPath(_baseDirectory));
        }

        private void ReportStage(string message, double progress)
        {
            Console.WriteLine($"[Stage] {message} ({progress}%)");
            OnMessage?.Invoke(message);
            OnProgress?.Invoke(message, progress, "");
        }

        private void Log(string message)
        {
            Console.WriteLine($"[INFO] {message}");
            OnMessage?.Invoke(message);
        }

        public async Task InstallAndRunAsync()
        {
            var settings = await UserData.Load() ?? throw new InvalidOperationException("User settings not found");
            string mcVersion = _serverInfo.VersionMc1 ??
                               throw new InvalidOperationException("Minecraft version not specified");
            string forgeVersion = _serverInfo.ForgeV1 ??
                                  throw new InvalidOperationException("Forge version not specified");
            string manifestUrl = _serverInfo.ManifestLink1 ??
                                 throw new InvalidOperationException("Manifest URL not specified");
            string installerLink = _serverInfo.InstallerLink ??
                                   throw new InvalidOperationException("Installer link not specified");

            ReportStage("Подготовка установки...", 0);
            await InstallVanillaMinecraft(mcVersion);

            ReportStage("Установка игры...", 25);
            await LoadAndInstallForge(mcVersion, forgeVersion, installerLink);

            ReportStage("Установка модов...", 50);
            //var warmupTask = LaunchGame(mcVersion, forgeVersion, settings.MemorySettings, headless: true);
            var modsTask = InstallMods(manifestUrl);

            await Task.WhenAll(modsTask);

            ReportStage("Загрузка конфигов...", 90);
            await LoadConfigsAndSettings();

            ReportStage("Запуск игры...", 100);
            await LaunchGame(mcVersion, forgeVersion, settings.MemorySettings);

            ReportStage("Установка завершена", 100);
            Console.WriteLine($"");
        }

        private async Task InstallVanillaMinecraft(string mcVersion)
        {
            string versionDir = Path.Combine(_baseDirectory, mcVersion);
            Directory.CreateDirectory(versionDir);

            string clientJar = Path.Combine(versionDir, mcVersion + ".jar");
            string clientJson = Path.Combine(versionDir, mcVersion + ".json");

            using var httpClient = new HttpClient();
            string manifestJson =
                await httpClient.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
            var manifest = JObject.Parse(manifestJson);
            var versionObj = manifest["versions"].First(v => v["id"]!.ToString() == mcVersion);
            string versionUrl = versionObj["url"]!.ToString();

            string versionJson = await httpClient.GetStringAsync(versionUrl);
            File.WriteAllText(clientJson, versionJson);

            var versionJ = JObject.Parse(versionJson);
            string jarUrl = versionJ["downloads"]["client"]["url"]!.ToString();

            if (!File.Exists(clientJar))
                await DownloadFileAsync(httpClient, jarUrl, clientJar);

            OnMessage?.Invoke("Vanilla Minecraft установлен");
        }

        private async Task LoadAndInstallForge(string mcVersion, string forgeVersion, string installerLink)
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
                using var client = new HttpClient();
                Log("Загрузка установщика...");
                await DownloadFileAsync(client, installerLink, installerPath);
            }

            var profile = new ProfileGeneration();
            await profile.ProfileGenerate();

            if (!Directory.Exists(forgeDir))
            {
                Log("Запуск установщика...");
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

                var process = new System.Diagnostics.Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Console.WriteLine($"[Forge] {e.Data}");
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Console.WriteLine($"[Forge:Error] {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
                var waitTask = process.WaitForExitAsync();

                var completedTask = await Task.WhenAny(waitTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch
                    {
                    }

                    throw new TimeoutException("Установка Forge заняла слишком много времени и была прервана.");
                }
                
                if (process.ExitCode != 0)
                    throw new Exception($"Forge installer завершился с ошибкой (код {process.ExitCode})");
            }

            Log("Игра установлена");
        }
        
        private async Task InstallMods(string manifestUrl)
        {
            var modInstaller = new ModLoader();
            modInstaller.OnMessage += msg => OnMessage?.Invoke(msg);
            
            modInstaller.OnProgress += (_, percent, _) =>
            {
                double globalProgress = 50 + (percent * 0.25);
                OnProgress?.Invoke("Установка модов", globalProgress, "");
            };

            await modInstaller.InstallOrUpdateMods(_modsDirectory, manifestUrl);

            OnProgress?.Invoke("Установка модов завершена", 75, "");
        }


        private async Task LoadConfigsAndSettings()
        {
            var configLoader = new ConfigLoader(_serverInfo);
            await configLoader.GuiLoader();
            await configLoader.QuestLoader();
            await configLoader.ShaderpacksLoader();

            var settingsLoader = new SettingsLoader();
            await settingsLoader.LoadSettings();
        }

        private async Task LaunchGame(string mcVersion, string forgeVersion, int memorySettings, bool headless = false)
        {
            string forgeVersionName = $"{mcVersion}-forge-{forgeVersion}";
            Console.WriteLine($"[Forge] Запуск игры. Этот процесс может длиться до 5-10 минут.");

            var launchOption = new MLaunchOption
            {
                MaximumRamMb = memorySettings,
                Session = MSession.CreateOfflineSession(_serverInfo.Username)
            };

            var forgeProcess = await _launcher.InstallAndBuildProcessAsync(forgeVersionName, launchOption);

            if (headless)
            {
                forgeProcess.StartInfo.CreateNoWindow = true;
                forgeProcess.StartInfo.RedirectStandardOutput = true;
            }

            var processWrapper = new ProcessWrapper(forgeProcess);
            processWrapper.StartWithEvents();

            await processWrapper.WaitForExitTaskAsync();
        }


        private async Task DownloadFileAsync(HttpClient client, string url, string path)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            await stream.CopyToAsync(file);
        }
    }
}