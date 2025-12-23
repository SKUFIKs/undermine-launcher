using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using UnderMine.Models;

namespace UnderMine.Core.Services
{
    public class GameLauncher
    {
        private readonly MinecraftLauncher _launcher;

        public GameLauncher(string baseDirectory)
        {
            _launcher = new MinecraftLauncher(new MinecraftPath(baseDirectory));
        }

        public async Task<System.Diagnostics.Process> CreateProcessAsync(string version, ServerInfo serverInfo, int ramMb)
        {
            var launchOption = new MLaunchOption
            {
                MaximumRamMb = ramMb,
                Session = MSession.CreateOfflineSession(serverInfo.Username)
            };

            return await _launcher.InstallAndBuildProcessAsync(version, launchOption);
        }
    }
}