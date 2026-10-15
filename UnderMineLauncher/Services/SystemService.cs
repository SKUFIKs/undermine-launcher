using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace UnderMine.Services
{
    public record AppPath(string Value);

    public class SliderValues
    {
        public double Step { get; set; }
        // Список строковых значений для меток на слайдере
        public List<string> Labels { get; set; } = new();

        public string FirstValue => Labels.ElementAtOrDefault(0) ?? "";
        public string SecondValue => Labels.ElementAtOrDefault(1) ?? "";
        public string ThirdValue => Labels.ElementAtOrDefault(2) ?? "";
        public string FourthValue => Labels.ElementAtOrDefault(3) ?? "";
        public string FifthValue => Labels.ElementAtOrDefault(4) ?? "";
    }

    public class SystemService
    {
        private const string AppName = "UnderMine";
        private readonly AppPath _storagePath;

        public SystemService()
        {
            _storagePath = InitializeStoragePath();
        }

        #region Public API

        public AppPath GetStorageDirectory() => _storagePath;

        /// <summary>
        /// Выполняет сканирование системы
        /// </summary>
        public SystemMetadata Scan()
        {
            return new SystemMetadata(
                OperatingSystem: RuntimeInformation.OSDescription,
                CpuInfo: GetCpuInfo(),
                TotalMemoryBytes: GetTotalMemory(),
                DeviceId: GetDeviceId(),
                Architecture: RuntimeInformation.OSArchitecture.ToString()
            );
        }

        /// <summary>
        /// Инициализирует конфигурацию слайдера памяти
        /// </summary>
        public async Task<SliderValues> InitializeMemorySliderAsync()
        {
            var metadata = Scan();
            double totalMemoryMb = metadata.totalMemoryBytes / 1024.0 / 1024.0;

            var min = 4096.0;
            var max = Math.Min(totalMemoryMb, 16384.0); // Ограничение в 16ГБ

            // Если памяти в системе меньше 4ГБ, подстраиваем минимум
            if (max < min) min = Math.Max(1024, max - 1024);

            int stepsCount = 5;
            double step = Math.Round((max - min) / (stepsCount - 1), 0);

            var result = new SliderValues { Step = step };

            for (int i = 0; i < stepsCount; i++)
            {
                double val = min + (step * i);
                result.Labels.Add(val.ToString(CultureInfo.InvariantCulture));
            }

            return await Task.FromResult(result);
        }

        #endregion

        #region Hardware Scan Logic

        private string GetDeviceId()
        {
            string rawData = $"{Environment.MachineName}-{Environment.ProcessorCount}-{RuntimeInformation.OSArchitecture}";
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private long GetTotalMemory()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (File.Exists("/proc/meminfo"))
                    {
                        var memTotalLine = File.ReadLines("/proc/meminfo").FirstOrDefault();
                        if (memTotalLine != null)
                        {
                            var parts = memTotalLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                                return kb * 1024;
                        }
                    }
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string? output = ExecuteCommand("sysctl", "hw.memsize");
                    if (output != null && output.Contains(':'))
                    {
                        string[] parts = output.Split(':');
                        if (long.TryParse(parts[1].Trim(), out long memBytes))
                            return memBytes;
                    }
                }
            }
            catch { /* Fallback */ }
            return 0;
        }

        private string GetCpuInfo()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"Hardware\Description\System\CentralProcessor\0");
                    return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown Windows CPU";
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return File.ReadLines("/proc/cpuinfo")
                               .FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                               ?.Split(':')
                               .Last()
                               .Trim() ?? "Unknown Linux CPU";
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return ExecuteCommand("sysctl", "-n machdep.cpu.brand_string") ?? "Unknown MacOS CPU";
                }
            }
            catch { return RuntimeInformation.OSArchitecture.ToString(); }
            return "Unknown CPU";
        }

        private string? ExecuteCommand(string fileName, string args)
        {
            try
            {
                var info = new ProcessStartInfo(fileName, args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(info);
                return process?.StandardOutput.ReadToEnd().Trim();
            }
            catch { return null; }
        }

        #endregion

        #region Path Logic

        private AppPath InitializeStoragePath()
        {
            string path;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", AppName);
            }
            else
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", AppName);
            }

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return new AppPath(path);
        }

        #endregion
    }
}