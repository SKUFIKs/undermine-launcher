using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UnderMine.Services;

namespace UnderMine.Models
{
    public class ProfileManager
    {
        public bool SaveUser { get; set; } = false;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int MemorySettings { get; set; } = 6144;
        public string? JavaPath { get; set; }

        private static readonly string SettingsPath = GetSettingsPath();
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        private static string GetSettingsPath()
        {
            var pathService = new SystemService();
            string baseStorage = pathService.GetStorageDirectory()?.Value ?? AppDomain.CurrentDomain.BaseDirectory;
            string appDir = Path.Combine(baseStorage, "UnderMine");

            Directory.CreateDirectory(appDir);
            return Path.Combine(appDir, "settings.json");
        }

        public async Task Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, Options);
                await File.WriteAllTextAsync(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Save UserData: {ex.Message}");
            }
        }

        public static async Task<ProfileManager> Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    var defaultData = new ProfileManager();
                    await defaultData.Save();
                    return defaultData;
                }

                string json = await File.ReadAllTextAsync(SettingsPath);
                return JsonSerializer.Deserialize<ProfileManager>(json, Options) ?? new ProfileManager();
            }
            catch
            {
                return new ProfileManager();
            }
        }

        public static void ResetToDefault()
        {
            try
            {
                if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Reset UserData: {ex.Message}");
            }
        }
    }
}