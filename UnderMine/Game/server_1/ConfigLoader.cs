using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using UnderMine.Models;
using Path = System.IO.Path;
using ZipFile = System.IO.Compression.ZipFile;
using System.Security.Cryptography;
using System.Text;

namespace UnderMine.Game.server_1;

public class ConfigLoader
{
    private readonly ServerInfo _serverInfo;

    public ConfigLoader(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
    }
    

    public async Task GuiLoader()
    {
        //string url = "https://s1224sas.storage.yandex.net/rdisk/045f90a2b578da95607cb2338587da3289725070af9dd46cdddfdf986e1f9c5e/6908f4c1/2mbaMZk2p7t0945E3s1R5DCGk0TbmyLpez1VbmptjOKfmhm42oNAAzi_9smXewttDeC9GcBWv64w5u-FwLMGNA==?uid=0&filename=fancymenu.zip&disposition=attachment&hash=HcTL0eGRo7ppOqFczveYuRzVVSmt2Xgx/8MzWgOJeCb0W6cXYfL%2BqCrQ5Ua8P6Njq/J6bpmRyOJonT3VoXnDag%3D%3D&limit=0&content_type=application%2Fzip&owner_uid=1608853094&fsize=8256695&hid=0b6bfa465099c7842f5a5bb0166aee84&media_type=compressed&tknv=v3&ts=642b4e8a5f240&s=c067ddf307dd5df2747bd507c12fc76941b3428421a41577bc6bf06f5b730b46&pb=U2FsdGVkX1-cz1dy-ITv3VBY9Sq9qTAtF4A_WkMA4SCueWfyG35hQ2rR5mDuTZgdCMjKXzlHZtp9lf8PXnZXgYcMvBiW8FZraoP_sv8sZmw";
        string url = _serverInfo.GuiLink ?? throw new InvalidOperationException("No link");
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "versions", "TechnoMagic", "config", "fancymenu");
        string examination = Path.Combine(path, "examination.json");
        await DownloadAndExtractZip(url, path, examination);
    }

    public async Task QuestLoader()
    {
        //string url = "https://s77klg.storage.yandex.net/rdisk/79bdf87bbafaf27a1ea541a945e938f22aa84040c4c8548f4438e850e371bbae/6908f535/2mbaMZk2p7t0945E3s1R5KAG_gXkCR3ljzHwzzem5QER8S0QjxrtVj4sIR6kfEOPJU8Vxfo4xd_t9hmesjclnw==?uid=0&filename=ftbquests.zip&disposition=attachment&hash=jfAfAFOmoLrDmoXbgBJ5hzI5TU3hSPAzgDMRvd7Ka6EWZ7wmrlFjcZbt%2B1IXgzs2q/J6bpmRyOJonT3VoXnDag%3D%3D&limit=0&content_type=application%2Fzip&owner_uid=1608853094&fsize=338377&hid=49ecf66f0f4c03600fa8bb7e2a3d9429&media_type=compressed&tknv=v3&ts=642b4ef8ff740&s=1077399f09cde96861f993742881f7baddc55d0f9432eda8043f3d01813cb76d&pb=U2FsdGVkX1_fpOwZZic8v9X55OKT7smE4Yr89vnQRq2FSUFsr2bbm7JfpiiGuwvC8KKOboamWxGgtWAHmy4iQb0lZAD550U2iRkhNmB3_Yk";
        string url = _serverInfo.QuestLink ?? throw new InvalidOperationException("No link");
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "versions", "TechnoMagic", "config", "ftbquests");
        string examination = Path.Combine(path, "examination.json");
        await DownloadAndExtractZip(url, path, examination);
    }

    public async Task ShaderpacksLoader()
    {
        //string url = "https://s294klg.storage.yandex.net/rdisk/3c70b3d4c0bcb9065f4d87b34e21c28c95726ccdb853b341596c5482a0aaf15b/6908f55d/2mbaMZk2p7t0945E3s1R5K5oc3LlUQ3CQAXE7PSupneK_IrQ0SN5jQR-lioYO5VXJ-BpRO2PjYZ4JcMOPU_Srw==?uid=0&filename=shaderpacks.zip&disposition=attachment&hash=TNK4JZJOlq5Av%2BFcfo0IwSic8aiW7SuFnwBrkvdqSiT%2BDQNs%2Bln/KnF4cz5QbBd8q/J6bpmRyOJonT3VoXnDag%3D%3D&limit=0&content_type=application%2Fzip&owner_uid=1608853094&fsize=4607035&hid=70447b9d75685544252da4f558108e8c&media_type=compressed&tknv=v3&ts=642b4f1f25140&s=a5e0bb76e6478f4b31aeda65bf42730825c62e548e8f04145122ebaa100672ce&pb=U2FsdGVkX19SKrZOtIJ4A3d17LqdIrXqRPb9z7_YMylbc2sKO0zmQ9oW6qwF2yXbvc8t63NiiSnMHVI0_VSllcb9vRS1ztIhaPHOdS_bAjU";
        string url = _serverInfo.ShadersLink ?? throw new InvalidOperationException("No link");
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "versions", "TechnoMagic", "shaderpacks");
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
}