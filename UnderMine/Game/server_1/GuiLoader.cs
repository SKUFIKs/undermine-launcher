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
    
    public GuiLoader(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
    }
    public async Task GuiLoaderAsync(string temp)
    {
        string link = _serverInfo.GuiLink1;
        string path = Path.Combine(temp);
        
        if (string.IsNullOrWhiteSpace(link))
            throw new Exception("GuiLink1 not found");
        using HttpClient client = new HttpClient();
        
        using HttpResponseMessage response = await client.GetAsync(link, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await stream.CopyToAsync(fileStream);
    }

    private Task InstallGuiAsync(string temp,string configDir)
    {
        string arhive = temp;
        string path = configDir;
        bool overwriteFiles = true;
        ZipFile.ExtractToDirectory(arhive, path, overwriteFiles);

        return Task.CompletedTask;
    }

    public async Task VerifyingInstallationAsync(string currentHash, string configDir, string temp)
    {
        string guiHash = _serverInfo.GuiHach1;
        if (!string.IsNullOrEmpty(currentHash) && !string.IsNullOrEmpty(guiHash))
        {
            if (currentHash != guiHash)
            {
                await GuiLoaderAsync(temp);

                if (!File.Exists(temp))
                {
                    Console.WriteLine($"Файл GUI не загружен.");
                }
                else
                {
                    await InstallGuiAsync(temp, configDir);
                    await AdditionalСheckAsync(GuiLoader.CalculateFoldrerHash(configDir), configDir);
                }
            }
        }
    }
    public Task AdditionalСheckAsync(string finalHach, string configDir)
    {
        CalculateFoldrerHash(configDir);
        Console.WriteLine(finalHach); 
        string guiHach = _serverInfo.GuiHach1;
        Console.WriteLine(guiHach);
        if (!string.IsNullOrEmpty(finalHach) && !string.IsNullOrEmpty(guiHach))
        {
            if (string.IsNullOrEmpty(finalHach) == string.IsNullOrEmpty(guiHach))
            {
                Console.WriteLine("ГОЙДАGOOOL");
            }
            else
            {
                Console.WriteLine("HUITA PEREDELUVAJ");
            }
        }

        return Task.CompletedTask;
    }

    public static string CalculateFoldrerHash(string configDir)
    {
        using (var sha256 = SHA256.Create())
        {
            return CalculateFolderHashRecursive(configDir, sha256);
        }
    }
    private static string CalculateFolderHashRecursive(string configDir, HashAlgorithm hashAlgorithm)
    {
        var sb = new StringBuilder();
        
        sb.Append(Path.GetFullPath(configDir));

        var files = Directory.GetFiles(configDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            using (var fileStream = new FileStream(file, FileMode.Open))
            {
                var hash = hashAlgorithm.ComputeHash(fileStream);
                sb.Append(BitConverter.ToString(hash));
            }
        }

        var finalHash = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return BitConverter.ToString(finalHash).Replace("-", "");
    }
}