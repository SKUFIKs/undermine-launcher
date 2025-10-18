using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnderMine.Models;
using Path = System.IO.Path;
using ZipFile = System.IO.Compression.ZipFile;
using System.Security.Cryptography;
using System.Text;

namespace UnderMine.Game.server_1;

public class GuiLoader
{
    private readonly ServerInfo _serverInfo;
    private static readonly HttpClient _httpClient = new HttpClient();
    
    public GuiLoader(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
    }

    public async Task DownloadGuiAsync(string tempPath)
    {
        string link = _serverInfo.ConfLink1;
        Console.WriteLine($"[GUI] Загрузка конфига: {link}");
        
        if (string.IsNullOrWhiteSpace(link))
            throw new Exception("ConfLink1 not found");
        
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
        
        using HttpResponseMessage response = await _httpClient.GetAsync(link, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await stream.CopyToAsync(fileStream);
        
        Console.WriteLine($"[GUI] Конфиг загружен: {tempPath}");
    }

    private Task InstallGuiAsync(string archivePath, string configDir)
    {
        Console.WriteLine($"[GUI] Установка конфига в: {configDir}");

        if (Directory.Exists(configDir))
        {
            Directory.Delete(configDir, true);
        }
        Directory.CreateDirectory(configDir);
        
        ZipFile.ExtractToDirectory(archivePath, configDir, overwriteFiles: true);
        Console.WriteLine($"[GUI] Конфиг установлен");
        
        return Task.CompletedTask;
    }

    public async Task VerifyingInstallationAsync(string currentHash, string configDir, string tempPath)
    {
        string guiHash = _serverInfo.GuiHach1;
        
        // Если хеш не указан - пропускаем проверку
        if (string.IsNullOrEmpty(guiHash))
        {
            Console.WriteLine("[GUI] Хеш не указан, пропускаем проверку");
            return;
        }

        bool needsUpdate = string.IsNullOrEmpty(currentHash) || currentHash != guiHash;
        
        if (!needsUpdate)
        {
            Console.WriteLine("[GUI] Конфиг актуален");
            return;
        }

        Console.WriteLine("[GUI] Конфиг требует обновления");
        
        try
        {
            await DownloadGuiAsync(tempPath);

            if (!File.Exists(tempPath))
            {
                Console.WriteLine("[GUI] Файл не загружен");
                return;
            }

            await InstallGuiAsync(tempPath, configDir);

            string newHash = CalculateFolderHash(configDir);
            await CheckInstallation(newHash);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GUI] Ошибка: {ex.Message}");
        }
    }

    public Task CheckInstallation(string finalHash)
    {
        string guiHash = _serverInfo.GuiHach1;
        
        if (!string.IsNullOrEmpty(finalHash) && !string.IsNullOrEmpty(guiHash))
        {
            if (finalHash == guiHash)
            {
                Console.WriteLine("[GUI] Конфиг проверен - OK");
            }
            else
            {
                Console.WriteLine("[GUI] Конфиг поврежден");
            }
        }

        return Task.CompletedTask;
    }

    public static string CalculateFolderHash(string configDir)
    {
        if (!Directory.Exists(configDir))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var files = Directory.GetFiles(configDir, "*", SearchOption.AllDirectories);
        
        if (files.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        
        foreach (var file in files)
        {
            using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            var hash = sha256.ComputeHash(fileStream);
            sb.Append(BitConverter.ToString(hash));
        }

        var finalHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return BitConverter.ToString(finalHash).Replace("-", "");
    }
}