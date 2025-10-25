using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace UnderMine.Game.server_1
{
    public class ModInstaller : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const int MAX_PARALLEL_DOWNLOADS = 3;
        private const int BUFFER_SIZE = 81920;

        public ModInstaller()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
        }

        public async Task InstallOrUpdateMods(string modsDirectory, string manifestUrl)
        {
            if (string.IsNullOrEmpty(manifestUrl))
                throw new ArgumentException("Manifest URL cannot be null or empty");

            if (!Directory.Exists(modsDirectory))
                Directory.CreateDirectory(modsDirectory);

            string manifestPath = Path.Combine(modsDirectory, "manifest.json");
            
            try
            {
                var manifest = await DownloadManifestAsync(manifestUrl);
                await UpdateModsAsync(manifest, modsDirectory);
                
                // Финальная проверка всех модов на целостность
                await ValidateAllModsIntegrity(modsDirectory);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error installing mods: {ex.Message}", ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(manifestPath))
                        File.Delete(manifestPath);
                }
                catch
                {
                    // Игнорируем ошибки удаления временного файла
                }
            }
        }

        private async Task<JObject> DownloadManifestAsync(string manifestUrl)
        {
            var response = await _httpClient.GetStringAsync(manifestUrl);
            return JObject.Parse(response);
        }

        private async Task UpdateModsAsync(JObject manifest, string modsPath)
        {
            var mods = manifest["Mods"] as JArray;
            var allowedModsToken = manifest["AllowedUserMods"] as JArray;

            if (mods == null || !mods.Any())
                throw new Exception("The mod list is empty.");

            var allowedUserMods = new HashSet<string>(
                allowedModsToken?
                    .Select(t => t?.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n)) 
                    .Select(n => n!) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase
            );

            var requiredMods = new List<ModInfo>();
            var downloadedMods = new List<string>();
            var failedMods = new List<string>();

            // Парсинг модов заранее
            foreach (var mod in mods)
            {
                string? modName = mod["Name"]?.ToString();
                string? modUrl = mod["Url"]?.ToString();
                string? expectedHash = mod["Hash"]?.ToString();

                if (string.IsNullOrWhiteSpace(modName) || string.IsNullOrWhiteSpace(modUrl))
                    continue;

                requiredMods.Add(new ModInfo
                {
                    Name = modName,
                    Url = modUrl,
                    ExpectedHash = expectedHash,
                    FilePath = Path.Combine(modsPath, modName)
                });
            }

            // Параллельная проверка хешей и загрузка
            await ProcessModsInParallel(requiredMods, downloadedMods, failedMods);

            // Очистка устаревших модов
            CleanupOldMods(modsPath, requiredMods.Select(m => m.Name).ToHashSet(), allowedUserMods);

            // Отчет о результате
            Console.WriteLine($"[MODS] Успешно обработано: {downloadedMods.Count}/{requiredMods.Count}");
            if (failedMods.Count > 0)
            {
                Console.WriteLine($"[MODS] Не удалось загрузить: {string.Join(", ", failedMods)}");
                throw new Exception($"Не удалось загрузить моды: {string.Join(", ", failedMods)}");
            }

            if (!downloadedMods.Any())
                Console.WriteLine("[MODS] All mods are up to date.");
        }

        private async Task ProcessModsInParallel(List<ModInfo> requiredMods, List<string> downloadedMods, List<string> failedMods)
        {
            var semaphore = new SemaphoreSlim(MAX_PARALLEL_DOWNLOADS);
            var tasks = new List<Task>();
            
            int currentIndex = 0;
            int totalMods = requiredMods.Count;

            foreach (var mod in requiredMods)
            {
                await semaphore.WaitAsync();
                
                var task = ProcessSingleModAsync(mod, downloadedMods, failedMods, totalMods, Interlocked.Increment(ref currentIndex), semaphore);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessSingleModAsync(ModInfo mod, List<string> downloadedMods, List<string> failedMods, 
            int totalMods, int currentIndex, SemaphoreSlim semaphore)
        {
            try
            {
                ReportProgress(currentIndex, totalMods, mod.Name, 0, "Checking");

                bool needDownload = await NeedsDownloadAsync(mod);
                
                if (needDownload)
                {
                    await DownloadAndValidateModAsync(
                        mod, 
                        percent => ReportProgress(currentIndex, totalMods, mod.Name, percent, "Downloading")
                    );
                    
                    lock (downloadedMods)
                    {
                        downloadedMods.Add(mod.Name);
                    }
                    ReportProgress(currentIndex, totalMods, mod.Name, 100, "Completed");
                }
                else
                {
                    // Проверяем целостность существующего мода
                    if (await ValidateModIntegrity(mod.FilePath))
                    {
                        ReportProgress(currentIndex, totalMods, mod.Name, 100, "Valid");
                    }
                    else
                    {
                        Console.WriteLine($"[MODS] Обнаружен поврежденный мод: {mod.Name}, перезагружаем...");
                        await DownloadAndValidateModAsync(
                            mod, 
                            percent => ReportProgress(currentIndex, totalMods, mod.Name, percent, "Re-downloading")
                        );
                        lock (downloadedMods)
                        {
                            downloadedMods.Add(mod.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to process {mod.Name}: {ex.Message}");
                lock (failedMods)
                {
                    failedMods.Add(mod.Name);
                }
                ReportProgress(currentIndex, totalMods, mod.Name, 0, $"Failed: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task DownloadAndValidateModAsync(ModInfo mod, Action<double> progress)
        {
            string tempPath = mod.FilePath + ".tmp";
            
            try
            {
                // Скачиваем во временный файл
                await DownloadModAsync(mod.Url, tempPath, progress);

                // Проверяем хеш если указан
                if (!string.IsNullOrEmpty(mod.ExpectedHash))
                {
                    string actualHash = await ComputeSha256Async(tempPath);
                    if (!actualHash.Equals(mod.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Hash mismatch for {mod.Name}. Expected: {mod.ExpectedHash}, Actual: {actualHash}");
                    }
                }

                // Проверяем целостность ZIP
                if (!await ValidateModIntegrity(tempPath))
                {
                    throw new Exception($"Downloaded mod {mod.Name} is not a valid ZIP archive");
                }

                // Заменяем старый файл новым
                if (File.Exists(mod.FilePath))
                    File.Delete(mod.FilePath);
                File.Move(tempPath, mod.FilePath);
            }
            catch
            {
                // Удаляем временный файл при ошибке
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private void ReportProgress(int current, int total, string name, double percent, string status)
        {
            Console.WriteLine($"[MODS] ({current}/{total}) {name} — {status} {percent:F1}%");
        }

        private async Task<bool> NeedsDownloadAsync(ModInfo mod)
        {
            if (!File.Exists(mod.FilePath))
                return true;

            if (string.IsNullOrEmpty(mod.ExpectedHash))
                return false;

            string actualHash = await ComputeSha256Async(mod.FilePath);
            return !actualHash.Equals(mod.ExpectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private async Task DownloadModAsync(string url, string destinationPath, Action<double>? progress = null, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes > 0;

                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = new FileStream(
                        destinationPath, 
                        FileMode.Create, 
                        FileAccess.Write, 
                        FileShare.None, 
                        BUFFER_SIZE, 
                        true
                    );

                    var buffer = new byte[BUFFER_SIZE];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;

                        if (canReportProgress)
                        {
                            double percent = (double)totalRead / totalBytes * 100;
                            progress?.Invoke(percent);
                        }
                    }

                    // Принудительно сбрасываем буфер
                    await fileStream.FlushAsync();
                    progress?.Invoke(100);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                        throw new Exception($"Failed to download {url} after {maxRetries} attempts: {ex.Message}");

                    Console.WriteLine($"[RETRY] Attempt {attempt} failed for {Path.GetFileName(destinationPath)}: {ex.Message}");
                    await Task.Delay(1000 * attempt);
                }
            }
        }

        private async Task<string> ComputeSha256Async(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private async Task<bool> ValidateModIntegrity(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                    return false;

                // Проверяем сигнатуру ZIP файла
                byte[] header = new byte[4];
                using (var fs = File.OpenRead(filePath))
                {
                    if (await fs.ReadAsync(header.AsMemory(0, 4)) < 4)
                        return false;
                }

                // Проверяем сигнатуру ZIP (PK)
                if (header[0] != 0x50 || header[1] != 0x4B)
                    return false;

                // Пытаемся открыть как ZIP архив
                using (var archive = ZipFile.OpenRead(filePath))
                {
                    // Проверяем наличие необходимых файлов для мода Forge
                    var entries = archive.Entries;
                    bool hasManifest = entries.Any(e => e.FullName.Equals("META-INF/MANIFEST.MF", StringComparison.OrdinalIgnoreCase));
                    
                    // Для модов Forge обычно должен быть либо mods.toml, либо mcmod.info
                    bool hasModConfig = entries.Any(e => 
                        e.FullName.Equals("META-INF/mods.toml", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.Equals("mcmod.info", StringComparison.OrdinalIgnoreCase));

                    if (!hasManifest)
                    {
                        Console.WriteLine($"[VALIDATE] Warning: Mod {Path.GetFileName(filePath)} missing META-INF/MANIFEST.MF");
                    }

                    return entries.Count > 0; // Должен быть хотя бы один файл
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VALIDATE] Invalid mod {Path.GetFileName(filePath)}: {ex.Message}");
                return false;
            }
        }

        private async Task ValidateAllModsIntegrity(string modsDirectory)
        {
            Console.WriteLine("[VALIDATE] Checking all mods integrity...");
            
            if (!Directory.Exists(modsDirectory))
                return;

            var modFiles = Directory.GetFiles(modsDirectory, "*.jar");
            var corruptedMods = new List<string>();

            foreach (var modFile in modFiles)
            {
                if (!await ValidateModIntegrity(modFile))
                {
                    corruptedMods.Add(Path.GetFileName(modFile));
                }
            }

            if (corruptedMods.Count > 0)
            {
                throw new Exception($"Found corrupted mods: {string.Join(", ", corruptedMods)}");
            }

            Console.WriteLine($"[VALIDATE] All {modFiles.Length} mods are valid");
        }

        private void CleanupOldMods(string modsPath, HashSet<string> requiredMods, HashSet<string> allowedUserMods)
        {
            try
            {
                foreach (var modPath in Directory.GetFiles(modsPath, "*.jar"))
                {
                    string fileName = Path.GetFileName(modPath);

                    if (!requiredMods.Contains(fileName) && !allowedUserMods.Contains(fileName))
                    {
                        try
                        {
                            File.Delete(modPath);
                            Console.WriteLine($"[CLEANUP] Removed old mod: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARNING] Could not delete {fileName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] During cleanup: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private struct ModInfo
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string ExpectedHash { get; set; }
            public string FilePath { get; set; }
        }
    }
}