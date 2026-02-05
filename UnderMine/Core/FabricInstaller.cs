using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UnderMine.Core
{
    public class FabricInstaller
    {
        public async Task<bool> InstallAsync(string gamePath, string mcVersion, string loaderVersion, string installerPath, CancellationToken ct, string? JavaPath = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = !string.IsNullOrWhiteSpace(JavaPath) ? JavaPath : "java",
                    Arguments = $"-jar \"{installerPath}\" client -mcversion {mcVersion} -loader {loaderVersion} -dir \"{gamePath}\" -snapshot -noprofile",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"[Fabric] {e.Data}");
                };

                if (!process.Start())
                {
                    Console.WriteLine("[Fabric Error] Не удалось запустить процесс Java.");
                    return false;
                }

                process.BeginOutputReadLine();

                await process.WaitForExitAsync(ct);

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"[Fabric Error] Процесс завершился с кодом {process.ExitCode}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fabric Critical Error] {ex.Message}");
                return false;
            }
        }
    }
}