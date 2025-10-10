using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UnderMine.Game.server_1
{
    public class ModInstaller
    {
        public async Task InstallOrUpdateMods(string modsDirectory, string manifestUrl)
        {
            string manifestPath = Path.Combine(modsDirectory, "manifest.json");

            if (!Directory.Exists(modsDirectory))
            {
                Directory.CreateDirectory(modsDirectory);
            }
            try
            {
                if (string.IsNullOrEmpty(manifestUrl))
                    throw new Exception("Failed to load mod list.");
                
                var manifest = await DownloadManifestAsync(manifestUrl);

                File.WriteAllText(manifestPath, manifest.ToString());

                await UpdateModsAsync(manifest, modsDirectory);
                
                if (File.Exists(manifestPath))
                {
                    File.Delete(manifestPath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error installing mods: " + ex.Message);
            }
        }

        private static async Task<JObject> DownloadManifestAsync(string manifestUrl)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetStringAsync(manifestUrl);
                return JObject.Parse(response);
            }
        }

        private static async Task UpdateModsAsync(JObject manifest, string modsPath)
        {
            var mods = manifest["Mods"] as JArray;
            var allowedModsToken = manifest["AllowedUserMods"] as JArray;

            if (mods == null)
            {
                throw new Exception("The mod list is empty.");
            }

            List<string> allowedUserMods = allowedModsToken?
                .Select(token => token.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList() ?? new List<string>();

            List<string> requiredModNames = new();
            List<string> downloadedMods = new();

            foreach (var mod in mods)
            {
                string? modName = mod["Name"]?.ToString();
                string? modUrl = mod["Url"]?.ToString();
                string? expectedHash = mod["Hash"]?.ToString();

                if (string.IsNullOrWhiteSpace(modName) || string.IsNullOrWhiteSpace(modUrl))
                {
                    Console.WriteLine("[WARN] Missing Mod (No Name or Url)");
                    continue;
                }

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
                            Console.WriteLine($"[SKIP] The mod is relevant: {modName}");
                            needDownload = false;
                        }
                        else
                        {
                            Console.WriteLine($"[INFO] Hash does not match. Rebooting: {modName}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[INFO] No hash for {modName}, skipping the check.");
                        needDownload = false;
                    }
                }

                if (needDownload)
                {
                    try
                    {
                        Console.WriteLine($"[INFO] Loading mod: {modName}...");
                        await DownloadModAsync(modUrl, modFilePath);
                        Console.WriteLine($"[OK] Mod uploaded: {modName}");
                        downloadedMods.Add(modName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to load {modName}: {ex.Message}");
                    }
                }
            }

            foreach (var modPath in Directory.GetFiles(modsPath, "*.jar"))
            {
                string fileName = Path.GetFileName(modPath);

                if (!requiredModNames.Contains(fileName) && !allowedUserMods.Contains(fileName))
                {
                    try
                    {
                        File.Delete(modPath);
                        Console.WriteLine($"[INFO] Unknown mod detected. Removing {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to delete {fileName}: {ex.Message}");
                    }
                }
            }

            if (downloadedMods.Any())
            {
                Console.WriteLine("Mods uploaded and updated:");
                foreach (var mod in downloadedMods)
                {
                    Console.WriteLine($"  - {mod}");
                }
            }
            else
            {
                Console.WriteLine("All mods are already up to date.");
            }
        }

        private static async Task DownloadModAsync(string url, string destinationPath, int maxRetries = 2)
        {
            using var client = new HttpClient();
            
            client.Timeout = TimeSpan.FromSeconds(100);
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var data = await client.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(destinationPath, data);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                        throw new Exception($"Loading error {url}: {ex.Message}");

                    Console.WriteLine($"[WARN] Loading error. Trying {attempt}/{maxRetries}...");
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


