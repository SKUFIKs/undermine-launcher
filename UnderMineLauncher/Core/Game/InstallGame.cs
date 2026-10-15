using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnderMine.Core.Network;
using UnderMine.Models;
using UnderMine.Services;

namespace UnderMine.Core.Game
{
    /// <summary>User-configurable launch options coming from the launcher settings.</summary>
    public class LauncherOptions
    {
        public int MaxMemoryMb { get; set; } = 4096;
        public string? JavaPath { get; set; }
        public string? InstallationPath { get; set; }
        public string LauncherVersion { get; set; } = "1.0";
    }

    /// <summary>
    /// Installs and launches Minecraft using manifest-based synchronization:
    /// fetch manifest -> sync files -> extract natives -> resolve Java -> launch.
    /// </summary>
    public class InstallGame
    {
        private readonly UserClient _userClient;
        private readonly ClientSyncService _syncService;
        private readonly ProcessLauncher _processLauncher;
        private readonly JavaRuntimeService _javaRuntime;
        private readonly ProgressService _reporter;
        private readonly SystemService _systemService;
        private readonly ILogger<InstallGame> _logger;

        public InstallGame(
            UserClient userClient,
            ClientSyncService syncService,
            ProcessLauncher processLauncher,
            JavaRuntimeService javaRuntime,
            ProgressService reporter,
            SystemService systemService,
            ILogger<InstallGame> logger)
        {
            _userClient = userClient;
            _syncService = syncService;
            _processLauncher = processLauncher;
            _javaRuntime = javaRuntime;
            _reporter = reporter;
            _systemService = systemService;
            _logger = logger;
        }

        public async Task<bool> InstallAndLaunchGame(
            ContextData contextData,
            LoginResult loginResult,
            int serverId,
            UserDto userInfo,
            LauncherOptions options,
            CancellationToken ct)
        {
            try
            {
                RegisterStages();

                _logger.LogInformation("Starting install and launch for server {ServerId}", serverId);
                await _reporter.ReportAsync("manifest", 0.1, "Fetching server config...");

                // 1. Server config (gives the display name used for the instance folder).
                var serverInfo = await _userClient.GetServerConfig(loginResult, ct, serverId)
                    ?? throw new InvalidOperationException("Server config not received.");
                var serverName = SanitizeFolderName(serverInfo.ServerName);

                // 2. Build manifest.
                await _reporter.ReportAsync("manifest", 0.6, "Fetching build manifest...");
                var manifest = await _userClient.GetBuildManifest(loginResult, ct, serverId)
                    ?? throw new InvalidOperationException("Build manifest not received.");
                await _reporter.ReportAsync("manifest", 1, $"Manifest: {manifest.DisplayName} ({manifest.Files.Count} files)");

                // 3. Resolve the instance directory (spec: <root>/instances/<ServerName>).
                var root = string.IsNullOrWhiteSpace(options.InstallationPath)
                    ? _systemService.GetStorageDirectory().Value
                    : options.InstallationPath!;
                var instancePath = Path.Combine(root, "instances", serverName);
                Directory.CreateDirectory(instancePath);
                _logger.LogInformation("Instance path: {InstancePath}", instancePath);
                _reporter.Log($"Instance path: {instancePath}");

                // 4. Synchronize files.
                var syncResult = await _syncService.SyncAsync(manifest, instancePath, ct);
                if (!syncResult.Success)
                    throw new InvalidOperationException("Failed to synchronize game files.");

                // 5. Extract native libraries (LWJGL etc.).
                await _reporter.ReportAsync("natives", 0.2, "Extracting native libraries...");
                var libraryPaths = manifest.Files
                    .Where(f => f.Path.StartsWith("libraries/", StringComparison.OrdinalIgnoreCase))
                    .Select(f => f.Path);
                var natives = NativesExtractor.Extract(instancePath, libraryPaths, _logger);
                await _reporter.ReportAsync("natives", 1, $"Extracted {natives} native file(s).");

                // 6. Resolve (or download) a suitable Java runtime.
                await _reporter.ReportAsync("java", 0.05, "Locating Java runtime...");
                var javaPath = await _javaRuntime.ResolveJavaAsync(options.JavaPath, root, manifest.JavaVersion, ct);
                if (string.IsNullOrEmpty(javaPath))
                    throw new InvalidOperationException($"Java {manifest.JavaVersion}+ could not be found or installed.");
                await _reporter.ReportAsync("java", 1, $"Java ready: {javaPath}");

                // 7. First start of this instance: Minecraft has no --fullscreen flag, it reads the
                //    setting from options.txt. The shipped build sets fullscreen:false, so flip it
                //    once here; every later launch keeps whatever the player chose in game (F11).
                var firstLaunch = !FirstLaunchSettings.HasLaunchedBefore(instancePath);
                if (firstLaunch && !FirstLaunchSettings.ForceFullscreen(instancePath))
                    _logger.LogWarning("Could not enable fullscreen in {InstancePath}", instancePath);

                // 8. Build and launch the process.
                await _reporter.ReportAsync("launch", 0.5, "Launching Minecraft...");
                var startInfo = _processLauncher.BuildProcess(
                    manifest,
                    instancePath,
                    javaPath,
                    userInfo.Username,
                    userInfo.Uuid.ToString(),
                    loginResult.Token,
                    options.MaxMemoryMb,
                    NativesExtractor.GetNativesDirectory(instancePath),
                    "UnderMineLauncher",
                    options.LauncherVersion);

                var process = _processLauncher.Launch(startInfo);
                process.Exited += (_, _) => _logger.LogInformation("Game exited with code {Code}", TryGetExitCode(process));

                if (firstLaunch)
                    FirstLaunchSettings.MarkLaunched(instancePath);

                CacheManifest(instancePath, manifest);

                await _reporter.ReportAsync("launch", 1, "Game launched.");
                _reporter.Log("Game launched successfully!", "SUCCESS");
                return true;
            }
            catch (OperationCanceledException)
            {
                _reporter.Log("Installation cancelled.", "WARN");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Install and launch failed for server {ServerId}", serverId);
                _reporter.LogException(ex, "InstallAndLaunchGame");
                return false;
            }
        }

        private void RegisterStages()
        {
            _reporter.RegisterStage("manifest", 1);
            _reporter.RegisterStage("sync", 10);
            _reporter.RegisterStage("natives", 1);
            _reporter.RegisterStage("java", 2);
            _reporter.RegisterStage("launch", 1);
            _reporter.Reset();
        }

        private static int? TryGetExitCode(Process process)
        {
            try { return process.ExitCode; }
            catch { return null; }
        }

        private static string SanitizeFolderName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "server";

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.IsNullOrEmpty(cleaned) ? "server" : cleaned;
        }

        private static void CacheManifest(string instancePath, BuildManifest manifest)
        {
            try
            {
                var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(instancePath, "manifest.json"), json);
            }
            catch
            {
                // Caching is best-effort.
            }
        }
    }
}
