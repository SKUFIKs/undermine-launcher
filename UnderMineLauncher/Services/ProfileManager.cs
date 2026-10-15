using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnderMine.Services;

namespace UnderMine.Models
{
    public class ProfileManager
    {
        public bool SaveUser { get; set; } = false;
        public string? Username { get; set; }
        public int MemorySettings { get; set; } = 6144;
        public string? JavaPath { get; set; }

        /// <summary>
        /// Root folder under which game instances are installed
        /// (&lt;root&gt;/instances/&lt;ServerName&gt;). Null means the default storage directory.
        /// </summary>
        public string? InstallationPath { get; set; }

        private static readonly string SettingsPath = GetSettingsPath();
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        // Encryption key and IV (in production, these should be derived from a user-specific secret)
        private static readonly byte[] EncryptionKey = DeriveKey("UnderMineLauncher_SecretKey_2024");
        private static readonly byte[] EncryptionIV = new byte[16] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F };

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

        /// <summary>
        /// Encrypts a string using AES encryption
        /// </summary>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.IV = EncryptionIV;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts a string using AES encryption
        /// </summary>
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            try
            {
                using var aes = Aes.Create();
                aes.Key = EncryptionKey;
                aes.IV = EncryptionIV;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Derives a 256-bit key from a password using SHA256
        /// </summary>
        private static byte[] DeriveKey(string password)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }
}
