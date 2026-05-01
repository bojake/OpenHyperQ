using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    [Serializable]
    public class eGreedyActionSelector<T> : IActionSelector<T>
    {
        private QActionSpace<int> _actionSpace;
        public virtual long RandomActionCount { get; private set; }  = 0;
        public virtual bool LastActionWasRandom { get; private set; } = false;
        public MinMaxActionEnum ActionMode { get; set; } = MinMaxActionEnum.Default;
        public Action<T, QAction> TelemetryCallback { get; set; } = null;
        public eGreedyActionSelector(QActionSpace<int> actionSpace)
        {
            _actionSpace = actionSpace;
        }

        /// <summary>
        /// Does nothing
        /// </summary>
        /// <param name="s"></param>
        /// <param name="a"></param>
        /// <param name="advantage"></param>
        /// <param name="hp"></param>
        public virtual void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
        {
            // Does nothing, intentionally
        }

        public virtual void StartEpisode()
        {
        }

        public virtual void EndEpisode(RewardAccumulator reward)
        {
        }

        private QAction RandomAction(IArgMinMax<T> q)
        {
            RandomActionCount++;
            LastActionWasRandom = true;
            int amax = _actionSpace.RandomAction(true);
            uint imax = _actionSpace.ToIndex(amax);
            return (new QAction(amax, 0.0, imax));
        }
        /// <summary>
        /// Chooses the action based upon the MAX reward return.
        /// </summary>
        /// <param name="q">The Q</param>
        /// <param name="state">The state the system is in</param>
        /// <param name="epsilon">Randomness trigger, higher values will generate more random action selection</param>
        /// <returns></returns>
        public virtual QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            LastActionWasRandom = false;
            if (_actionSpace.Random.Ran.NextDouble() < epsilon)
            {
                QAction ra = RandomAction(q);
                TelemetryCallback?.Invoke(state, ra);
                return ra;
            }
            QAction r = null;
            if (ActionMode == MinMaxActionEnum.LeastProbable)
                r = q.ArgMin(state);
            else
                r = q.ArgMax(state);
            if (r == null)
            {
                QAction ra = RandomAction(q);
                TelemetryCallback?.Invoke(state, ra);
                return ra;
            }
            TelemetryCallback?.Invoke(state, r);
            return (r);
        }
    }
}
