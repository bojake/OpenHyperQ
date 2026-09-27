using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;

namespace HyperQ.Learners
{
    [Serializable]
    public class DoubleHyperQ<T> : IHyperQ<T>
    {
        public QAdvantage Advantage { get; private set; } = null;
        private QRandom _random;
        private SingleHyperQ<T> _Q1;
        private SingleHyperQ<T> _Q2;
        private MemoryBackedHyperMapper<T> _StateMap = new MemoryBackedHyperMapper<T>();
        public QActionSpace<int> ActionSpace { get; private set; } = null;
        private Dictionary<uint, QArg<double>> _StateArgs = new Dictionary<uint, QArg<double>>();
        private int _WhichQ = 0;
        private Func<double, double, double> _BlendingFunction = null;
        private double[] _defaultActionArray;
        public virtual bool RequiresIndexRepositoryLinking { get { return true; } }
        /// <summary>
        /// The action used to get a default value for a (s,a) pair.
        /// </summary>
        public Func<double> DefaultValueFunc { get; set; } = () => 0.0;
        public string Label { 
            set {
                _Q1.Label = string.Format("{0}_1", value);
                _Q2.Label = string.Format("{0}_2", value);
            }
            get {
                if (_WhichQ == 0)
                {
                    return (_Q1.Label);
                }
                return (_Q2.Label);
            }
        }

        private double __default_blender(double q1, double q2) {
            return 0.5 * (q1 + q2);
        }

        public DoubleHyperQ(QActionSpace<int> actionSpace, Func<double> defaultValueAction = null, QRandom ran = null, Func<double,double,double> blendingFunction=null)
        {
            ActionSpace = actionSpace;
            _Q1 = new SingleHyperQ<T>(actionSpace, defaultValueAction);
            _Q2 = new SingleHyperQ<T>(actionSpace, defaultValueAction);
            _Q1.Link(_StateMap, ActionSpace);
            _Q2.Link(_StateMap, ActionSpace);
            if (defaultValueAction != null)
            {
                DefaultValueFunc = defaultValueAction;
            }
            if (blendingFunction != null)
            {
                _BlendingFunction = blendingFunction;
            }
            else
            {
                _BlendingFunction = __default_blender;
            }
            _random = ran ?? QRandom.Instance;
            CreateDefaultActionArray();
            Advantage = new QAdvantage();
        }
        protected virtual void CreateDefaultActionArray()
        {
            if (_defaultActionArray == null)
            {
                _defaultActionArray = new double[MaxNumActions];
            }
            for (int i = 0; i < MaxNumActions; i++)
            {
                _defaultActionArray[i] = DefaultValueFunc();
            }
        }
        public virtual Tuple<uint, uint> Shape { get { return _Q1.Shape; } }
        public virtual void Link(MemoryBackedHyperMapper<T> stateMap, QActionSpace<int> actionSpace)
        {
            _StateMap = stateMap;
            ActionSpace = actionSpace;
            _Q1.Link(_StateMap, ActionSpace);
            _Q2.Link(_StateMap, ActionSpace);
        }
        /// <summary>
        /// Returns the action-space indexes that either table has a value for in the given state.
        /// </summary>
        private SortedSet<uint> KnownActionIndexes(uint stateIndex)
        {
            SortedSet<uint> indexes = new SortedSet<uint>();
            foreach (KeyValuePair<uint, double> kv in _Q1.KnownActionValuesAt(stateIndex))
            {
                indexes.Add(kv.Key);
            }
            foreach (KeyValuePair<uint, double> kv in _Q2.KnownActionValuesAt(stateIndex))
            {
                indexes.Add(kv.Key);
            }
            return indexes;
        }

        private double DefaultValue()
        {
            return DefaultValueFunc != null ? DefaultValueFunc() : 0.0;
        }

        private double Blend(double a1, double a2)
        {
            return _BlendingFunction != null ? _BlendingFunction(a1, a2) : 0.5 * (a1 + a2);
        }

