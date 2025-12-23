using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using UnderMine.Models;

namespace UnderMine.Core.Services
{
    public class ConfigService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseGamePath;

        public ConfigService(HttpClient httpClient, string baseGamePath)
        {
            _httpClient = httpClient;
            _baseGamePath = baseGamePath;
        }

        public async Task InstallAllConfigsAsync(ServerInfo info)
        {
            await InstallComponent(info.ConfLink, "config");
            await InstallComponent(info.ShadersLink, "shaderpacks");
            await InstallComponent(info.KubejsLink, "kubejs");
        }

        private async Task InstallComponent(string? url, string folderName)
        {
            if (string.IsNullOrEmpty(url)) return;

            string targetPath = Path.Combine(_baseGamePath, folderName);
            string examinationFile = Path.Combine(targetPath, "examination.json");

            if (File.Exists(examinationFile)) return;

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
            }
            finally
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }
    }
}
