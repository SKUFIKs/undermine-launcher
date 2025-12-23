using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using System.Diagnostics;
using System.Threading.Tasks;
using UnderMine.Models;

namespace UnderMine.Core.Services
{
    public class MinecraftLauncherService
    {
        private readonly MinecraftLauncher _launcher;

        public MinecraftLauncherService(string baseDirectory)
        {
            _launcher = new MinecraftLauncher(new MinecraftPath(baseDirectory));
        }

        public async Task<Process> BuildProcessAsync(string version, ServerInfo info, int memorySettings)
        {
            var launchOption = new MLaunchOption
            {
                MaximumRamMb = memorySettings,
                Session = MSession.CreateOfflineSession(info.Username)
            };

            return await _launcher.InstallAndBuildProcessAsync(version, launchOption);
        }
    }
}