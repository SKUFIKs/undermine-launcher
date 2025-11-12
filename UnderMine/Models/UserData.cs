using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using System.Runtime.InteropServices;

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

        private static readonly string SettingsPath = GetSettingsPath();

        private static string GetSettingsPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                    ?? throw new InvalidOperationException("Не удалось определить путь приложения"),
                    "settings.json"
                );
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string dir = Path.Combine(home, "Library", "Application Support", AppName);
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.json");
            }
            else
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dir = Path.Combine(home, ".config", AppName);
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.json");
            }
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
