using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;
using System.IO;
using HyperQ.Util.Licensing;

namespace HyperQ.Learners
{
    [Serializable]
    public class SingleHyperQ<T> : IHyperQ<T>, ICheckpointable
    {
        public QAdvantage Advantage { get; private set; } = null;

        /// <summary>
        /// The matrix version of the Q data
        /// </summary>
        private double[,] _Q = null;
        /// <summary>
        /// The adaptive Q data
        /// </summary>
        private List<QRow> _data = null;
        /// <summary>
        /// Maps a state key to its index in the '_data' list. _data[_stateMap[key]][action] = Q(s,a)
        /// </summary>
        private MemoryBackedHyperMapper<T> _StateMap = new MemoryBackedHyperMapper<T>();
        public QActionSpace<int> ActionSpace { get; private set; } = null;
        /// <summary>
        /// Index the min/max arg cache by the state index
        /// </summary>
        private Dictionary<uint, QArg<double>> _StateArgs = new Dictionary<uint, QArg<double>>();
        private uint _StateIndexStart = 0;
        private uint _ActionIndexStart = 0;
        public virtual bool RequiresIndexRepositoryLinking { get { return false; } }

        /// <summary>
        /// The action used to get a default value for a (s,a) pair.
        /// </summary>
        public Func<double> DefaultValueFunc { get; set; } = () => 0.0;

        public string Label { get; set; } = "SingleHyperQ";

        /// <summary>
        /// Constructor for the Q. You must define the actions for the Q and then a function to act as
        /// the lazy initializer of action data when new states are added. The default is to set the 
        /// action Q values to zero.
        /// </summary>
        /// <param name="nActions">The number of actions in the Q</param>
        /// <param name="defaultValueAction">The lazy action initializer</param>
        public SingleHyperQ(QActionSpace<int> actionSpace, Func<double> defaultValueAction = null)
        {
            FeatureGate.Require(HyperQFeatures.LearnerHyperQ);
            ActionSpace = actionSpace;
            if (defaultValueAction != null)
            {
                DefaultValueFunc = defaultValueAction;
            }
            Advantage = new QAdvantage();
        }


        public virtual uint MapState(QState<T> stateKey)
        {
            return _StateMap[stateKey.Enumerator];
        }
        public virtual Tuple<uint, uint> Shape { get { return new Tuple<uint, uint>(_StateMap.MappingCount, ActionSpace.NumberOfKnownActions); } }
        public virtual void Link(MemoryBackedHyperMapper<T> stateMap, QActionSpace<int> actionSpace)
        {
            _StateMap = stateMap;
            ActionSpace = actionSpace;
        }

        /// <summary>
        /// Returns the full action array across the MaxNumActions, including actions that were not
        /// visited in the given state.
        /// </summary>
        /// <param name="stateKey">The state in which to query for action</param>
        /// <returns></returns>
        public virtual double[] GetActionArray(QState<T> stateKey)
        {
            double[] r = new double[ActionSpace.MaximumNumberOfActions];
            for (int i = 0; i < r.Length; i++)
            {
                r[i] = GetValue(stateKey, i);
            }
            return (r);
        }

