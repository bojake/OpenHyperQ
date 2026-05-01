using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{

    [Serializable]
    public class UniformActionSelector<T> : IActionSelector<T>
    {
        protected QActionSpace<int> _actionSpace;
        /// <summary>
        /// [state] => (action-index) => count of incidence
        /// </summary>
        private Dictionary<uint, Dictionary<uint, int>> _actions = new Dictionary<uint, Dictionary<uint, int>>();
        public virtual bool LastActionWasRandom { get; protected set; } = false;
        public long RandomActionCount { get; protected set; } = 0;
        public MinMaxActionEnum ActionMode { get; set; } = MinMaxActionEnum.Default;
        public Action<T, QAction> TelemetryCallback { get; set; } = null;
        private IStateMapper<T> _mapper;

        public UniformActionSelector(IStateMapper<T> mapper, QActionSpace<int> actionSpace)
        {
            _actionSpace = actionSpace;
            _mapper = mapper;
        }

        /// <summary>
        /// Convenience 1-arg constructor: creates an internal MemoryBackedHyperMapper&lt;T&gt;
        /// as the state mapper. Matches the pre-.NET10 prebuilt DLL API where samples
        /// could omit the explicit state mapper.
        /// </summary>
        public UniformActionSelector(QActionSpace<int> actionSpace)
            : this(new MemoryMapperAdapter<T>(), actionSpace) { }
        /// <summary>
        /// Does nothing
        /// </summary>
        /// <param name="s"></param>
        /// <param name="a"></param>
        /// <param name="advantage"></param>
        /// <param name="hp"></param>
        public virtual void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
        {
        }

        public virtual void StartEpisode()
        {
        }

        public virtual void EndEpisode(RewardAccumulator reward)
        {
        }

        protected uint randomAction(uint state_index, IArgMinMax<T> q)
        {
            LastActionWasRandom = false;
            int max_actions = (int)_actionSpace.MaximumNumberOfActions;
            uint ix = 0;
            if (!_actions.ContainsKey(state_index))
            {
                _actions[state_index] = new Dictionary<uint, int>();
                int a = _actionSpace.RandomAction(true);
                ix = _actionSpace.ToIndex(a);
                _actions[state_index][ix] = 1;
                LastActionWasRandom = true;
                RandomActionCount++;
            }
            else
            {
                Dictionary<uint, int> a = _actions[state_index];
                if (a.Keys.Count < max_actions)
                {
                    do
                    {
                        int ax = _actionSpace.RandomAction(true);
                        ix = _actionSpace.ToIndex(ax);
                    }
                    while (a.ContainsKey(ix));
                    // Pick a random action that has not been selected
                    a[ix] = 1;
                    LastActionWasRandom = true;
                    RandomActionCount++;
                }
                else
                {
                    // Choose the least visited action
                    int kv = 0;
                    foreach (var k in a.Keys)
                    {
                        int iv = a[k];
                        if (kv == 0)
                        {
                            kv = iv;
                            ix = k;
                        }
                        else if (iv < kv)
                        {
                            kv = iv;
                            ix = k;
                        }
                        // Console.WriteLine($"Action {k} visited {a[k]} times");
                    }
                    //ix = a.Aggregate((x1, x2) => x1.Value < x2.Value ? x1 : x2).Key;
                    a[ix]++;
                }
            }
            return ix;
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
            uint i = _mapper.AddState(state);
            uint ix = randomAction(i,q);
            QAction action = null;
            action = new QAction(_actionSpace.FromIndex(ix), 0.0, ix);
            TelemetryCallback?.Invoke(state, action);
            return (action);
        }
    }
}
