using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using UnderMine.Models;
using System.Text.Json;

namespace UnderMine.Core.Services;

public class ExaminationData
{
    public string Version { get; set; } = "0.0";
}

public class ConfigService
{
    private readonly HttpClient _httpClient;

    public ConfigService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task InstallAllConfigsAsync(ServerInfo info, string gamePath)
    {
        
        string targetVersion = !string.IsNullOrEmpty(info.uiVersion) ? info.uiVersion : (info.version ?? "1.0");

        Console.WriteLine($"[ConfigService] Начало установки конфигов в: {gamePath}");

        await InstallComponent(info.ConfLink, "config", targetVersion, gamePath);
        await InstallComponent(info.ShadersLink, "shaderpacks", targetVersion, gamePath);
        await InstallComponent(info.KubejsLink, "kubejs", targetVersion, gamePath);
    }

    private async Task InstallComponent(string? url, string folderName, string targetVersion, string gamePath)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Console.WriteLine($"[ConfigService] Пропуск {folderName} (ссылка пуста)");
            return;
        }

        string targetPath = Path.Combine(gamePath, folderName);
        string examinationFile = Path.Combine(targetPath, "examination.json");

        // Проверка версии
        if (File.Exists(examinationFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(examinationFile);
                var data = JsonSerializer.Deserialize<ExaminationData>(json);
                if (data?.Version == targetVersion)
                {
                    Console.WriteLine($"[ConfigService] {folderName} уже актуален (v{targetVersion})");
                    return;
                }
            }
            catch { }

            Console.WriteLine($"[ConfigService] Обновление {folderName}...");
            DeleteDirectoryContent(targetPath);
        }

        if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
        string zipPath = Path.Combine(targetPath, "download.zip");

        try
        {
            Console.WriteLine($"[ConfigService] Загрузка {folderName}...");
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using (var fs = new FileStream(zipPath, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }

            Console.WriteLine($"[ConfigService] Распаковка {folderName}...");
            ZipFile.ExtractToDirectory(zipPath, targetPath, true);

            var newData = new ExaminationData { Version = targetVersion };
            await File.WriteAllTextAsync(examinationFile, JsonSerializer.Serialize(newData));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR ConfigService] Ошибка в {folderName}: {ex.Message}");
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
        foreach (FileInfo file in di.GetFiles()) try { file.Delete(); } catch { }
        foreach (DirectoryInfo dir in di.GetDirectories()) try { dir.Delete(true); } catch { }
    }
}