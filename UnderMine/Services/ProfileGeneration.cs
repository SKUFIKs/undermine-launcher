using System;
using System.Text.Json;
using Avalonia.Controls.Shapes;
using System.IO;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace UnderMine.Services;

public class ProfileGeneration
{
    public async Task ProfileGenerate()
    {
        string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "versions", "TechnoMagic");
        Directory.CreateDirectory(folderPath);
        
        string filePath = Path.Combine(folderPath, "launcher_profiles.json");
        
        var profileJson = new
        {
            clientToken = Guid.NewGuid().ToString(),
            profiles = new
            {
                forge = new
                {
                    name = "forge",
                    type = "custom",
                    lastVersionId = "1.20.1-forge-47.4.10"
                }
            }
        };
        
        string jsonString = JsonSerializer.Serialize(profileJson, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, jsonString);
    }
}