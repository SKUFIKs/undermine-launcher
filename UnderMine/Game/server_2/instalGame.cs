using System;
using System.IO;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ProcessBuilder;
using UnderMine.Models;

namespace UnderMine.Game.server_2
{
    public class InstalGame
    {
        private readonly ServerInfo _serverInfo;
        private readonly MinecraftLauncher _launcher;
        private readonly string _modsDirectory;
        
        public InstalGame(ServerInfo serverInfo)
        {
            _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
            string launcherDirectory = AppDomain.CurrentDomain.BaseDirectory;
            
            string gameDirectory = Path.Combine(launcherDirectory, "Games", "server2");
            _modsDirectory = Path.Combine(launcherDirectory, "mods2");
            
            Directory.CreateDirectory(gameDirectory);
            Directory.CreateDirectory(_modsDirectory);

            var path = new MinecraftPath(gameDirectory);
            _launcher = new MinecraftLauncher(path);
        }
        
        public async Task InstallAndRunAsync()
        {
            UserData settings = await UserData.Load() ?? throw new InvalidOperationException();
            int memorySettings = settings.MemorySettings;
            var mcVersion = _serverInfo.VersionMc2;
            var forgeVersion = _serverInfo.ForgeV2;
            var manifestUrl = _serverInfo.ManifestLink2;
            Console.WriteLine(mcVersion);
            Console.WriteLine(forgeVersion);

            if (string.IsNullOrEmpty(mcVersion) || string.IsNullOrEmpty(forgeVersion))
                throw new InvalidOperationException("Minecraft version or Forge version is not specified");
            

            var forge = new ForgeInstaller(_launcher);

            var fileProgress = new SyncProgress<InstallerProgressChangedEventArgs>(e => Console.WriteLine($"[{e.EventType}] {e.ProgressedTasks}/{e.TotalTasks} - {e.Name}"));
            var byteProgress = new SyncProgress<ByteProgress>(e => Console.WriteLine($"{e.ToRatio() * 100:F2}%"));
            var installerOutput = new SyncProgress<string>(s => Console.WriteLine($"{s}"));

            var modInstallerServer2 = new ModInstaller();
            
            var versionName = await forge.Install(mcVersion, forgeVersion, new ForgeInstallOptions
            {
                FileProgress = fileProgress,
                ByteProgress = byteProgress,
                InstallerOutput = installerOutput,
            });
            
            await modInstallerServer2.InstallOrUpdateMods(_modsDirectory, manifestUrl);
            
            var launchOption = new MLaunchOption
            {
                MaximumRamMb = memorySettings,
                Session = MSession.CreateOfflineSession(_serverInfo.Username),
            };
            
            var process = await _launcher.InstallAndBuildProcessAsync(versionName, launchOption);
            
            var processUtil = new ProcessWrapper(process);
            processUtil.OutputReceived += (_, e) => Console.WriteLine("[GAME-OUTPUT] " + e);
            processUtil.StartWithEvents();
            await processUtil.WaitForExitTaskAsync();
        }
    }
}
