using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using UnderMine.Models;
using System.Text.Json;

namespace UnderMine.Core.Services
{
    public class ConfigService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseGamePath;
        public class ExaminationData
        {
            public string Version { get; set; } = "0.0";
        }
        public ConfigService(HttpClient httpClient, string baseGamePath)
        {
            _httpClient = httpClient;
            _baseGamePath = baseGamePath;
        }

        public async Task InstallAllConfigsAsync(ServerInfo info)
        {
            string targetVersion =  info.uiVersion;

            await InstallComponent(info.ConfLink, "config", targetVersion);
            await InstallComponent(info.ShadersLink, "shaderpacks", targetVersion);
            await InstallComponent(info.KubejsLink, "kubejs", targetVersion);
        }

        private async Task InstallComponent(string? url, string folderName, string targetVersion)
        {
            if (string.IsNullOrEmpty(url)) return;

            string targetPath = Path.Combine(_baseGamePath, folderName);
            string examinationFile = Path.Combine(targetPath, "examination.json");

            if (File.Exists(examinationFile))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(examinationFile);
                    var data = JsonSerializer.Deserialize<ExaminationData>(json);

                    if (data?.Version == targetVersion)
                    {
                        return;
                    }
                }
                catch
                {
                    // Если файл поврежден, считаем, что версии не совпадают
                }
                DeleteDirectoryContent(targetPath);
            }

            Directory.CreateDirectory(targetPath);
            string zipPath = Path.Combine(targetPath, "temp.zip");

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                await using (var fs = new FileStream(zipPath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }

                ZipFile.ExtractToDirectory(zipPath, targetPath, true);

                var newData = new ExaminationData { Version = targetVersion };
                string newJson = JsonSerializer.Serialize(newData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(examinationFile, newJson);
            }
            finally
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }
        private void DeleteDirectoryContent(string path)
        {
            if (!Directory.Exists(path)) return;

            DirectoryInfo di = new DirectoryInfo(path);

            foreach (FileInfo file in di.GetFiles()) file.Delete();
            foreach (DirectoryInfo dir in di.GetDirectories()) dir.Delete(true);
        }
    }
}
