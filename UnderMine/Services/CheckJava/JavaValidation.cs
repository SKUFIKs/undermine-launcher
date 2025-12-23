using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UnderMine.Services.CheckJava
{
    internal class JavaValidation
    {
        
        public static async Task ValidateJava21Async()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = "-version",
                    RedirectStandardError = true, 
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.Start();

                string output = await process.StandardError.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(output))
                {
                    output = await process.StandardOutput.ReadToEndAsync();
                }

                await process.WaitForExitAsync();

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    throw new InvalidOperationException("Java не установлена в системе или не добавлена в PATH.");
                }

                var match = Regex.Match(output, @"version ""(\d+)\.");

                if (match.Success)
                {
                    string majorVersion = match.Groups[1].Value;
                    if (majorVersion != "21")
                    {
                        throw new Exception($"Обнаружена Java версии {majorVersion}, но для работы требуется Java 21.");
                    }
                }
                else
                {
                    
                    if (!output.Contains("version \"21"))
                    {
                        throw new Exception("Не удалось определить версию Java или версия не является 21-й.");
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new FileNotFoundException("Не удалось найти java на устройстве. Пожалуйста, установите 21 java.");
            }
        }
    }
}
