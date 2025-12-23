using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnderMine.Services.ProgressBar
{
    // Новый ProgressManager без хардкода
    public class ProgressManager
    {
        private readonly Action<double, string> _onUpdate;

        // Словарь: Ключ (имя этапа) -> (ТекущийПрогресс, ВесЭтапа)
        private readonly Dictionary<string, (double Progress, double Weight)> _stages = new();

        public ProgressManager(Action<double, string> onUpdate) => _onUpdate = onUpdate;

        // Регистрация этапа (вызывается при старте приложения)
        public void RegisterStage(string stageName, double weight)
        {
            _stages[stageName] = (0, weight);
        }

        public void Report(string stageName, double stageProgress, string message)
        {
            if (_stages.ContainsKey(stageName))
            {
                var weight = _stages[stageName].Weight;
                // Обновляем прогресс конкретного этапа (0.0 - 1.0)
                _stages[stageName] = (Math.Clamp(stageProgress, 0, 1), weight);
            }

            CalculateGlobalAndNotify(message);
        }

        private void CalculateGlobalAndNotify(string message)
        {
            double totalWeight = _stages.Values.Sum(x => x.Weight);
            if (totalWeight == 0) return;

            double currentWeightedSum = _stages.Values.Sum(x => x.Progress * x.Weight);

            // Нормализуем к 100%
            double globalProgress = (currentWeightedSum / totalWeight) * 100;

            _onUpdate?.Invoke(globalProgress, message);
        }
    }
}
