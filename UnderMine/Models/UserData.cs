using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace UnderMine.Models
{
    public class UserData
    {
        public bool SaveUser { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int MemorySettings { get; set; }
        public string GraphicSettings { get; set; } = null!;
        public bool EULAAccepted { get; set; } // Добавлено поле для EULA
        public DateTime EULAAcceptedDate { get; set; } // Дата принятия
        public string EULAVersion { get; set; } = "1.0"; // Версия соглашения

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

        // Метод для сохранения состояния EULA
        public async Task SaveEULAAcceptance(bool accepted)
        {
            this.EULAAccepted = accepted;
            this.EULAAcceptedDate = DateTime.Now;
            await this.Save();
            
            Console.WriteLine($"EULA acceptance saved: {accepted} at {EULAAcceptedDate}");
        }

        // Метод для проверки принятия EULA при загрузке
        public static async Task<bool> CheckEULAAccepted()
        {
            var userData = await Load();
            return userData?.EULAAccepted ?? false;
        }

        // Метод для принудительного сброса EULA (для тестирования)
        public static async Task ResetEULAAcceptance()
        {
            var userData = await Load();
            if (userData != null)
            {
                userData.EULAAccepted = false;
                userData.EULAAcceptedDate = DateTime.MinValue;
                await userData.Save();
            }
        }
        
        private void MemorySlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            // Логика изменения настроек памяти
        }
        
        public static async Task<UserData?> Load()
        {
            if (!File.Exists(SettingsPath))
            {
                var defaultSettings = new UserData
                {
                    SaveUser = false,
                    MemorySettings = 4096,
                    GraphicSettings = "Average",
                    EULAAccepted = false, // По умолчанию не принято
                    EULAAcceptedDate = DateTime.MinValue
                };

                await defaultSettings.Save();
                return defaultSettings;
            }

            try
            {
                string json = await File.ReadAllTextAsync(SettingsPath);
                return JsonSerializer.Deserialize<UserData>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                return new UserData
                {
                    SaveUser = false,
                    MemorySettings = 2048,
                    GraphicSettings = "Average",
                    EULAAccepted = false,
                    EULAAcceptedDate = DateTime.MinValue
                };
            }
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