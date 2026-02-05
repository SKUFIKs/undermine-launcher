using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
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
using UnderMine.Services;

namespace UnderMine.Core
{
    public class ModLoaderService
    {
        private readonly HttpClient _httpClient;
        private readonly ProgressReporter _progress;
        private readonly ProgressManager _progressManager;
        private const int BufferSize = 81920;

        public ModLoaderService(HttpClient httpClient, ProgressReporter progress, ProgressManager progressManager)
        {
            _httpClient = httpClient;
            _progress = progress;
            _progressManager = progressManager;
        }

        private static readonly SemaphoreSlim _validationSemaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount / 2));

        public async Task InstallOrUpdateModsAsync(string modsDirectory, string manifestUrl, CancellationToken ct)
        {
            //debug

            //manifestUrl = "https://www.dropbox.com/scl/fi/yegsaya731651z9htbnzm/manifest.json?rlkey=yfqf0lq8j2fsf8jhx7mr4u9eu&st=axipxsjg&dl=1";

            _progress.Log("Проверка модов...");

            try
            {
                if (!Directory.Exists(modsDirectory))
                {
                    Directory.CreateDirectory(modsDirectory);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось создать папку модов: {ex.Message}");
            }

            var manifestJson = await _httpClient.GetStringAsync(manifestUrl, ct);
            var manifest = JObject.Parse(manifestJson);
            var modList = ParseManifest(manifest, modsDirectory);

            CleanupObsoleteMods(modsDirectory, modList);

            int total = modList.Count;
            int completed = 0;

            var semaphore = new SemaphoreSlim(3); 

            var tasks = modList.Select(async mod =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    if (await NeedsDownloadAsync(mod) || !await ValidateModIntegrity(mod.FilePath))
                    {
                        await ProcessModAsync(mod, ct);
                    }
                }
                finally
                {
                    semaphore.Release();
                    int current = Interlocked.Increment(ref completed);

                    
                    double ratio = (double)current / total;

                    _progressManager.Report("Mods", ratio, $"Загрузка модов...");

                }
            });

            await Task.WhenAll(tasks);
            _progress.Log("Моды успешно установлены...");
        }


        private async Task ProcessModAsync(ModInfo mod, CancellationToken ct)
        {
            string tmp = mod.FilePath + ".tmp";
            try
            {
                string computedHash = await DownloadModAsync(mod.Url!, tmp);
                if (!string.IsNullOrEmpty(mod.ExpectedHash) && !computedHash.Equals(mod.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Hash mismatch (ожидалось {mod.ExpectedHash}, получено {computedHash})");

                if (!await ValidateModIntegrity(tmp))
                    throw new Exception("Файл загружен с ошибкой, архивация повреждена.");

                if (File.Exists(mod.FilePath)) File.Delete(mod.FilePath);
                File.Move(tmp, mod.FilePath, true);
            }
            finally
            {
                if (File.Exists(tmp)) try { File.Delete(tmp); } catch { }
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
            catch { return false; }
            finally { _validationSemaphore.Release(); }
        }
        private async Task<string> DownloadModAsync(string url, string dest, int retries = 3)
        {
            string modName = Path.GetFileName(dest);

            for (int i = 1; i <= retries; i++)
            {
                try
                             {
                    Console.WriteLine($"[Download] {modName}");

                    using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();

                    long totalBytes = resp.Content.Headers.ContentLength ?? -1;

                    await using var src = await resp.Content.ReadAsStreamAsync();
                    await using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

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
                        }

                        Console.WriteLine($"\r {modName} загружен успешно.            ");

                        return BitConverter.ToString(hasher.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                catch (Exception) when (i < retries)
                {
                    Console.WriteLine($"\n Ошибка при загрузке {modName}, повтор {i + 1}...");
                    await Task.Delay(1000 * i);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n Не удалось загрузить {modName}: {ex.Message}");
                    throw;
                }
            }
            throw new Exception("Не удалось загрузить мод.");
        }

        private async Task<bool> NeedsDownloadAsync(ModInfo mod)
        {
            if (!File.Exists(mod.FilePath)) return true;
            if (string.IsNullOrEmpty(mod.ExpectedHash)) return false;

            string currentHash = await ComputeSha256Async(mod.FilePath);
            return !currentHash.Equals(mod.ExpectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> ComputeSha256Async(string path)
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    sha.AppendData(buffer, 0, bytesRead);
                }
                return BitConverter.ToString(sha.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
        private void CleanupObsoleteMods(string modsDirectory, List<ModInfo> manifestMods)
        {
            try
            {
                var allowedFiles = new HashSet<string>(
                    manifestMods.Select(m => Path.GetFileName(m.FilePath)),
                    StringComparer.OrdinalIgnoreCase
                );

                var existingFiles = Directory.GetFiles(modsDirectory);

                foreach (var file in existingFiles)
                {
                    string fileName = Path.GetFileName(file);

                    if (!allowedFiles.Contains(fileName) && !fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Delete(file);
                            _progress.Log($"Удален лишний мод: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            _progress.Log($"Не удалось удалить {fileName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _progress.Log($"Ошибка при очистке папки модов: {ex.Message}");
            }
        }
        private List<ModInfo> ParseManifest(JObject manifest, string modsPath)
        {
            return (manifest["Mods"] as JArray)?
                .Select(m => new ModInfo
                {
                    Name = m["Name"]?.ToString(),
                    Url = m["Url"]?.ToString(),
                    ExpectedHash = m["Hash"]?.ToString(),
                    FilePath = Path.Combine(modsPath, m["Name"]?.ToString() ?? "")
                }).ToList() ?? new List<ModInfo>();
        }

        private struct ModInfo
        {
            public string? Name;
            public string? Url;
            public string? ExpectedHash;
            public string FilePath;
        }
    }
}