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

    private async Task InstallGuiAsync(string temp,string fancyDirectory)
    {
        string arhive = temp;
        string path = fancyDirectory;
        bool overwriteFiles = true;
        ZipFile.ExtractToDirectory(arhive, path, overwriteFiles);
    }

    public async Task VerifyingInstallationAsync(string finalHach, string fancyDirectory, string temp)
    {
        CalculateFoldrerHash(fancyDirectory);
        Console.WriteLine(finalHach);
        string GuiHach = _serverInfo.GuiHach1;
        if (finalHach != null || GuiHach != null)
        {
            if (finalHach != GuiHach)
            {
                await GuiLoaderAsync(temp);
                
                if (!File.Exists(temp))
                {
                    Console.WriteLine($"Файл не загружен.");
                    return;
                }
                else
                {
                    await InstallGuiAsync(temp, fancyDirectory);
                    await AdditionalСheckAsync(finalHach,fancyDirectory);
                }
            }
        }
    }

    public async Task AdditionalСheckAsync(string finalHach, string fancyDirectory)
    {
        CalculateFoldrerHash(fancyDirectory);
        Console.WriteLine(finalHach); 
        string GuiHach = _serverInfo.GuiHach1;
        Console.WriteLine(GuiHach);
        if (finalHach != null || GuiHach != null)
        {
            if (finalHach == GuiHach)
            {
                Console.WriteLine("ГОЙДАGOOOL");
            }
            else
            {
                Console.WriteLine("HUITA PEREDELUVAJ");
            }
        }
    }

    public static string CalculateFoldrerHash(string fancyDirectory)
    {
        using (var sha256 = SHA256.Create())
        {
            return CalculateFolderHashRecursive(fancyDirectory, sha256);
        }
    }
    private static string CalculateFolderHashRecursive(string fancyDirectory, HashAlgorithm hashAlgorithm)
    {
        var sb = new StringBuilder();
        
        sb.Append(Path.GetFullPath(fancyDirectory));

        var files = Directory.GetFiles(fancyDirectory, "*", SearchOption.AllDirectories);
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