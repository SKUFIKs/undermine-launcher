using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace UnderMine.Services.ProgressBar
{
    public class SmoothProgressUpdater
    {
        private readonly Action<string, double, string> _onProgress;
        private readonly TimeSpan _duration = TimeSpan.FromSeconds(0.5);
        private readonly double _updateIntervalMs = 20;

        public double CurrentValue
        {
            get { lock (_lock) return _currentDisplayedProgress; }
        }

        private double _currentDisplayedProgress = 0;
        private readonly object _lock = new object();

        public SmoothProgressUpdater(Action<string, double, string> onProgress)
        {
            _onProgress = onProgress;
        }

        public async Task UpdateSmoothly(string message, double targetProgress, string tag)
        {
            double startProgress;
            lock (_lock)
            {
                startProgress = _currentDisplayedProgress;

                if (targetProgress < startProgress)
                {
                    _currentDisplayedProgress = targetProgress;
                    _onProgress?.Invoke(message, targetProgress, tag);
                    return;
                }

                if (Math.Abs(targetProgress - startProgress) < 0.01) return;
            }

            long steps = (long)(_duration.TotalMilliseconds / _updateIntervalMs);
            double delta = targetProgress - startProgress;

            for (int i = 1; i <= steps; i++)
            {
                double newProgress = startProgress + delta * ((double)i / steps);

                lock (_lock)
                {
                    _currentDisplayedProgress = newProgress;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    _onProgress?.Invoke(message, newProgress, tag);
                });

                await Task.Delay((int)_updateIntervalMs);
            }

            lock (_lock) { _currentDisplayedProgress = targetProgress; }
            _onProgress?.Invoke(message, targetProgress, tag);
        }
        public void Reset()
        {
            lock (_lock)
            {
                _currentDisplayedProgress = 0;
            }
        }
    }
}