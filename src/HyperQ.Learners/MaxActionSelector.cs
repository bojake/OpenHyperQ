using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;
namespace HyperQ.Learners
{
    public enum MinMaxActionEnum
    {
        LeastProbable,
        MostProbable,
        Default
    }

    [Serializable]
    public abstract class MinMaxActionSelectorBase<T> : IActionSelector<T>
    {
        private QActionSpace<int> _actionSpace;
        public bool LastActionWasRandom { get; protected set; } = false;
        public long RandomActionCount { get; private set; } = 0;
        // Keep a list of prior actions
        private QAction[] _LastActions = null;
        private double[] _LastActionProbabilities = null;
        private int _idx = 0;
        protected QRandom _ran = null;
        public MinMaxActionEnum ActionMode { get; set; } = MinMaxActionEnum.MostProbable;
        public Action<T, QAction> TelemetryCallback { get; set; } = null;
        /// <summary>
        /// The decay (or inflation) value used to reduce the memory of prior acts when choosing
        /// a random value for states that are new to this action selector.
        /// </summary>
        public double Histeresis { get; set; } = 0.997;

        /// <summary>
        /// Does nothing
        /// </summary>
        /// <param name="s"></param>
        /// <param name="a"></param>
        /// <param name="advantage"></param>
        /// <param name="hp"></param>
        public virtual void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
        {
            // Does nothing
        }

        public virtual void StartEpisode()
        {
        }

        public virtual void EndEpisode(RewardAccumulator reward)
        {
        }

        public MinMaxActionSelectorBase(QActionSpace<int> actionSpace, QRandom ran = null)
        {
            _actionSpace = actionSpace;
            _ran = ran ?? QRandom.Instance;
            _LastActions = new QAction[actionSpace.MaximumNumberOfActions];
            for (int i = 0; i < _LastActions.Length; i++)
            {
                _LastActions[i] = new QAction(actionSpace.FromIndex((uint)i), 0.0, (uint)i);
            }
            _LastActionProbabilities = new double[_LastActions.Length];
            for (int i = 0; i < _LastActions.Length; i++)
            {
                // All prior actions are equally probable
                _LastActionProbabilities[i] = 1.0 / (double)_LastActions.Length;
            }
            if (ran != null)
            {
                _ran = ran;
            }
        }

        public abstract QAction SelectAction(IArgMinMax<T> q, T state, double epsilon);

        /// <summary>
        /// Shifts the action arrays either left or right, if the dir parameter
        /// is -1 or 1, respectively. Shifts only 1 element
        /// </summary>
        /// <param name="dir">Direction to shift. 1 for right, -1 for left.</param>
        /// <returns></returns>
        private int Shift(int dir)
        {
            if (dir < 0)
            {
                // Shift left
                for (int j = 0; j < _LastActions.Length - 1; j++)
                {
                    _LastActions[j] = _LastActions[j + 1];
                    _LastActionProbabilities[j] = _LastActionProbabilities[j + 1];
                }
                return _LastActions.Length - 1;
            }
            else
            {
                // Shift right
                for (int j = _LastActions.Length-2; j >= 0; j--) 
                {
                    _LastActions[j+1] = _LastActions[j];
                    _LastActionProbabilities[j+1] = _LastActionProbabilities[j];
                }
                return 0;
            }
        }

        /// <summary>
        /// Chooses an action from the history of actions using the probabilitlies
        /// assigned to each action. If no action is selected from the distribution, then
        /// a random action on the [0,maxActions] range is chosen.
        /// </summary>
        /// <param name="maxActions">The maximum number of actions in the domain</param>
        /// <returns></returns>
        protected QAction RandomAction(IArgMinMax<T> q)
        {
            LastActionWasRandom = true;
            RandomActionCount++;
            if (_LastActions != null)
            {
                double r = _ran.Ran.NextDouble();
                for (int i = 0; i < _LastActions.Length; i++)
                {
                    double rx = _LastActionProbabilities[i];
                    if (ActionMode == MinMaxActionEnum.LeastProbable)
                        rx = 1.0 - rx;
                    if (rx > r && _LastActions[i] != null)
                    {
                        return (_LastActions[i]);
                    }
                    r -= rx;
                }
            }
            // Here we choose a uniform random
            int action = _actionSpace.RandomAction(true);
            return new QAction(action, 0.0, _actionSpace.ToIndex(action));
        }

