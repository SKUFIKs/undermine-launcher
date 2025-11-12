using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnderMine.Models;
using Path = System.IO.Path;
using ZipFile = System.IO.Compression.ZipFile;

namespace UnderMine.Core.server_1
{
    public class ConfigLoader
    {
        private readonly ServerInfo _serverInfo;

        public ConfigLoader(ServerInfo serverInfo)
        {
            _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
        }

        public async Task ConfLoader()
        {
            string url = _serverInfo.ConfLink ?? throw new InvalidOperationException("No link");
            string path = Path.Combine(GetBaseDirectory(), "versions", "TechnoMagic", "config");
            string examination = Path.Combine(path, "examination.json");
            await DownloadAndExtractZip(url, path, examination);
        }

        public async Task ShaderpacksLoader()
        {
            string url = _serverInfo.ShadersLink ?? throw new InvalidOperationException("No link");
            string path = Path.Combine(GetBaseDirectory(), "versions", "TechnoMagic", "shaderpacks");
            string examination = Path.Combine(path, "examination.json");
            await DownloadAndExtractZip(url, path, examination);
        }

        public async Task ScriptsLoader()
        {
            string url = _serverInfo.KubejsLink ?? throw new InvalidOperationException("No link");
            string path = Path.Combine(GetBaseDirectory(), "versions", "TechnoMagic", "kubejs");
            string examination = Path.Combine(path, "examination.json");
            await DownloadAndExtractZip(url, path, examination);
        }

        private async Task DownloadAndExtractZip(string zipUrl, string destinationDir, string examinationFile)
        {
            string zipPath = Path.Combine(destinationDir, "temp.zip");

            if (File.Exists(examinationFile))
            {
                Console.WriteLine($"[{Path.GetFileName(destinationDir)}] уже установлен.");
                return;
            }

            Directory.CreateDirectory(destinationDir);

            try
            {
                Console.WriteLine($"[{Path.GetFileName(destinationDir)}] Загрузка архива...");

                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }

                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(destinationDir, entry.FullName);
                        string? directory = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);

                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        entry.ExtractToFile(destinationPath, true);
                    }
                }

                Console.WriteLine($"[{Path.GetFileName(destinationDir)}] Установка завершена!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{Path.GetFileName(destinationDir)}] Ошибка установки: {ex.Message}");
            }
            finally
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
        }

        private static string GetBaseDirectory()
        {
            const string AppName = "UnderMine";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return AppContext.BaseDirectory;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string dir = Path.Combine(home, "Library", "Application Support", AppName);
                Directory.CreateDirectory(dir);
                return dir;
            }
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dir = Path.Combine(home, ".config", AppName);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }
}
