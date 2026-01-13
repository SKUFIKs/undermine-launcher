using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnderMine.Models;

namespace UnderMine.Core.Services
{
    public class MinecraftLauncherService
    {
        public MinecraftLauncherService(string dummyPath)
        {
        }

        public async Task<Process> BuildProcessAsync(string version, ServerInfo info, int memorySettings, string customPath, string? customJavaPath = null)
        {
            var path = new MinecraftPath(customPath);
            var launcher = new MinecraftLauncher(path);

            if (string.IsNullOrWhiteSpace(info.username))
            {
                throw new Exception("Критическая ошибка: Поле 'username' пустое.");
            }

            var launchOption = new MLaunchOption
            {
                MaximumRamMb = memorySettings,
                Session = MSession.CreateOfflineSession(info.username),

                JavaPath = !string.IsNullOrWhiteSpace(customJavaPath) ? customJavaPath : null
            };

            return await launcher.BuildProcessAsync(version, launchOption);
        }
    }
}