        /// <summary>
        /// Blends, per action index, the values the two tables hold for the state. An action only one table has
        /// seen is blended with the default value, exactly as <see cref="GetValue"/> would report it, but without
        /// writing that default into the other table.
        /// </summary>
        public virtual IEnumerable<KeyValuePair<uint, double>> KnownActionValues(QState<T> stateKey)
        {
            List<KeyValuePair<uint, double>> result = new List<KeyValuePair<uint, double>>();
            if (!_StateMap.Known(stateKey.Enumerator))
            {
                return result;
            }
            uint ix = _StateMap[stateKey.Enumerator];
            Dictionary<uint, double> q1 = new Dictionary<uint, double>();
            foreach (KeyValuePair<uint, double> kv in _Q1.KnownActionValuesAt(ix))
            {
                q1[kv.Key] = kv.Value;
            }
            Dictionary<uint, double> q2 = new Dictionary<uint, double>();
            foreach (KeyValuePair<uint, double> kv in _Q2.KnownActionValuesAt(ix))
            {
                q2[kv.Key] = kv.Value;
            }
            foreach (uint index in q1.Keys.Union(q2.Keys).OrderBy(i => i))
            {
                double a1;
                double a2;
                if (!q1.TryGetValue(index, out a1))
                {
                    a1 = DefaultValue();
                }
                if (!q2.TryGetValue(index, out a2))
                {
                    a2 = DefaultValue();
                }
                result.Add(new KeyValuePair<uint, double>(index, Blend(a1, a2)));
            }
            return result;
        }

        /// <summary>
        /// Returns the blended values of the actions known in either table for the given state, ordered by
        /// action index. The two tables fill in different orders, so the values are matched up by action
        /// index rather than by position in each table's row.
        /// </summary>
        public virtual double[] GetKnownActionArray(QState<T> stateKey)
        {
            if (!_StateMap.Known(stateKey.Enumerator))
            {
                // Return a random array over all actions
                double[] r1 = _defaultActionArray;
                CreateDefaultActionArray();
                return (r1);
            }
            uint state_idx = _StateMap[stateKey.Enumerator];
            SortedSet<uint> indexes = KnownActionIndexes(state_idx);
            double[] q = new double[indexes.Count];
            int i = 0;
            foreach (uint index in indexes)
            {
                q[i++] = GetValue(stateKey, ActionSpace.FromIndex(index));
            }
            return (q);
        }

        /// <summary>
        /// Returns the cached blended min/max for the state, computing it from the blended value of every
        /// action known to either table when the cache has no entry. Returns null when nothing is known.
        /// </summary>
        private QArg<double> StateArgsFor(QState<T> stateKey, uint ix)
        {
            QArg<double> arg;
            if (_StateArgs.TryGetValue(ix, out arg))
            {
                return arg;
            }
            SortedSet<uint> indexes = KnownActionIndexes(ix);
            if (indexes.Count == 0)
            {
                return null;
            }
            arg = new QArg<double>();
            foreach (uint index in indexes)
            {
                int action = ActionSpace.FromIndex(index);
                arg.Set(GetValue(stateKey, action), action);
            }
            _StateArgs[ix] = arg;
            return arg;
        }

