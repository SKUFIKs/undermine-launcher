using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Path = System.IO.Path;

namespace UnderMine.Core.server_1
{
    public class ProfileGeneration
    {
        public async Task ProfileGenerate()
        {
            string folderPath = Path.Combine(GetBaseDirectory(), "versions", "TechnoMagic");
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

        private static string GetBaseDirectory()
        {
            const string AppName = "UnderMine";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
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
