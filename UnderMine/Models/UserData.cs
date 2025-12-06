using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace UnderMine.Models
{
    public class UserData
    {
        public bool SaveUser { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int MemorySettings { get; set; }
        public string GraphicSettings { get; set; } = "Average";

        private const string AppName = "UnderMine";
        [UnconditionalSuppressMessage("SingleFile", "IL3002:Avoid calling members marked with 'RequiresAssemblyFilesAttribute' when publishing as a single-file", Justification = "<Pending>")]
        private static readonly string SettingsPath = GetSettingsPath();

        [RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")] 
        private static string GetSettingsPath()
        {
            string basePath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support");
            }
            else
            {
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            string appDir = Path.Combine(basePath, AppName);
  
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }

            return Path.Combine(appDir, "settings.json");
        }

        public async Task Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                await File.WriteAllTextAsync(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении настроек: {ex.Message}");
            }
        }

        private void MemorySlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            // обработка ползунка памяти (если нужно)
        }

        public static async Task<UserData?> Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    var defaultSettings = new UserData
                    {
                        SaveUser = false,
                        MemorySettings = 6144,
                        GraphicSettings = "Average"
                    };
                    await defaultSettings.Save();
                    return defaultSettings;
                }

                string json = await File.ReadAllTextAsync(SettingsPath);
                return JsonSerializer.Deserialize<UserData>(json) ?? new UserData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке настроек: {ex.Message}");
                return new UserData
                {
                    SaveUser = false,
                    MemorySettings = 6144,
                    GraphicSettings = "Average"
                };
            }
        }

        public static void LoadDefault()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    File.Delete(SettingsPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сбросе настроек: {ex.Message}");
            }
        }
    }
}
