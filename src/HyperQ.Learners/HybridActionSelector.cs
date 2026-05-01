using System;
using System.Collections.Generic;
using System.Linq;
using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// Hybrid action selector that chooses among a set of selectors based on
    /// their running average reward over the last N episodes. The selector in
    /// use only changes at the start of an episode.
    /// </summary>
    [Serializable]
    public class HybridActionSelector<T> : IActionSelector<T>
    {
        private readonly IActionSelector<T>[] _selectors;
        private readonly List<Queue<double>> _rewardWindows;
        private readonly int _window;
        private int _currentIndex = 0;

        public HybridActionSelector(int window, params IActionSelector<T>[] selectors)
        {
            if (selectors == null || selectors.Length == 0)
                throw new ArgumentException("At least one selector must be provided", nameof(selectors));
            _selectors = selectors;
            _window = Math.Max(1, window);
            _rewardWindows = new List<Queue<double>>(selectors.Length);
            for (int i = 0; i < selectors.Length; i++)
                _rewardWindows.Add(new Queue<double>());
        }

        private IActionSelector<T> Current => _selectors[_currentIndex];

        public bool LastActionWasRandom => Current.LastActionWasRandom;
        public long RandomActionCount => Current.RandomActionCount;

        public MinMaxActionEnum ActionMode
        {
            get => Current.ActionMode;
            set
            {
                foreach (var s in _selectors)
                    s.ActionMode = value;
            }
        }

        public Action<T, QAction> TelemetryCallback
        {
            get => Current.TelemetryCallback;
            set
            {
                foreach (var s in _selectors)
                    s.TelemetryCallback = value;
            }
        }

        public void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
        {
            Current.ApplyAdvantage(s, a, advantage, hp);
        }

        public QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            return Current.SelectAction(q, state, epsilon);
        }

        private void UpdateReward(int idx, RewardAccumulator reward)
        {
            var q = _rewardWindows[idx];
            q.Enqueue(reward);
            if (q.Count > _window)
                q.Dequeue();
        }

        private int BestSelectorIndex()
        {
            double best = double.NegativeInfinity;
            int bestIdx = 0;
            for (int i = 0; i < _rewardWindows.Count; i++)
            {
                if (_rewardWindows[i].Count == 0)
                    continue;
                double avg = _rewardWindows[i].Average();
                if (avg > best)
                {
                    best = avg;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        public void StartEpisode()
        {
            _currentIndex = BestSelectorIndex();
            Current.StartEpisode();
        }

        public void EndEpisode(RewardAccumulator reward)
        {
            Current.EndEpisode(reward);
            UpdateReward(_currentIndex, reward);
        }
    }
}