        public virtual double[] GetKnownActionArray(QState<T> stateKey)
        {
            double[] r = null;
            uint ix = _StateMap[stateKey.Enumerator];
            if (ix < _StateIndexStart)
            {
                r = new double[_Q.GetLength(1)];
                for (int i = 0; i < r.Length; i++)
                {
                    r[i] = _Q[ix, i];
                }
            }
            else if(_data != null)
            {
                ix -= _StateIndexStart;
                if (ix < _data.Count)
                {
                    r = new double[_data[(int)ix].Count];
                    int i = 0;
                    foreach (uint id in _data[(int)ix].Keys)
                    {
                        r[i] = _data[(int)ix][id];
                        i++;
                    }
                }
            }
            return (r);
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        /// <param name="from"></param>
        /// <param name="methodType"></param>
        /// <exception cref="NotImplementedException"></exception>
        public virtual void MergeInto(Q<QState<T>> from, EvalMethodType methodType)
        {
            throw new NotImplementedException();
        }

        public virtual double[,] AsMatrix
        {
            get
            {
                return (makeQ());
            }
        }
        private double[,] makeQ()
        {
            // Snapshot the row count and the action count once. Visualizers read this matrix from
            // another thread while training keeps adding rows; re-reading _data.Count inside the loop
            // would walk past the rows that were allocated and throw IndexOutOfRangeException.
            List<QRow> data = _data;
            int nrows = data != null ? data.Count : 0;
            uint maxActions = ActionSpace.MaximumNumberOfActions;
            double[,] qq = new double[_StateIndexStart + nrows, _ActionIndexStart + maxActions];
            // Copy the original Q
            if (_StateIndexStart > 0 && _Q != null)
            {
                int qrows = Math.Min(_Q.GetLength(0), (int)_StateIndexStart);
                int qcols = Math.Min(_Q.GetLength(1), qq.GetLength(1));
                for (int j = 0; j < qrows; j++)
                {
                    for (int i = 0; i < qcols; i++)
                    {
                        qq[j, i] = _Q[j, i];
                    }
                }
            }
            for (int j = 0; j < nrows; j++)
            {
                QRow row = data[j];
                for (uint i = 0; i < maxActions; i++)
                {
                    // The action rows may not have all of the mapped actions. When there is no action for that
                    // state (row), use the default value action.
                    double v;
                    if (row == null || !row.TryGetValue(i, out v))
                    {
                        v = DefaultValueFunc != null ? DefaultValueFunc() : 0.0;
                    }
                    qq[j + _StateIndexStart, i + _ActionIndexStart] = v;
                }
            }
            return (qq);
        }
        public virtual Q<QState<T>> Clone()
        {
            SingleHyperQ<T> newQ = new SingleHyperQ<T>(ActionSpace.Clone(), DefaultValueFunc);
            newQ._Q = makeQ();
            newQ._StateIndexStart = _StateIndexStart + (uint)(_data != null ? _data.Count : 0);
            newQ._ActionIndexStart = _ActionIndexStart + ActionSpace.MaximumNumberOfActions;
            newQ._StateMap = _StateMap.Clone();
            return (newQ);
        }

        /// <summary>
        /// Expands the data list for the given state index so that the given action exists. Backsfills
        /// the unknown actions with DefaultValueAction() return values.
        /// </summary>
        /// <param name="stateIndex">The state index to work in</param>
        /// <param name="actionIndex">The new action index</param>
        private void EnsureCapacity(uint stateIndex, uint actionIndex)
        {
            EnsureCapacity(stateIndex);
            if (!_data[(int)stateIndex].ContainsKey(actionIndex))
            {
                if (DefaultValueFunc != null)
                {
                    _data[(int)stateIndex][actionIndex] = DefaultValueFunc();
                }
            }
        }
        private void EnsureCapacity(uint stateIndex)
        {
            if (_data == null)
            {
                _data = new List<QRow>();
            }
            while (stateIndex >= _data.Count)
            {
                _data.Add(new QRow());
            }
        }
        public virtual bool IsKnownState(QState<T> stateKey)
        {
            return (_StateMap.Known(stateKey.Enumerator));
        }
        internal bool IsKnownState(uint stateIndex)
        {
            // Check the Q matrix
            if (stateIndex < _StateIndexStart)
            {
                return (true);
            }
            // Check the data list
            if (_data != null && (_data.Count > stateIndex - _StateIndexStart))
            {
                return (true);
            }
            // Not found in either, so it's not known
            return (false);
        }

        public virtual bool IsKnownAction(int action)
        {
            return (ActionSpace.IsKnownAction(action));
        }

        public virtual bool RemoveState(QState<T> stateKey)
        {
            return (_StateMap.RemoveKey(stateKey.Enumerator));
        }
        public virtual uint AddState(QState<T> stateKey)
        {
            uint ix = _StateMap[stateKey.Enumerator];
            if (ix < _StateIndexStart)
            {
                return(ix - _StateIndexStart);
            }
            EnsureCapacity(ix);
            return (ix);
        }

        /// <summary>
        /// Return the Q(s,a) value for the given state and action.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public virtual double GetValue(QState<T> stateKey, int action)
        {
            uint ix = _StateMap[stateKey.Enumerator];
            uint ax = ActionSpace.ToIndex(action);
            if (ix < _StateIndexStart && ax < _ActionIndexStart)
            {
                return (_Q[ix, ax]);
            }
            ix -= _StateIndexStart;
            ax -= _ActionIndexStart;
            EnsureCapacity(ix, ax);
#if VERBOSE
            Console.WriteLine("{3} GetValue {0}:{1}={2}", ix, ax, _data[ix][ax], Label);
#endif
            if (_data[(int)ix].ContainsKey(ax))
            {
                return (_data[(int)ix][ax]);
            }
            if (DefaultValueFunc != null)
            {
                return(DefaultValueFunc());
            }
            return (0.0);// No bueno, arbitrary value that is not realistic in most situations
        }

        protected virtual void UpdateArgMinMax(QState<T> stateKey)
        {
            // Determine the min and max
            uint ix = _StateMap[stateKey.Enumerator];
            QArg<double> arg = new QArg<double>(0.0,-1);
            if (ix < _StateIndexStart)
            {
                for (uint i = 0; i < _Q.GetLength(1); i++)
                {
                    int a = ActionSpace.FromIndex(i);
                    arg.Set(_Q[(int)ix, (int)i], a);
                }
            }
            ix -= _StateIndexStart;
            foreach (uint i in _data[(int)ix].Keys)
            {
                int a = ActionSpace.FromIndex(i+_ActionIndexStart);
                arg.Set(_data[(int)ix][i], a);
            }
            _StateArgs[ix] = arg;
        }

        public virtual void SetValue(QState<T> stateKey, int action, double v)
        {
            bool updateMinMax = false;
            uint ix = _StateMap[stateKey.Enumerator];
            if (_StateArgs.ContainsKey(ix))
            {
                if (_StateArgs[ix].Set(v, action))
                {
                    updateMinMax = true;
                }
            }
            else
            {
                _StateArgs[ix] = new QArg<double>(v, action);
            }
            uint ax = ActionSpace.ToIndex(action);
            if (ix < _StateIndexStart && ax < _ActionIndexStart)
            {
                _Q[ix, ax] = v;
#if VERBOSE
                Console.WriteLine("{3} SetValue {0}:{1}={2}", ix, ax, _Q[ix,action],Label);
#endif
            }
            else
            {
                ix -= _StateIndexStart;
                ax -= _ActionIndexStart;
                EnsureCapacity(ix, ax);
                _data[(int)ix][ax] = v;
#if VERBOSE
                Console.WriteLine("{3} SetValue {0}:{1}={2}", ix, ax, _data[ix][ax],Label);
#endif
            }
            if(updateMinMax)
                UpdateArgMinMax(stateKey);
        }

        public virtual IEnumerable<KeyValuePair<uint, double>> KnownActionValues(QState<T> stateKey)
        {
            if (!_StateMap.Known(stateKey.Enumerator))
            {
                return Enumerable.Empty<KeyValuePair<uint, double>>();
            }
            return KnownActionValuesAt(_StateMap[stateKey.Enumerator]);
        }

        /// <summary>
        /// Enumerates the (action index, Q value) pairs recorded for the given state index. The keys are
        /// action-space indexes (what <see cref="QActionSpace{T}.ToIndex"/> returns), so the enumeration
        /// position must never be used as the action index: rows fill in the order actions are first tried.
        /// </summary>
        /// <param name="ix">The state index in the state map</param>
        internal IEnumerable<KeyValuePair<uint, double>> KnownActionValuesAt(uint ix)
        {
            if (ix < _StateIndexStart)
            {
                if (_Q != null)
                {
                    for (uint i = 0; i < _Q.GetLength(1); i++)
                    {
                        yield return new KeyValuePair<uint, double>(i, _Q[ix, i]);
                    }
                }
                yield break;
            }
            ix -= _StateIndexStart;
            List<QRow> data = _data;
            if (data == null || ix >= data.Count)
            {
                yield break;
            }
            QRow row = data[(int)ix];
            if (row == null)
            {
                yield break;
            }
            foreach (KeyValuePair<uint, double> kv in row)
            {
                yield return new KeyValuePair<uint, double>(kv.Key + _ActionIndexStart, kv.Value);
            }
        }

        /// <summary>
        /// Returns the cached min/max for the state, computing it from every known (action, value) pair when
        /// the cache has no entry. Returns null when nothing is known about the state.
        /// </summary>
        private QArg<double> StateArgsFor(uint ix)
        {
            QArg<double> arg;
            if (_StateArgs.TryGetValue(ix, out arg))
            {
                return arg;
            }
            arg = new QArg<double>();
            bool found = false;
            foreach (KeyValuePair<uint, double> kv in KnownActionValuesAt(ix))
            {
                arg.Set(kv.Value, ActionSpace.FromIndex(kv.Key));
                found = true;
            }
            if (!found)
            {
                return null;
            }
            _StateArgs[ix] = arg;
            return arg;
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
            QArg<double> arg = StateArgsFor(ix);
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
            QArg<double> arg = StateArgsFor(ix);
            if (arg == null)
                return null;
            return new QAction(arg.MinIndex, arg.Min, ActionSpace.ToIndex(arg.MinIndex));
        }


        public virtual double OnPolicyUpdate(QState<T> s, int a, QState<T> sprime, int aprime, double r, HyperParams hp)
        {
            AddState(s);
            AddState(sprime);
            double prime_v = this[sprime, aprime];
            double curr_v = this[s, a];
            double baseline = QUpdateCore.Baseline(GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            this[s, a] = new_v;
            return new_v;
        }
        public virtual double OffPolicyUpdate(QState<T> s, int a, QState<T> sprime, double r, HyperParams hp, EvalMethodType evalType = EvalMethodType.Max)
        {
            AddState(s);
            AddState(sprime);
            int aprime = QUpdateCore.SelectGreedyAction(sprime, a, evalType, ArgMax, ArgMin);
            double prime_v = this[sprime, aprime];
            double curr_v = this[s, a];
            double baseline = QUpdateCore.Baseline(GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            this[s, a] = new_v;
            return new_v;
        }

        /// <summary>
        /// uses the GetValue and SetValue methods to manage the tabular value for the given (s,a) combination.
        /// </summary>
        /// <param name="state">The state of the environment</param>
        /// <param name="action">The action, in action space</param>
        /// <returns></returns>
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
        public virtual void Dump(string label = null, int level = 0, bool dumpData = false)
        {
            Console.WriteLine("=={0}==", label??Label);
            if (dumpData)
            {
                Console.WriteLine(_Q);
                Console.WriteLine("==data==");
                if (_data != null)
                {
                    int iState = 0;
                    foreach (QRow d in _data)
                    {
                        Console.Write("{0}:", iState);
                        foreach(int id in d.Keys) 
                        {
                            Console.Write("{0:g3}", id);
                            Console.Write(",");
                        }
                        Console.WriteLine();
                        iState++;
                    }
                }
                else
                {
                    Console.WriteLine("empty");
                }
                Console.WriteLine();
            }
            Console.WriteLine("Num Actions={0}/{3}, Num States={4} state offset={1}, action offset={2}", ActionSpace.MaximumNumberOfActions, _StateIndexStart, _ActionIndexStart, ActionSpace.NumberOfKnownActions, _StateMap.MappingCount);
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

        // ── ICheckpointable ──

        /// <summary>
        /// Serializer for the elements of the composite state keys in checkpoints; set it for an element type
        /// without a built-in serializer (see <see cref="QKeySerializerFactory"/>).
        /// </summary>
        public virtual IQKeySerializer<T> KeySerializer { get; set; }

        private IQKeySerializer<T> Keys
        {
            get { return KeySerializer ?? (KeySerializer = QKeySerializerFactory.GetDefault<T>()); }
        }

        public int CheckpointVersion { get { return 1; } }

        public virtual void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            CheckpointIO.WriteActionSpace(writer, ActionSpace);
            _StateMap.SaveCheckpoint(writer, Keys);
            writer.Write(_StateIndexStart);
            writer.Write(_ActionIndexStart);
            CheckpointIO.WriteMatrix(writer, _Q);
            CheckpointIO.WriteRows(writer, _data);
        }

        public virtual void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            CheckpointIO.ReadActionSpace(reader, ActionSpace);
            _StateMap.LoadCheckpoint(reader, Keys);
            _StateIndexStart = reader.ReadUInt32();
            _ActionIndexStart = reader.ReadUInt32();
            _Q = CheckpointIO.ReadMatrix(reader);
            _data = CheckpointIO.ReadRows(reader);
            _StateArgs.Clear();
        }
    }
}
