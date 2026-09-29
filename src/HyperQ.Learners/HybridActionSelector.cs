using System;
using System.Collections.Generic;
using System.Linq;
using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// Hybrid action selector that chooses among a set of selectors by their episode rewards. The choice is made at
    /// the start of each episode and holds for the whole episode:
    /// <list type="number">
    /// <item>A selector that has not finished an episode yet goes first, in the order the selectors were given, so
    /// every component is tried.</item>
    /// <item>After that, the selector with the highest average reward over its own last <c>window</c> episodes; a
    /// tie goes to the earlier selector.</item>
    /// </list>
    /// There is no exploration after the first round: a selector whose first episodes scored badly is chosen again
    /// only when the current selector's average falls below that selector's last average.
    /// <para>
    /// Only the current selector receives <see cref="ApplyAdvantage"/>. The policy-gradient components update on
    /// policy: their gradient assumes the action was drawn from their own policy, so the steps another selector
    /// chose would bias them without importance weights. Each component therefore learns only from its own
    /// episodes.
    /// </para>
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
            // Untried selectors first, in order. (Skipping them instead, as this once did, left every window but the
            // first one empty forever, so the first selector was the only one ever used.)
            for (int i = 0; i < _rewardWindows.Count; i++)
            {
                if (_rewardWindows[i].Count == 0)
                    return i;
            }
            double best = double.NegativeInfinity;
            int bestIdx = 0;
            for (int i = 0; i < _rewardWindows.Count; i++)
            {
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