        /// <summary>
        /// Saves the given action and updates the probability distribution according to 
        /// </summary>
        /// <param name="a"></param>
        protected void SaveAction(QAction a)
        {
            // decay all priors if this was not random, otherwise inflate all priors
            int nextIdx = -1;
            double pSum = 0.0;
            double pNext = 1.0;
            if (LastActionWasRandom)
            {
                nextIdx = Shift(-1);
                for (int j = 0; j < _LastActionProbabilities.Length-1; j++)
                {
                    // Find the min which will be used for the random action's probability
                    if (_LastActionProbabilities[j] < pNext)
                    {
                        pNext = _LastActionProbabilities[j];
                    }
                    // Inflate the other actions
                    _LastActionProbabilities[j] *= (1.0 - Histeresis);
                    if (j != nextIdx)
                    {
                        pSum += _LastActionProbabilities[j];
                    }
                }
                pNext /= 2.0; // Cut the min prob for the random act by 2 to make is very less probable
                pSum += pNext;
            }
            else
            {
                pNext = 0.0;
                nextIdx = Shift(1);
                for (int j = 1; j < _LastActionProbabilities.Length; j++)
                {
                    // Find the max which will be used for the random action's probability
                    if (_LastActionProbabilities[j] > pNext)
                    {
                        pNext = _LastActionProbabilities[j];
                    }
                    // Deflate the other actions
                    _LastActionProbabilities[j] *= Histeresis;
                    if (j != nextIdx)
                    {
                        pSum += _LastActionProbabilities[j];
                    }
                }
                pNext *= (2-Histeresis); // Add the histeresis amount to the likelihood to make this stand out more
                pSum += pNext;
            }
            _LastActionProbabilities[nextIdx] = pNext;
            _LastActions[nextIdx] = a;
            // Normalize the probs
            for (int j = 0; j < _LastActionProbabilities.Length; j++)
            {
                _LastActionProbabilities[j] /= pSum;
            }
            _idx = nextIdx;
            // If all of the actions are the same, then shuffle/reset
            bool same = true;
            for (int i = 1; same && i < _LastActions.Length; i++)
            {
                if (_LastActions[i] == null || _LastActions[i].Action != _LastActions[0].Action)
                    same = false;
            }
            if (same)
            {
                // Remake the priors. The probabilities are irrelevant here
                for (int i = 0; i < _LastActions.Length; i++)
                {
                    _LastActions[i] = new QAction(_actionSpace.FromIndex((uint)i), 0.0, (uint)i);
                }
            }
        }

        /// <summary>
        /// Common method to either choose a random action if the 't' parameter is null, or 
        /// return the Item1 parameter from the tuple.
        /// </summary>
        /// <param name="t">The result of a max or min arg selection</param>
        /// <param name="maxNumActions">The max number of actions in the Q</param>
        /// <returns></returns>
        protected virtual QAction ProcessSelection(T state, IArgMinMax<T> q, QAction t)
        {
            QAction a = null;
            if (t == null)
            {
                a = RandomAction(q);
            }
            else
            {
                a = t;
            }
            SaveAction(a);
            TelemetryCallback?.Invoke(state, a);
            return (a);
        }
    }

    [Serializable]
    public class MaxActionSelector<T> : MinMaxActionSelectorBase<T>
    {
        public MaxActionSelector(QActionSpace<int> actionSpace, QRandom ran = null) : base(actionSpace, ran)
        {
        }

        /// <summary>
        /// Always returns the maximum reward action for the given state. Does not use random selection.
        /// </summary>
        /// <param name="q">The Q</param>
        /// <param name="state">The discretized state</param>
        /// <param name="epsilon">Ignored</param>
        /// <returns></returns>
        public override QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            LastActionWasRandom = false;
            return ProcessSelection(state, q, q.ArgMax(state));
        }
    }

    [Serializable]
    public class MostProbableMaxActionSelector<T> : MinMaxActionSelectorBase<T>
    {
        public MostProbableMaxActionSelector(QActionSpace<int> actionSpace, QRandom ran = null) : base(actionSpace, ran)
        {
            ActionMode = MinMaxActionEnum.MostProbable;
        }

        /// <summary>
        /// Always returns the maximum reward action for the given state. Does not use random selection.
        /// </summary>
        /// <param name="q">The Q</param>
        /// <param name="state">The discretized state</param>
        /// <param name="epsilon">Ignored</param>
        /// <returns></returns>
        public override QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            LastActionWasRandom = false;
            if (_ran.Ran.NextDouble() < epsilon)
            {
                // Choose a random action
                return (ProcessSelection(state, q, null));
            }
            return ProcessSelection(state, q, q.ArgMax(state));
        }
    }

    [Serializable]
    public class LeastProbableMaxActionSelector<T> : MinMaxActionSelectorBase<T>
    {
        public LeastProbableMaxActionSelector(QActionSpace<int> actionSpace, QRandom ran = null) : base(actionSpace, ran)
        {
            ActionMode = MinMaxActionEnum.LeastProbable;
        }

        /// <summary>
        /// Always returns the maximum reward action for the given state. Does not use random selection.
        /// </summary>
        /// <param name="q">The Q</param>
        /// <param name="state">The discretized state</param>
        /// <param name="epsilon">Ignored</param>
        /// <returns></returns>
        public override QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            LastActionWasRandom = false;
            if (_ran.Ran.NextDouble() < epsilon)
            {
                // Choose a random action
                return (ProcessSelection(state, q, null));
            }
            return ProcessSelection(state, q, q.ArgMax(state));
        }
    }

    [Serializable]
    public class MinActionSelector<T> : MinMaxActionSelectorBase<T>
    {
        public MinActionSelector(QActionSpace<int> actionSpace, QRandom ran = null) : base(actionSpace, ran)
        {
        }

        /// <summary>
        /// Always returns the maximum reward action for the given state. Does not use random selection.
        /// </summary>
        /// <param name="q">The Q</param>
        /// <param name="state">The discretized state</param>
        /// <param name="epsilon">Ignored</param>
        /// <returns></returns>
        public override QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            LastActionWasRandom = false;
            return ProcessSelection(state, q, q.ArgMin(state));
        }
    }
}
