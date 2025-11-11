using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
namespace UnderMine.Services;

public static class SystemInformation
    {
        private static readonly string DeviceIdFilePath = GetDeviceIdPath();

        
        public static string GetStableDeviceId()
        {
            string stored = GetOrCreateStoredId();
            string hwid = GetHardwareFingerprint();

            using var sha = SHA256.Create();
            var combined = $"{stored}-{hwid}";
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        
        private static string GetHardwareFingerprint()
        {
            string raw = $"{Environment.MachineName}-{Environment.OSVersion}-{RuntimeInformation.OSArchitecture}";

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        
        private static string GetOrCreateStoredId()
        {
            try
            {
                if (File.Exists(DeviceIdFilePath))
                    return File.ReadAllText(DeviceIdFilePath).Trim();

                var id = Guid.NewGuid().ToString("N");
                var directory = Path.GetDirectoryName(DeviceIdFilePath);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(DeviceIdFilePath, id);
                return id;
            }
            catch
            {
                return Guid.NewGuid().ToString("N");
            }
        }
        
        private static string GetDeviceIdPath()
        {
            string basePath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnderMine");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "UnderMine");
            else 
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnderMine");

            return Path.Combine(basePath, "device.id");
        }
    }