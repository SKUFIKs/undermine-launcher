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

        public async Task<bool> InstallAsync(string installerPath, string clientArchiveUrl, IProgress<int> progress, CancellationToken ct, dynamic progressManager, string libHash, string assetsHash, string versionsHash)
        {
            try
            {

                var psi = new ProcessStartInfo
                {
                    FileName = "java",
                    WorkingDirectory = _baseDirectory,
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

                Console.WriteLine($"[Forge] Запуск Java: java -jar \"{installerPath}\" --installClient");

                if (!process.Start())
                {
                    Console.WriteLine("[Forge] Не удалось запустить процесс Java.");
                    return false;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5), ct);
                var waitTask = process.WaitForExitAsync(ct);

                var completedTask = await Task.WhenAny(waitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("[Forge] Превышено время ожидания. Пробуем второй метод...");
                    try
                    {
                        process.Kill(true); 
                    }
                    catch { /* Игнорируем ошибки при убийстве процесса */ }

                    Console.WriteLine("[Forge] Запуск загрузки архива и ручной установки...");
                    return await InstallFallbackAsync(clientArchiveUrl, progress, ct, progressManager, libHash, assetsHash, versionsHash);
                }

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"[Forge] Процесс завершился с кодом ошибки: {process.ExitCode}");
                    return await InstallFallbackAsync(clientArchiveUrl, progress, ct, progressManager, libHash, assetsHash, versionsHash);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Forge Critical Error] {ex}");
                return await InstallFallbackAsync(clientArchiveUrl, progress, ct, progressManager, libHash, assetsHash, versionsHash);
            }
        }


        private async Task<bool> InstallFallbackAsync(string zipUrl, IProgress<int> progress, CancellationToken ct, dynamic progressManager, string libHash, string assetsHash, string versionsHash)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ForgeFallback_" + Guid.NewGuid());
            string tempZipPath = Path.Combine(tempDir, "client.zip");

            try
            {
                Directory.CreateDirectory(tempDir);

                Console.WriteLine($"[Fallback] Загрузка клиента...");
                progress?.Report(5);

                bool downloadSuccess = await DownloadFileAsync(zipUrl, tempZipPath, progress, ct);
                if (!downloadSuccess) return false;

                Console.WriteLine("[Fallback] Распаковка архива...");
                progress?.Report(50);

                await Task.Run(() => ZipFile.ExtractToDirectory(tempZipPath, tempDir), ct);
                try { File.Delete(tempZipPath); } catch { }

                Console.WriteLine($"[Fallback] Копирование файлов в: {_baseDirectory}");
                progress?.Report(80);

                await Task.Run(() => CopyDirectorySafe(tempDir, _baseDirectory), ct);

                try
                {
                    progressManager.Report("Forge", 0.6, "Проверка библиотек...");
                    string librariesFolder = Path.Combine(_baseDirectory, "libraries");
                    bool isLibOk = await CheckingFile.VerifyFolderIntegrityByZipHash(librariesFolder, libHash);

                    progressManager.Report("Forge", 0.7, "Проверка ресурсов...");
                    string assetsFolder = Path.Combine(_baseDirectory, "assets");
                    bool isAssetsOk = await CheckingFile.VerifyFolderIntegrityByZipHash(assetsFolder, assetsHash);

                    progressManager.Report("Forge", 0.8, "Проверка версий...");
                    string versionsFolder = Path.Combine(_baseDirectory, "versions");
                    bool isVersionsOk = await CheckingFile.VerifyFolderIntegrityByZipHash(versionsFolder, versionsHash);

                    if (!isAssetsOk || !isLibOk || !isVersionsOk)
                    {
                        progressManager.Report("Forge", 1.0, "Ошибка установки игры.");
                        Console.WriteLine("Ошибка проверки целостности файлов после установки.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Fallback Warning] Ошибка при проверке хэшей: {ex.Message}");
                }

                progress?.Report(100);
                Console.WriteLine("[Fallback] Установка полностью завершена.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Критическая ошибка: {ex}");
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch (Exception cleanupEx)
                {
                    Console.WriteLine($"[Fallback Warning] Не удалось очистить temp: {cleanupEx.Message}");
                }
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