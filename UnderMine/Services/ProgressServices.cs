using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace UnderMine.Services
{
    public class ProgressService : IDisposable
    {
        // --- События ---
        public event Action<string>? OnLog;
        public event Action<string, double, string>? OnProgressUpdate;

        // --- Константы и Настройки ---
        private const int MaxLogFiles = 5;
        private readonly TimeSpan _smoothDuration = TimeSpan.FromSeconds(0.5);
        private readonly double _updateIntervalMs = 20;

        // --- Поля логирования ---
        private readonly string _logFilePath;
        private readonly TextWriter _originalConsoleOut;
        private readonly StreamWriter _fileWriter;
        private readonly object _syncLock = new();
        private readonly SystemService _scanner;

        // --- Поля управления прогрессом ---
        private readonly Dictionary<string, (double Progress, double Weight)> _stages = new();
        private double _currentDisplayedProgress = 0;

        public ProgressService(Action<string, double, string>? onUpdate = null)
        {
            if (onUpdate != null) OnProgressUpdate += onUpdate;

            _scanner = new SystemService();
            var pathService = new SystemService();
            var metadata = _scanner.Scan();

            // 1. Настройка путей и очистка старых логов
            string baseStorage = pathService.GetStorageDirectory()?.Value ?? AppDomain.CurrentDomain.BaseDirectory;
            string logsDirectory = Path.Combine(baseStorage, "launcher-logs");

            if (!Directory.Exists(logsDirectory)) Directory.CreateDirectory(logsDirectory);
            Task.Run(() => CleanupOldLogs(logsDirectory));

            // 2. Инициализация Writer-а
            string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
            _logFilePath = Path.Combine(logsDirectory, fileName);
            var fileStream = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _fileWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };

            // 3. Перехват консоли
            _originalConsoleOut = Console.Out;
            var multiWriter = new MultiTextWriter(_originalConsoleOut, this);
            Console.SetOut(multiWriter);
            Console.SetError(multiWriter);

            // 4. Глобальные исключения
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log($"[FATAL EXCEPTION] {e.ExceptionObject}", "CRITICAL");
                Flush();
            };

            Task.Run(() => WriteDetailedSystemInfo(metadata));
        }

        #region Логирование (ProgressReporter)

        public void Log(string message, string level = "INFO")
        {
            if (string.IsNullOrEmpty(message)) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string fullMessage = message.StartsWith("[")
                ? message
                : $"[{timestamp}] [{level}] {message}";

            lock (_syncLock)
            {
                _originalConsoleOut.WriteLine(fullMessage);
                try { _fileWriter.WriteLine(fullMessage); } catch { }
            }

            OnLog?.Invoke(fullMessage);
        }

        public void LogException(Exception ex, string context)
        {
            string message = ex switch
            {
                HttpRequestException => $"Ошибка сети: {ex.Message}",
                TaskCanceledException => "Превышено время ожидания операции",
                _ => $"Критическая ошибка: {ex.Message}"
            };
            Log($"[{context}] {message}", "ERROR");
        }

        public static string GetHttpStatusMessage(HttpStatusCode status) => status switch
        {
            HttpStatusCode.BadRequest => "Неверный запрос (400)",
            HttpStatusCode.Unauthorized => "Ошибка авторизации (401)",
            HttpStatusCode.Forbidden => "Доступ запрещён (403)",
            HttpStatusCode.NotFound => "Сервис не найден (404)",
            HttpStatusCode.Conflict => "Такой пользователь уже существует (409)",
            HttpStatusCode.Gone => "Аккаунт забанен или удален (410)",
            HttpStatusCode.TooManyRequests => "Слишком много попыток (429)",
            HttpStatusCode.InternalServerError => "Ошибка сервера (500)",
            _ => $"Ошибка {(int)status}"
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

                lock (_syncLock) { _fileWriter.WriteLine(sb.ToString()); }
            }
            catch (Exception ex) { _originalConsoleOut.WriteLine($"[ERROR] System info failed: {ex.Message}"); }
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

        public void Flush() { lock (_syncLock) { _fileWriter?.Flush(); } }

        #endregion

        #region Управление прогрессом (ProgressManager)

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

        #endregion

        #region Вспомогательные классы

        private class MultiTextWriter : TextWriter
        {
            private readonly TextWriter _baseWriter;
            private readonly ProgressService _service;
            public override Encoding Encoding => _baseWriter.Encoding;

            public MultiTextWriter(TextWriter baseWriter, ProgressService service)
            {
                _baseWriter = baseWriter;
                _service = service;
            }

            public override void WriteLine(string? value) { if (value != null) _service.Log(value); }
            public override void WriteLine(object? value) => WriteLine(value?.ToString());
            public override void Write(string? value) { if (!string.IsNullOrWhiteSpace(value)) _service.Log(value, "RAW"); }
        }

        public void Dispose()
        {
            lock (_syncLock) { _fileWriter?.Dispose(); }
            Console.SetOut(_originalConsoleOut);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}