        /// <summary>
        /// Returns the averaged Q matrix in 2D format of the two Q's in this QQ.
        /// </summary>
        public virtual double[,] AsMatrix
        {
            get
            {
                double[,] m1 = _Q1.AsMatrix;
                double[,] m2 = _Q2.AsMatrix;
                double[,] m = null;
                if (m1.GetLength(1) != m2.GetLength(1))
                {
                    throw (new NotSupportedException($"Incongruent matrix merge is not supported: Q1={m1.GetLength(1)} Q2={m2.GetLength(1)}"));
                }
                int rows = Math.Max(m1.GetLength(0), m2.GetLength(0));
                if (m1.GetLength(0) > m2.GetLength(0))
                    m = m1;
                else
                    m = m2;

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < m1.GetLength(1); c++)
                    {
                        double a1 = 0.0;
                        double a2 = 0.0;
                        if (DefaultValueFunc != null)
                        {
                            a1 = DefaultValueFunc();
                            a2 = DefaultValueFunc();
                        }
                        if (r < m1.GetLength(0))
                            a1 = m1[r, c];
                        if (r < m2.GetLength(0))
                            a2 = m2[r, c];
                        m[r, c] = _BlendingFunction(a1, a2);
                    }
                }
                return m;
            }
        }

        public virtual uint MaxNumActions { 
            get {
                return ActionSpace.MaximumNumberOfActions;
            }
        }


        public virtual uint NumActions
        {
            get
            {
                return (ActionSpace.NumberOfKnownActions);
            }
        }
        public virtual bool IsKnownState(QState<T> stateKey)
        {
            return (_StateMap.Known(stateKey.Enumerator));
        }
        public virtual bool IsKnownAction(int action)
        {
            return (ActionSpace.IsKnownAction(action));
        }
        #region Q Interface
        public virtual bool RemoveState(QState<T> stateKey)
        {
            return(_StateMap.RemoveKey(stateKey.Enumerator));
        }
        public virtual uint AddState(QState<T> stateKey)
        {
            uint i1 = _Q1.AddState(stateKey);
            uint i2 = _Q2.AddState(stateKey);
            if (i1 != i2)
            {
                // OOps, error
                throw (new Exception("State index for " + stateKey + " should be the same, but it was not consistent: i1=" + i1 + ",i2=" + i2));
            }
            return (i1);
        }

        public virtual double[] GetActionArray(QState<T> stateKey)
        {
            double[] r = new double[MaxNumActions];
            for (int i = 0; i < MaxNumActions; i++)
            {
                r[i] = GetValue(stateKey, i); // 0.5 * (_Q1.GetValue(stateKey, i) + _Q2.GetValue(stateKey, i));
            }
            return (r);
        }
        /// <summary>
        /// Returns the action index and Q value for that action that is the maximum average value from all
        /// of the Q learners in this ensemble.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>Tuple(action,Q)</returns>
        public virtual QAction ArgMax(QState<T> stateKey)
        {
            uint ix = _StateMap[stateKey.Enumerator];
            QArg<double> arg = StateArgsFor(stateKey, ix);
            if (arg == null)
                return null;
            return new QAction(arg.MaxIndex, arg.Max, ActionSpace.ToIndex(arg.MaxIndex));
        }

        /// <summary>
        /// Returns the action index and Q value for that action that is the minimum average value from all
        /// of the Q learners in this ensemble.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>Tuple(action,Q)</returns>
        public virtual QAction ArgMin(QState<T> stateKey)
        {
            uint ix = _StateMap[stateKey.Enumerator];
            QArg<double> arg = StateArgsFor(stateKey, ix);
            if (arg == null)
                return null;
            return new QAction(arg.MinIndex, arg.Min, ActionSpace.ToIndex(arg.MinIndex));
        }

        public virtual Q<QState<T>> Clone()
        {
            DoubleHyperQ<T> c = new DoubleHyperQ<T>(ActionSpace, DefaultValueFunc);
            c._Q1 = _Q1.Clone() as SingleHyperQ<T>;
            c._Q2 = _Q2.Clone() as SingleHyperQ<T>;
            return (c);
        }

        public virtual void Dump(string label = "Double HyperQ", int level = 0, bool dumpData = false)
        {
            Console.WriteLine("=~{0}:Q1~=",label??Label);
            _Q1.Dump(level: level, dumpData: dumpData);
            Console.WriteLine("=~{0}:Q2~=", label ?? Label);
            _Q2.Dump(level: level, dumpData: dumpData);
        }

        public virtual double GetValue(QState<T> stateKey, int action)
        {
            double q1 = 0.0;
            double q2 = 0.0;
            if (IsKnownState(stateKey))
            {
                q1 = _Q1[stateKey, action];
                q2 = _Q2[stateKey, action];
            }
            else
            {
                if (DefaultValueFunc != null)
                {
                    q1 = DefaultValueFunc();
                    q2 = DefaultValueFunc();
                }
            }
            if (_BlendingFunction != null)
            {
                return (_BlendingFunction(q1, q2));
            }
            return 0.5 * (q1 + q2);
        }

        public virtual void MergeInto(Q<QState<T>> from, EvalMethodType methodType = EvalMethodType.Max)
        {
            if (from is DoubleHyperQ<T>)
            {
                DoubleHyperQ<T> fromQ = (DoubleHyperQ<T>)from;
                _Q1.MergeInto(fromQ._Q1, methodType);
                _Q2.MergeInto(fromQ._Q2, methodType);
            }
            else
            {
                _Q1.MergeInto(from, methodType);
                _Q2.MergeInto(from, methodType);
            }
        }

        public virtual void SetValue(QState<T> stateKey, int action, double v)
        {
            AddState(stateKey);
            if (_WhichQ == 0)
            {
                _Q1.SetValue(stateKey, action, v);
            }
            else
            {
                _Q2.SetValue(stateKey, action, v);
            }
            InvalidateStateArgs(stateKey);
            NextQ();
        }

        /// <summary>
        /// Drops the cached blended min/max of a state after one of the tables changed. Keeping the cache
        /// in sync incrementally is not possible because a lowered maximum needs a full rescan, so the
        /// extremes are recomputed lazily by the next ArgMax/ArgMin.
        /// </summary>
        private void InvalidateStateArgs(QState<T> stateKey)
        {
            _StateArgs.Remove(_StateMap[stateKey.Enumerator]);
        }

        /// <summary>
        /// Performs the on-policy update calculation
        /// </summary>
        /// <param name="s">Current state</param>
        /// <param name="a">Current action</param>
        /// <param name="sprime">Derived state from S(a)</param>
        /// <param name="aprime">Derived action for S(a)</param>
        /// <param name="r">The reward for this action</param>
        /// <param name="gamma">Decay parameter</param>
        /// <param name="alpha">Learning parameter</param>
        /// <returns></returns>
        public virtual double OnPolicyUpdate(QState<T> s, int a, QState<T> sprime, int aprime, double r, HyperParams hp)
        {
            AddState(s);
            AddState(sprime);
            // Canonical Double-Q (Hasselt 2010): randomly select which Q to update,
            // use the other Q for value evaluation to reduce maximization bias.
            NextQ(-1, true);
            IHyperQ<T>[] qq = { _Q1, _Q2 };
            IHyperQ<T> q1 = qq[_WhichQ];
            IHyperQ<T> q2 = qq[(_WhichQ + 1) % 2];
            double prime_v = q2.GetValue(sprime, aprime);
            double curr_v = q1.GetValue(s, a);
            double baseline = QUpdateCore.Baseline(q1.GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            q1.SetValue(s, a, new_v);
            InvalidateStateArgs(s);
            return new_v;
        }
        public virtual double OffPolicyUpdate(QState<T> s, int a, QState<T> sprime, double r, HyperParams hp, EvalMethodType evalType = EvalMethodType.Max)
        {
            AddState(s);
            AddState(sprime);
            // Canonical Double-Q (Hasselt 2010): randomly select which Q to update,
            // use q1 for action selection, q2 for value evaluation.
            NextQ(-1, true);
            IHyperQ<T>[] qq = { _Q1, _Q2 };
            IHyperQ<T> q1 = qq[_WhichQ];
            IHyperQ<T> q2 = qq[(_WhichQ + 1) % 2];
            int aprime = QUpdateCore.SelectGreedyAction(sprime, a, evalType, q1.ArgMax, q1.ArgMin);
            double prime_v = q2.GetValue(sprime, aprime);
            double curr_v = q1.GetValue(s, a);
            double baseline = QUpdateCore.Baseline(q1.GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            q1.SetValue(s, a, new_v);
            InvalidateStateArgs(s);
            return new_v;
        }
        #endregion
        /// <summary>
        /// Schedule the next Q to use, or set it explicitly.
        /// </summary>
        /// <param name="which">optionally specify which Q to use, either 0 or 1. This value is mod 2 so any value is safe</param>
        public virtual void NextQ(int which = -1, bool random = false)
        {
            if (random)
            {
                _WhichQ = _random.Ran.Next(2);
            }
            else
            {
                if (which == -1)
                {
                    _WhichQ = (_WhichQ + 1) % 2;
                }
                else
                {
                    _WhichQ = ((which % 2) + 2) % 2;
                }
            }
        }
        /// <summary>
        /// Returns the current selected Q
        /// </summary>
        /// <returns></returns>
        public virtual IHyperQ<T> CurrentQ()
        {
            if (_WhichQ == 0)
            {
                return (_Q1);
            }
            return (_Q2);
        }
        /// <summary>
        /// Returns the other Q instead of the current selected Q
        /// </summary>
        /// <returns></returns>
        public virtual IHyperQ<T> AltQ()
        {
            if (_WhichQ == 0)
            {
                return (_Q2);
            }
            return (_Q1);
        }

        public virtual uint MapState(QState<T> stateKey)
        {
            return _StateMap[stateKey.Enumerator];
        }

        public void ResetAdvantageTrace()
        {
            Advantage = new QAdvantage();
        }

        public IDictionary<QState<T>, double[]> Snapshot(IEnumerable<QState<T>> states)
        {
            Dictionary<QState<T>, double[]> snap = new Dictionary<QState<T>, double[]>();
            foreach(var s in states)
            {
                snap[s] = GetActionArray(s);
            }
            return snap;
        }

        public virtual double this[QState<T> state, int action]
        {
            get
            {
                return GetValue(state, action);
            }
            set
            {
                SetValue(state, action, value);
            }
        }
    }
}
