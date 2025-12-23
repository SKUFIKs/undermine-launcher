using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UnderMine.Services.Location;

namespace UnderMine.Core.Services
{
    public class ProfileService
    {
        public async Task GenerateLauncherProfileAsync(string targetFolder, string mcVersion, string forgeVersion)
        {
            var pathService = new PathService();
            
            string filePath = Path.Combine(targetFolder, "launcher_profiles.json");

            var profileJson = new
            {
                clientToken = Guid.NewGuid().ToString(),
                profiles = new
                {
                    forge = new
                    {
                        name = "forge",
                        type = "custom",
                        lastVersionId = $"{mcVersion}-forge-{forgeVersion}"
                    }
                }
            };

            string jsonString = JsonSerializer.Serialize(profileJson, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, jsonString);
        }
    }
}