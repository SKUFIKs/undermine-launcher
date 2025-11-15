using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Buffers;

namespace UnderMine.Core.server_1
{
    public class ModLoader : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const int MaxParallelDownloads = 3;
        private const int BufferSize = 81920;

        public event Action<string, double, string>? OnProgress;
        public event Action<string>? OnMessage;

        private void ReportStage(string message, double progress)
        {
            OnMessage?.Invoke(message);
            OnProgress?.Invoke(message, progress, "");
        }

        private int _totalMods;
        private int _completedMods;
        private readonly object _progressLock = new();
        private double _globalProgress;

        private static readonly SemaphoreSlim _validationSemaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount / 2));

        public ModLoader()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        public async Task InstallOrUpdateMods(string modsDirectory, string manifestUrl)
        {
            if (string.IsNullOrEmpty(manifestUrl))
                throw new ArgumentException("Не удалось получить список модов");

            modsDirectory = GetSafeDirectory(modsDirectory);
            Directory.CreateDirectory(modsDirectory);

            try
            {
                ReportStage("Подготовка к установке модов...", 0);

                var manifest = await DownloadManifestAsync(manifestUrl);

                ReportStage("Получаем список модов...", 16.6);

                await UpdateModsAsync(manifest, modsDirectory);

                ReportStage("Проверяем файлы...", 66.6);

                await ValidateAllModsIntegrity(modsDirectory);

                ReportStage("Завершение установки...", 83.2);

                ReportStage("Все моды установлены!", 100);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка установки модов: {ex.Message}", ex);
            }
        }

        private string GetSafeDirectory(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return path; 
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string dir = Path.Combine(home, "Library", "Application Support", "UnderMine", "versions", "TechnoMagic", Path.GetFileName(path));
                Directory.CreateDirectory(dir);
                return dir;
            }
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dir = Path.Combine(home, ".config", "UnderMine", "versions", "TechnoMagic", Path.GetFileName(path));
                Directory.CreateDirectory(dir);
                return dir;
            }
        }


        private async Task<JObject> DownloadManifestAsync(string manifestUrl)
        {
            string json = await _httpClient.GetStringAsync(manifestUrl);
            return JObject.Parse(json);
        }

        private async Task UpdateModsAsync(JObject manifest, string modsPath)
        {
            ReportStage("Загружаем моды...", 33.3);

            var mods = manifest["Mods"] as JArray
                       ?? throw new Exception("Manifest missing Mods array");

            var allowedModsToken = manifest["AllowedUserMods"] as JArray;
            var allowedUserMods = new HashSet<string>(
                (allowedModsToken?
                    .Select(t => t.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n)) ?? Array.Empty<string>()),
                StringComparer.OrdinalIgnoreCase);

            var modList = mods
                .Select(m => new ModInfo
                {
                    Name = (string?)m["Name"],
                    Url = (string?)m["Url"],
                    ExpectedHash = (string?)m["Hash"],
                    FilePath = Path.Combine(modsPath, (string?)m["Name"] ?? "")
                })
                .Where(m => !string.IsNullOrEmpty(m.Name) && !string.IsNullOrEmpty(m.Url))
                .ToList();

            _totalMods = modList.Count;
            _completedMods = 0;

            var semaphore = new SemaphoreSlim(MaxParallelDownloads);
            var tasks = modList.Select(async mod =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await ProcessSingleModAsync(mod);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);

            CleanupOldMods(modsPath, modList.Select(m => m.Name!).ToHashSet(), allowedUserMods);
        }


        private async Task ProcessSingleModAsync(ModInfo mod)
        {
            if (await NeedsDownloadAsync(mod))
            {
                await DownloadAndValidateModAsync(mod);
            }
            else
            {
                if (!await ValidateModIntegrity(mod.FilePath))
                {
                    await DownloadAndValidateModAsync(mod);
                }
            }
        }

        private async Task ValidateAllModsIntegrity(string modsDirectory)
        {
            if (!Directory.Exists(modsDirectory))
                return;

            var modFiles = Directory.GetFiles(modsDirectory, "*.jar");
            var corruptedMods = new List<string>();

            // Validate sequentially to avoid too many concurrent CPU validations
            foreach (var modFile in modFiles)
            {
                if (!await ValidateModIntegrity(modFile))
                {
                    corruptedMods.Add(Path.GetFileName(modFile));
                }
            }

            if (corruptedMods.Count > 0)
                throw new Exception($"Повреждены моды: {string.Join(", ", corruptedMods)}");
        }

        private async Task DownloadAndValidateModAsync(ModInfo mod)
        {
            string tmp = mod.FilePath + ".tmp";
            try
            {
                // Download and compute hash during download
                string computedHash = await DownloadModAsync(mod.Url!, tmp);

                if (!string.IsNullOrEmpty(mod.ExpectedHash))
                {
                    if (!computedHash.Equals(mod.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                        throw new Exception($"Hash mismatch (ожидалось {mod.ExpectedHash}, получено {computedHash})");
                }

                if (!await ValidateModIntegrity(tmp))
                    throw new Exception("Файл загружен с ошибкой, архивация повреждена.");

                if (File.Exists(mod.FilePath))
                    File.Delete(mod.FilePath);
                File.Move(tmp, mod.FilePath, true);
            }
            finally
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
        }

        private async Task<bool> NeedsDownloadAsync(ModInfo mod)
        {
            if (!File.Exists(mod.FilePath)) return true;
            if (string.IsNullOrEmpty(mod.ExpectedHash)) return false;

            string hash = await ComputeSha256Async(mod.FilePath);
            return !hash.Equals(mod.ExpectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> DownloadModAsync(string url, string dest, int retries = 3)
        {
            string modName = Path.GetFileName(dest);

            for (int i = 1; i <= retries; i++)
            {
                try
                {
                    Console.WriteLine($"→ [Download] {modName}");

                    using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();

                    long totalBytes = resp.Content.Headers.ContentLength ?? -1;
                    long downloadedBytes = 0;

                    await using var src = await resp.Content.ReadAsStreamAsync();
                    await using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None,
                        BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                    var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                    try
                    {
                        int bytesRead;
                        var sw = Stopwatch.StartNew();

                        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                        while ((bytesRead = await src.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            hasher.AppendData(buffer, 0, bytesRead);
                            await dst.WriteAsync(buffer.AsMemory(0, bytesRead));
                            downloadedBytes += bytesRead;

                            if (sw.ElapsedMilliseconds >= 300)
                            {
                                sw.Restart();

                                double percent = totalBytes > 0
                                    ? (double)downloadedBytes / totalBytes * 100
                                    : 0;

                                Console.Write($"\r   {modName}: {percent:F1}%   ");
                            }
                        }

                        Console.WriteLine($"\r✓ {modName} загружен успешно.           ");

                        int completed = Interlocked.Increment(ref _completedMods);
                        double totalProgress = (double)completed / Math.Max(1, _totalMods);
                        _globalProgress = totalProgress;

                        double finalMapped = 50 + _globalProgress * 40;
                        OnProgress?.Invoke("Установка модов...", finalMapped, "InstallingMods");

                        var hash = hasher.GetHashAndReset();
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                catch when (i < retries)
                {
                    Console.WriteLine($"\n⚠ Ошибка при загрузке {modName}, повтор {i + 1}...");
                    await Task.Delay(1000 * i);
                }
                catch
                {
                    Console.WriteLine($"\n✗ Не удалось загрузить {modName}");
                    throw;
                }
            }

            throw new Exception("Не удалось загрузить мод после нескольких попыток.");
        }

        private async Task<string> ComputeSha256Async(string path)
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var fs = File.OpenRead(path);
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    sha.AppendData(buffer, 0, bytesRead);
                }

                var hash = sha.GetHashAndReset();
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task<bool> ValidateModIntegrity(string file)
        {
            await _validationSemaphore.WaitAsync();
            try
            {
                if (!File.Exists(file)) return false;
                using var zip = ZipFile.OpenRead(file);
                return zip.Entries.Count > 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                _validationSemaphore.Release();
            }
        }

        private void CleanupOldMods(string dir, HashSet<string> required, HashSet<string> allowed)
        {
            foreach (var file in Directory.GetFiles(dir, "*.jar"))
            {
                string name = Path.GetFileName(file);
                if (!required.Contains(name) && !allowed.Contains(name))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        public void Dispose() => _httpClient.Dispose();

        private struct ModInfo
        {
            public string? Name { get; set; }
            public string? Url { get; set; }
            public string? ExpectedHash { get; set; }
            public string FilePath { get; set; }
        }
    }
}