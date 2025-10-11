using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using CmlLib.Core.Installers; // для SyncProgress

namespace UnderMine.Game.server_1
{
    public class ModInstaller
    {
        public async Task InstallOrUpdateMods(string modsDirectory, string manifestUrl)
        {
            string manifestPath = Path.Combine(modsDirectory, "manifest.json");

            if (!Directory.Exists(modsDirectory))
                Directory.CreateDirectory(modsDirectory);

            try
            {
                if (string.IsNullOrEmpty(manifestUrl))
                    throw new Exception("Failed to load mod list.");
                
                var manifest = await DownloadManifestAsync(manifestUrl);
                File.WriteAllText(manifestPath, manifest.ToString());
                await UpdateModsAsync(manifest, modsDirectory);
                
                if (File.Exists(manifestPath))
                    File.Delete(manifestPath);
            }
            catch (Exception ex)
            {
                throw new Exception("Error installing mods: " + ex.Message);
            }
        }

        private static async Task<JObject> DownloadManifestAsync(string manifestUrl)
        {
            using var client = new HttpClient();
            var response = await client.GetStringAsync(manifestUrl);
            return JObject.Parse(response);
        }

        private static async Task UpdateModsAsync(JObject manifest, string modsPath)
        {
            var mods = manifest["Mods"] as JArray;
            var allowedModsToken = manifest["AllowedUserMods"] as JArray;

            if (mods == null)
                throw new Exception("The mod list is empty.");

            List<string> allowedUserMods = allowedModsToken?
                .Select(t => t.ToString())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList() ?? new List<string>();

            List<string> requiredModNames = new();
            List<string> downloadedMods = new();

            var fileProgress = new SyncProgress<(int current, int total, string name, double percent)>(
                e => Console.WriteLine($"[MODS] ({e.current}/{e.total}) {e.name} — {e.percent:F1}%"));

            int totalMods = mods.Count;
            int currentModIndex = 0;

            foreach (var mod in mods)
            {
                currentModIndex++;

                string? modName = mod["Name"]?.ToString();
                string? modUrl = mod["Url"]?.ToString();
                string? expectedHash = mod["Hash"]?.ToString();

                if (string.IsNullOrWhiteSpace(modName) || string.IsNullOrWhiteSpace(modUrl))
                    continue;

                requiredModNames.Add(modName);
                string modFilePath = Path.Combine(modsPath, modName);
                bool needDownload = true;

                if (File.Exists(modFilePath))
                {
                    if (!string.IsNullOrEmpty(expectedHash))
                    {
                        string actualHash = ComputeSha256(modFilePath);
                        if (actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                        {
                            // Мод уже актуален
                            needDownload = false;
                        }
                    }
                    else
                        needDownload = false;
                }

                if (needDownload)
                {
                    try
                    {
                        await DownloadModAsync(
                            modUrl,
                            modFilePath,
                            progress => fileProgress.Report((currentModIndex, totalMods, modName, progress))
                        );

                        downloadedMods.Add(modName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to load {modName}: {ex.Message}");
                    }
                }
            }

            // Удаление неразрешённых модов
            foreach (var modPath in Directory.GetFiles(modsPath, "*.jar"))
            {
                string fileName = Path.GetFileName(modPath);

                if (!requiredModNames.Contains(fileName) && !allowedUserMods.Contains(fileName))
                {
                    try
                    {
                        File.Delete(modPath);
                    }
                    catch { }
                }
            }

            if (!downloadedMods.Any())
                Console.WriteLine("[MODS] All mods are up to date.");
        }

        private static async Task DownloadModAsync(string url, string destinationPath, Action<double>? progress = null, int maxRetries = 2)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(100);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1;

                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;

                        if (canReportProgress)
                        {
                            double percent = (double)totalRead / totalBytes * 100;
                            progress?.Invoke(percent);
                        }
                    }
                    progress?.Invoke(100);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                        throw new Exception($"Loading error {url}: {ex.Message}");

                    await Task.Delay(2000);
                }
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}


