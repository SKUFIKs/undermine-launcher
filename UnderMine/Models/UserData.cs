using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace UnderMine.Models
{
    public class UserData
    {
        public bool SaveUser {get; set;}
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int MemorySettings { get; set; }
        public string GraphicSettings { get; set; } = null!;

        private static readonly string SettingsPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException(),
            "settings.json"
        );
        
        public async Task Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            await File.WriteAllTextAsync(SettingsPath, json);
        }
        
        public static async Task<UserData?> Load()
        {
            if (!File.Exists(SettingsPath))
            {
                var defaultSettings = new UserData
                {
                    SaveUser = false,
                    MemorySettings = 2048,
                    GraphicSettings = "Average"
                };

                await defaultSettings.Save();
                return defaultSettings;
            }

            string json = await File.ReadAllTextAsync(SettingsPath);
            return JsonSerializer.Deserialize<UserData>(json);
        }

        public static void LoadDefault()
        {
            if (File.Exists(SettingsPath))
            {
                File.Delete(SettingsPath);
            }
        }
        
    }
}