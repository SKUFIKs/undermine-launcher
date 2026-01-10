using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnderMine.Services.Location;

namespace UnderMine.Services.Logger
{
    public class ProgressReporter
    {
        public event Action<string>? OnLog;
        private readonly string _logFilePath;
        private readonly TextWriter _originalConsoleOut;
        private const int MaxLogFiles = 5;

        private readonly StreamWriter _fileWriter;    
        private readonly object _lockObj = new();

        public ProgressReporter()
        {
            var pathService = new PathService();
            string baseStorage = pathService.GetStorageDirectory()?.Value ?? AppDomain.CurrentDomain.BaseDirectory;
            string logsDirectory = Path.Combine(baseStorage, "launcher-logs");

            Task.Run(() => CleanupOldLogs(logsDirectory));

            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
            _logFilePath = Path.Combine(logsDirectory, fileName);

            var fileStream = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _fileWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };

            _originalConsoleOut = Console.Out;

            var multiWriter = new MultiTextWriter(_originalConsoleOut, this);
            Console.SetOut(multiWriter);
            Console.SetError(multiWriter);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log($"[FATAL EXCEPTION] {e.ExceptionObject}", "CRITICAL");
                Flush(); 
            };

            Task.Run(WriteDetailedSystemInfo);
        }
        private void CleanupOldLogs(string directoryPath)
        {
            try
            {
                var directory = new DirectoryInfo(directoryPath);
                if (!directory.Exists) return;

                var files = directory.GetFiles("*.log")
                                     .OrderByDescending(f => f.LastWriteTime)
                                     .ToList();

                if (files.Count >= MaxLogFiles)
                {
                    var filesToDelete = files.Skip(MaxLogFiles); 
                    foreach (var file in filesToDelete)
                    {
                        try { file.Delete(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _originalConsoleOut.WriteLine($"[WARN] Failed to cleanup logs: {ex.Message}");
            }
        }
        public void Flush()
        {
            lock (_lockObj)
            {
                _fileWriter?.Flush();
            }
        }
        private async void WriteDetailedSystemInfo() // Добавили async
        {
            try
            {
                var userData = await UnderMine.Models.UserData.Load();
                
                string allocatedRamGb = userData != null
                    ? Math.Round(userData.MemorySettings / 1024.0, 2).ToString()
                    : "Unknown";

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("============================================");
                sb.AppendLine("             SYSTEM INFORMATION             ");
                sb.AppendLine("============================================");
                sb.AppendLine($"Timestamp:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"OS:             {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
                sb.AppendLine($"CPU:            {GetCpuName()}");
                sb.AppendLine($"RAM (Total):    {GetTotalMemoryGb()} GB"); 
                sb.AppendLine($"RAM allocated:  {allocatedRamGb} GB");    
                sb.AppendLine($"GPU:            {GetGpuName()}");
                sb.AppendLine("============================================");

                lock (_lockObj)
                {
                    _fileWriter.WriteLine(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Failed to collect system info: {ex.Message}");
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
        private class MultiTextWriter : TextWriter
        {
            private readonly TextWriter _baseWriter;
            private readonly ProgressReporter _reporter;
            public override Encoding Encoding => _baseWriter.Encoding;

            public MultiTextWriter(TextWriter baseWriter, ProgressReporter reporter)
            {
                _baseWriter = baseWriter;
                _reporter = reporter;
            }
            public override void WriteLine(string? value)
            {
                if (value != null) _reporter.Log(value);
            }

            public override void WriteLine(object? value) => WriteLine(value?.ToString());

            public override void Write(string? value)
            {

                if (!string.IsNullOrWhiteSpace(value))
                    _reporter.Log(value, "RAW");
            }
        }

        public void Log(string message, string level = "INFO")
        {
            if (string.IsNullOrEmpty(message)) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string fullMessage = message.StartsWith("[")
                ? message
                : $"[{timestamp}] [{level}] {message}";

            lock (_lockObj)
            {
                _originalConsoleOut.WriteLine(fullMessage);

                try
                {
                    _fileWriter.WriteLine(fullMessage);
                }
                catch { /* Игнорируем ошибки записи, чтобы не уронить приложение */ }
            }

            OnLog?.Invoke(fullMessage);
        }
        public void Dispose()
        {
            lock (_lockObj)
            {
                _fileWriter?.Dispose();
            }
            Console.SetOut(_originalConsoleOut); 
        }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);
    }
}