using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UnderMine.Core.Services
{
    public class ForgeInstaller
    {
        private readonly string _baseDirectory;

        public ForgeInstaller(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
        }

        public async Task<bool> InstallAsync(string installerPath, IProgress<int> progress, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-jar \"{installerPath}\" --installClient",
                WorkingDirectory = _baseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;

                if (e.Data.Contains("Downloading libraries") || e.Data.Contains("Considering library"))
                    progress?.Report(10);
                if (e.Data.Contains("Extracting: /data/client.lzma"))
                    progress?.Report(30);
                if (e.Data.Contains("Building Processors"))
                    progress?.Report(40);
                if (e.Data.Contains("Splitting:"))
                    progress?.Report(60);
                if (e.Data.Contains("Reading patch"))
                    progress?.Report(80); 
                if (e.Data.Contains("Patching"))
                    progress?.Report(90);
                if (e.Data.Contains("Successfully installed"))
                    progress?.Report(100);
                Console.WriteLine($"[Forge] {e.Data}");
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Console.WriteLine($"[Forge Error] {e.Data}");
            };

            if (!process.Start()) return false;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(10), ct); 
            var waitTask = process.WaitForExitAsync(ct);

            var completedTask = await Task.WhenAny(waitTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                try { process.Kill(true); } catch { }
                Console.WriteLine("[Forge] Ошибка: Превышено время ожидания установки (10 мин).");
                return false;
            }

            return process.ExitCode == 0;
        }
    }
}