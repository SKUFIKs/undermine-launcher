using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using UnderMine.Services.Location;
using Avalonia.Utilities;

namespace UnderMine.Core.Services
{
    public class ProgressReporter
    {
        public event Action<string>? OnLog;
        private readonly string _logFilePath;

        public ProgressReporter()
        {
            var pathService = new PathService();
            string baseStorage = pathService.GetStorageDirectory().Value;
            string logsDirectory = Path.Combine(baseStorage, "launcher-logs");

            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
            _logFilePath = Path.Combine(logsDirectory, fileName);

            WriteDetailedSystemInfo();
        }

        private void WriteDetailedSystemInfo()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("============================================");
                sb.AppendLine("      DETAILED SYSTEM INFORMATION           ");
                sb.AppendLine("============================================");
                sb.AppendLine($"Timestamp:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"OS:             {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
                sb.AppendLine($"Framework:      {RuntimeInformation.FrameworkDescription}");
                sb.AppendLine($"Machine Name:   {Environment.MachineName}");
                sb.AppendLine($"User:           {Environment.UserName}");

                sb.AppendLine($"CPU Model:      {GetCpuName()}");
                sb.AppendLine($"Cores Count:    {Environment.ProcessorCount}");

                sb.AppendLine($"Total RAM:      {GetTotalMemoryGb()} GB");

                sb.AppendLine($"GPU (Primary):  {GetGpuName()}");

                sb.AppendLine("============================================");
                sb.AppendLine();

                File.AppendAllText(_logFilePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to collect system info: {ex.Message}");
            }
        }

        private string GetCpuName()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "Unknown CPU")?.ToString() ?? "Unknown";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    foreach (var line in File.ReadAllLines("/proc/cpuinfo"))
                    {
                        if (line.StartsWith("model name")) return line.Split(':')[1].Trim();
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return ExecuteCommand("sysctl", "-n machdep.cpu.brand_string");
                }
            }
            catch { }
            return "Generic CPU";
        }

        private string GetGpuName()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return ExecuteCommand("wmic", "path win32_VideoController get name").Replace("Name", "").Trim();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return ExecuteCommand("sh", "-c \"lspci | grep -E 'VGA|3D'\"").Trim();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return ExecuteCommand("sh", "-c \"system_profiler SPDisplaysDataType | grep Chipset\"");
                }
            }
            catch { }
            return "Unknown GPU";
        }

        private string GetTotalMemoryGb()
        {
            try
            {
                double totalBytes = 0;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    NativeMethods.GetPhysicallyInstalledSystemMemory(out long memKb);
                    totalBytes = memKb * 1024.0;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string memInfo = File.ReadAllLines("/proc/meminfo")[0]; 
                    string[] parts = memInfo.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    totalBytes = double.Parse(parts[1]) * 1024.0;
                }
                else
                {
                    totalBytes = (double)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                }

                return Math.Round(totalBytes / (1024 * 1024 * 1024), 2).ToString();
            }
            catch { return "Unknown"; }
        }

        private string ExecuteCommand(string fileName, string args)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(startInfo);
                using var reader = process?.StandardOutput;
                return reader?.ReadToEnd() ?? "";
            }
            catch { return "Unknown"; }
        }

        public void Log(string message, string level = "INFO")
        {
            string timeStamp = DateTime.Now.ToString("HH:mm:ss");
            string fullMessage = $"[{timeStamp}] [{level}] {message}";

            OnLog?.Invoke(fullMessage);
            Console.WriteLine(fullMessage);

            try
            {
                File.AppendAllText(_logFilePath, fullMessage + Environment.NewLine);
            }
            catch { }
        }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);
    }
}