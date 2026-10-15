using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace UnderMine.Services
{
    public class ProgressService : IDisposable
    {
        public event Action<string>? OnLog;
        public event Action<string, double, string>? OnProgressUpdate;

        private const int MaxLogFiles = 5;
        private readonly TimeSpan _smoothDuration = TimeSpan.FromSeconds(0.5);
        private readonly double _updateIntervalMs = 20;

        private readonly List<string> _logHistory = new();
        private const int MaxHistoryItems = 50;

        private readonly string _logFilePath;
        private readonly object _syncLock = new();
        private readonly SystemService _scanner;
        private readonly ILogger<ProgressService> _logger;

        private readonly Dictionary<string, (double Progress, double Weight)> _stages = new();
        private double _currentDisplayedProgress = 0;

        public ProgressService(ILogger<ProgressService> logger)
        {
            _logger = logger;
            _scanner = new SystemService();
            var pathService = new SystemService();
            var metadata = _scanner.Scan();

            string baseStorage = pathService.GetStorageDirectory()?.Value ?? AppDomain.CurrentDomain.BaseDirectory;
            string logsDirectory = Path.Combine(baseStorage, "launcher-logs");

            if (!Directory.Exists(logsDirectory)) Directory.CreateDirectory(logsDirectory);
            Task.Run(() => CleanupOldLogs(logsDirectory));

            string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
            _logFilePath = Path.Combine(logsDirectory, fileName);
            var fileStream = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var fileWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log($"[FATAL EXCEPTION] {e.ExceptionObject}", "CRITICAL");
                Flush();
            };

            Task.Run(() => WriteDetailedSystemInfo(metadata));
        }

        public void Log(string message, string level = "INFO")
        {
            if (string.IsNullOrEmpty(message)) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string fullMessage = message.Contains("[") && message.Contains("]")
                ? message
                : $"[{timestamp}] [{level}] {message}";

            lock (_syncLock)
            {
                _logger.LogInformation(fullMessage);

                _logHistory.Add(fullMessage);
                if (_logHistory.Count > MaxHistoryItems) _logHistory.RemoveAt(0);
            }

            OnLog?.Invoke(fullMessage);
        }

        public void LogException(Exception ex, string context)
        {
            string message = ex switch
            {
                HttpRequestException => $"Network error: {ex.Message}",
                TaskCanceledException => "Operation timed out",
                _ => $"Critical error: {ex.Message}"
            };
            Log($"[{context}] {message}", "ERROR");
        }

        public static string GetHttpStatusMessage(HttpStatusCode status) => status switch
        {
            HttpStatusCode.BadRequest => "Bad request (400)",
            HttpStatusCode.Unauthorized => "Authorization error (401)",
            HttpStatusCode.Forbidden => "Access denied (403)",
            HttpStatusCode.NotFound => "Service not found (404)",
            HttpStatusCode.Conflict => "User already exists (409)",
            HttpStatusCode.Gone => "Account banned or deleted (410)",
            HttpStatusCode.TooManyRequests => "Too many attempts (429)",
            HttpStatusCode.InternalServerError => "Server error (500)",
            _ => $"Error {(int)status}"
        };

        private void WriteDetailedSystemInfo(SystemMetadata meta)
        {
            try
            {
                double totalRamGb = Math.Round(meta.totalMemoryBytes / 1073741824.0, 2);
                var sb = new StringBuilder();
                sb.AppendLine("============================================");
                sb.AppendLine("               SYSTEM INFORMATION           ");
                sb.AppendLine("============================================");
                sb.AppendLine($"OS:      {meta.operatingSystem}");
                sb.AppendLine($"Arch:    {meta.architecture}");
                sb.AppendLine($"CPU:     {meta.cpuInfo}");
                sb.AppendLine($"RAM:     {totalRamGb} GB");
                sb.AppendLine("============================================");

                lock (_syncLock) { _logger.LogInformation(sb.ToString()); }
            }
            catch (Exception ex) { _logger.LogError(ex, "System info failed"); }
        }

        private void CleanupOldLogs(string directoryPath)
        {
            try
            {
                var directory = new DirectoryInfo(directoryPath);
                if (!directory.Exists) return;

                var files = directory.GetFiles("*.log")
                    .OrderByDescending(f => f.LastWriteTime)
                    .Skip(MaxLogFiles);

                foreach (var file in files) try { file.Delete(); } catch { }
            }
            catch { /* Ignore */ }
        }

        public void Flush() { lock (_syncLock) { } }

        public void RegisterStage(string stageName, double weight)
        {
            lock (_syncLock) { _stages[stageName] = (0, weight); }
        }

        public async Task ReportAsync(string stageName, double stageProgress, string message, bool logThis = false)
        {
            if (logThis) Log(message, "PROGRESS");

            double targetGlobalProgress;
            lock (_syncLock)
            {
                if (_stages.ContainsKey(stageName))
                {
                    _stages[stageName] = (Math.Clamp(stageProgress, 0, 1), _stages[stageName].Weight);
                }

                double totalWeight = _stages.Values.Sum(x => x.Weight);
                if (totalWeight <= 0) return;

                double currentWeightedSum = _stages.Values.Sum(x => x.Progress * x.Weight);
                targetGlobalProgress = (currentWeightedSum / totalWeight) * 100;
            }

            await UpdateSmoothly(message, targetGlobalProgress, stageName);
        }

        private async Task UpdateSmoothly(string message, double targetProgress, string tag)
        {
            double startProgress;
            lock (_syncLock)
            {
                startProgress = _currentDisplayedProgress;
                if (targetProgress < startProgress || Math.Abs(targetProgress - startProgress) < 0.01)
                {
                    _currentDisplayedProgress = targetProgress;
                    NotifyUI(message, targetProgress, tag);
                    return;
                }
            }

            int steps = (int)(_smoothDuration.TotalMilliseconds / _updateIntervalMs);
            double delta = targetProgress - startProgress;

            for (int i = 1; i <= steps; i++)
            {
                double newProgress = startProgress + delta * ((double)i / steps);
                lock (_syncLock) { _currentDisplayedProgress = newProgress; }
                NotifyUI(message, newProgress, tag);
                await Task.Delay((int)_updateIntervalMs);
            }
        }

        private void NotifyUI(string message, double value, string tag)
        {
            Dispatcher.UIThread.Post(() => OnProgressUpdate?.Invoke(message, value, tag));
        }

        public void Reset()
        {
            lock (_syncLock)
            {
                _currentDisplayedProgress = 0;
                var keys = _stages.Keys.ToList();
                foreach (var key in keys) _stages[key] = (0, _stages[key].Weight);
            }
            NotifyUI("Ready", 0, "System");
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
