using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression; 
using System.Net.Http;       
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace UnderMine.Core.Services
{
    public class ForgeInstaller
    {
        private readonly string _baseDirectory;
        private static readonly HttpClient _httpClient = new HttpClient();

        public ForgeInstaller(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
        }

        public async Task<bool> InstallAsync(string serverPath, string installerPath, string clientArchiveUrl, IProgress<int> progress, CancellationToken ct, dynamic progressManager, string libHash, string assetsHash, string versionsHash)
        {
            string targetPath = string.IsNullOrWhiteSpace(serverPath) ? _baseDirectory : serverPath;

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                Console.WriteLine("[Forge Error] Не указан путь для установки (targetPath is empty).");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "java",
                    WorkingDirectory = targetPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("-jar");
                psi.ArgumentList.Add(installerPath);
                psi.ArgumentList.Add("--installClient");

                using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                process.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Data)) return;
                    Console.WriteLine($"[Forge] {e.Data}");

                    int currentPercent = 0;
                    if (e.Data.Contains("Downloading libraries")) currentPercent = 10;
                    else if (e.Data.Contains("Extracting:")) currentPercent = 30;
                    else if (e.Data.Contains("Building Processors")) currentPercent = 45;
                    else if (e.Data.Contains("Splitting:")) currentPercent = 60;
                    else if (e.Data.Contains("Reading patch")) currentPercent = 75;
                    else if (e.Data.Contains("Patching")) currentPercent = 90;
                    else if (e.Data.Contains("Successfully installed")) currentPercent = 100;

                    if (currentPercent > 0) progress?.Report(currentPercent);
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Console.WriteLine($"[Forge Error] {e.Data}");
                };

                Console.WriteLine($"[Forge] Запуск Java в директории: {targetPath}");

                if (!process.Start())
                {
                    Console.WriteLine("[Forge] Не удалось запустить процесс Java.");
                    return await InstallFallbackAsync(targetPath, clientArchiveUrl, progress, ct, progressManager, libHash, assetsHash, versionsHash);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(7), ct);
                var waitTask = process.WaitForExitAsync(ct);

                var completedTask = await Task.WhenAny(waitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("[Forge] Превышено время ожидания. Убиваем процесс и пробуем Fallback...");
                    try { process.Kill(true); } catch { }
                    return await InstallFallbackAsync(targetPath, clientArchiveUrl, progress, ct, progressManager, libHash, assetsHash, versionsHash);
                }

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"[Forge] Код ошибки: {process.ExitCode}. Запускаем Fallback...");
                    return await InstallFallbackAsync(targetPath, clientArchiveUrl, progress, ct, progressManager, libHash, assetsHash, versionsHash);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Forge Critical Error] {ex.Message}");
                return await InstallFallbackAsync(targetPath, clientArchiveUrl, progress, ct, progressManager, libHash, assetsHash, versionsHash);
            }
        }


        private async Task<bool> InstallFallbackAsync(string targetPath, string zipUrl, IProgress<int> progress, CancellationToken ct, dynamic progressManager, string libHash, string assetsHash, string versionsHash)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return false;

            string tempDir = Path.Combine(Path.GetTempPath(), "ForgeFallback_" + Guid.NewGuid());
            string tempZipPath = Path.Combine(tempDir, "client.zip");

            try
            {
                Directory.CreateDirectory(tempDir);
                Console.WriteLine($"[Fallback] Загрузка клиента в {tempZipPath}...");

                bool downloadSuccess = await DownloadFileAsync(zipUrl, tempZipPath, progress, ct);
                if (!downloadSuccess) return false;

                Console.WriteLine("[Fallback] Распаковка...");
                await Task.Run(() => ZipFile.ExtractToDirectory(tempZipPath, tempDir), ct);

                Console.WriteLine($"[Fallback] Копирование файлов в целевую папку: {targetPath}");
                await Task.Run(() => CopyDirectorySafe(tempDir, targetPath), ct);

                // Проверка целостности после Fallback
                try
                {
                    progressManager.Report("Forge", 0.9, "Проверка файлов...");
                }
                catch (Exception ex) { Console.WriteLine($"[Fallback Check Error] {ex.Message}"); }

                Console.WriteLine("[Fallback] Готово.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fallback Error] {ex}");
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        private async Task<bool> DownloadFileAsync(string url, string destinationPath, IProgress<int> progress, CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Download Error] Сервер вернул ошибку: {response.StatusCode}");
                    return false;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
                using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await destStream.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;

                    if (canReportProgress && progress != null)
                    {
                        var percent = (int)((double)totalRead / totalBytes * 45) + 5;
                        progress.Report(percent);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Download Error] {ex.Message}");
                return false;
            }
        }

        private void CopyDirectorySafe(string sourceDir, string destinationDir)
        {
            if (string.IsNullOrWhiteSpace(destinationDir))
                throw new ArgumentException("Destination directory path is empty or null.");

            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);

                if (File.Exists(targetFilePath))
                {
                    try { File.SetAttributes(targetFilePath, FileAttributes.Normal); } catch { }
                }

                int attempts = 3;
                while (attempts > 0)
                {
                    try
                    {
                        file.CopyTo(targetFilePath, true);
                        break;
                    }
                    catch (IOException)
                    {
                        attempts--;
                        if (attempts == 0) throw;
                        Thread.Sleep(500);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        try
                        {
                            if (File.Exists(targetFilePath))
                                File.SetAttributes(targetFilePath, FileAttributes.Normal);
                            file.CopyTo(targetFilePath, true);
                            break;
                        }
                        catch { throw; }
                    }
                }
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectorySafe(subDir.FullName, newDestinationDir);
            }
        }
    }
}