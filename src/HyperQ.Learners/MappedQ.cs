using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;
using System.IO;

namespace HyperQ.Learners
{
    /// <summary>
    /// Maps action "index" (index space) to values. The index is not the problem's action value.
    /// </summary>
    public class QRow : Dictionary<uint, double>
    {
    }

    [Serializable]
    public class MappedQ<T> : BaseQ<T>, ICheckpointable
    {
        /// <summary>
        /// The matrix version of the Q data
        /// </summary>
        private double[,] _Q = null;
        /// <summary>
        /// The adaptive Q data
        /// </summary>
        private List<QRow> _data = null;
        /// <summary>
        /// Data that overlaps the state index in _Q but action index is beyond the Q column rank
        /// </summary>
        private List<QRow> _overlapData = null;
        /// <summary>
        /// Maps a state key to its index in the '_data' list. _data[_stateMap[key]][_ActionMap[action]] = Q(s,a)
        /// </summary>
        private IIndexMapper<T> _StateMap = new IndexMapper<T>(0);
        /// <summary>
        /// Maps an action key to its in-list index of '_data'. _data[_stateMap[key]][_ActionMap[action]] = Q(s,a)
        /// </summary>
        private Dictionary<T, QArg<double>> _StateArgs = new Dictionary<T, QArg<double>>();
        private uint _StateIndexStart = 0;
        private uint _ActionIndexStart = 0;
        public override Tuple<uint, uint> Shape { get { return new Tuple<uint, uint>(_StateMap.MappingCount, ActionSpace.NumberOfKnownActions); } }

        /// <summary>
        /// Constructor for the Q. You must define the actions for the Q and then a function to act as
        /// the lazy initializer of action data when new states are added. The default is to set the 
        /// action Q values to zero.
        /// </summary>
        /// <param name="nActions">The number of actions in the Q</param>
        /// <param name="defaultValueAction">The lazy action initializer</param>
        public MappedQ(QActionSpace<int> actionSpace, Func<double> defaultValueAction = null) : base(actionSpace)
        {
            if (defaultValueAction != null)
            {
               DefaultValueFunc = defaultValueAction;
            }
        }

        public MappedQ(QActionSpace<int> actionSpace, IndexMapper<T> stateMapper, Func<double> defaultValueAction = null) : base(actionSpace)
        {
            if (defaultValueAction != null)
            {
                DefaultValueFunc = defaultValueAction;
            }
            _StateMap = stateMapper;
        }

        /// <summary>
        /// Returns the Q matrix in 2D format.
        /// </summary>
        public override double[,] AsMatrix
        {
            get
            {
                return (makeQ());
            }
        }
        public override uint MapState(T stateKey)
        {
            return _StateMap[stateKey];
        }
        private double[,] makeQ()
        {
            // Snapshot the row and action counts once. Visualizers read this matrix from another thread
            // while training keeps adding states and actions; re-reading the live counts inside the loops
            // would index past the allocated matrix and throw IndexOutOfRangeException.
            List<QRow> data = _data;
            List<QRow> overlap = _overlapData;
            int nrows = data != null ? data.Count : 0;
            uint known = ActionSpace.NumberOfKnownActions;
            double[,] qq = new double[nrows + _StateIndexStart, known + _ActionIndexStart];
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
                for (uint i = 0; i < known; i++)
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
            // Inject the overlap
            if (overlap != null)
            {
                int orows = Math.Min(overlap.Count, (int)_StateIndexStart);
                for (int j = 0; j < orows; j++)
                {
                    QRow row = overlap[j];
                    for (uint i = 0; i < known; i++)
                    {
                        // The action rows may not have all of the mapped actions. When there is no action for that
                        // state (row), use the default value action.
                        double v;
                        if (row == null || !row.TryGetValue(i, out v))
                        {
                            v = DefaultValueFunc != null ? DefaultValueFunc() : 0.0;
                        }
                        qq[j, i + _ActionIndexStart] = v;
                    }
                }
            }
            return (qq);
        }

        /// <summary>
        /// Returns a copy of the actions for the given state key. The ordering of the array is not conserved.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns></returns>
        public override double[] GetActionArray(T stateKey)
        {
            // Snapshot the known action count once so the array and the loops below always agree, even
            // when another thread is adding actions to the shared action space.
            uint known = ActionSpace.NumberOfKnownActions;
            double[] qq = new double[known + _ActionIndexStart];
            if (!_StateMap.Known(stateKey))
            {
                for (int i = 0; i < known; i++)
                {
                    qq[i] = DefaultValueFunc();
                }
            }
            else
            {
                uint ix = _StateMap[stateKey];
                // Copy the original Q
                if (ix < _StateIndexStart)
                {
                    for (int a = 0; a < _Q.GetLength(1); a++)
                        qq[a] = _Q[ix, a];
                    if (_overlapData != null && _overlapData.Count > ix)
                    {
                        for (uint i = 0; i < known; i++)
                        {
                            // The action rows may not have all of the mapped actions. When there is no action for that
                            // state (row), use the default value action.
                            QRow rowx = _overlapData[(int)ix];
                            double v = 0.0;
                            if (!rowx.ContainsKey(i))
                            {
                                if (DefaultValueFunc != null)
                                {
                                    v = DefaultValueFunc();
                                }
                                else
                                {
                                    v = 0.0;
                                }
                            }
                            else
                            {
                                v = rowx[i];
                            }
                            qq[i + _ActionIndexStart] = v;
                        }
                    }
                }
                else
                {
                    ix -= _StateIndexStart;
                    QRow row = _data[(int)ix];
                    for (uint i = 0; i < known; i++)
                    {
                        // The action rows may not have all of the mapped actions. When there is no action for that
                        // state (row), use the default value action.
                        double v = 0.0;
                        if (!row.ContainsKey(i))
                        {
                            if (DefaultValueFunc != null)
                            {
                                v = DefaultValueFunc();
                            }
                            else
                            {
                                v = 0.0;
                            }
                        }
                        else
                        {
                            v = row[i];
                        }
                        qq[i] = v;
                    }
                }
            }
            return (qq);
        }
        public override Q<T> Clone()
        {
            MappedQ<T> newQ = new MappedQ<T>(ActionSpace.Clone(), DefaultValueFunc);
            newQ._Q = makeQ();
            newQ._StateIndexStart = _StateIndexStart + (uint)_data.Count;
            newQ._ActionIndexStart = _ActionIndexStart + ActionSpace.NumberOfKnownActions;
            newQ._StateMap = _StateMap.Clone();
            newQ._StateArgs = new Dictionary<T, QArg<double>>(_StateArgs);
            return (newQ);
        }

        private List<QRow> EnsureCapacity(uint stateIndex, uint actionIndex, List<QRow> data = null)
        {
            if(data == null)
            {
                data = new List<QRow>();
            }
            while (stateIndex >= data.Count)
            {
                data.Add(new QRow());
            }
            if (actionIndex >= 0)
            {
                if (!data[(int)stateIndex].ContainsKey(actionIndex))
                {
                    if (DefaultValueFunc != null)
                    {
                        data[(int)stateIndex][actionIndex] = DefaultValueFunc();
                    }
                }
            }
            return data;
        }
        private List<QRow> EnsureCapacity(uint stateIndex, List<QRow> data = null)
        {
            if (data == null)
            {
                data = new List<QRow>();
            }
            while (stateIndex >= data.Count)
            {
                data.Add(new QRow());
            }
            return data;
        }
        public virtual bool IsKnownState(T stateKey)
        {
            if (_StateMap.Known(stateKey))
            {
                uint ix = _StateMap[stateKey];
                if (ix < _StateIndexStart)
                {
                    return true;
                }
                if (ix >= _data.Count || ix < 0)
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        public virtual bool IsKnownAction(int action)
        {
            return (ActionSpace.IsKnownAction(action));
        }

        public override bool RemoveState(T stateKey)
        {
            return (_StateMap.RemoveKey(stateKey));
        }
        public override uint AddState(T stateKey)
        {
            if (_data == null)
            {
                _data = new List<QRow>();
            }
            uint ix = _StateMap[stateKey] - _StateIndexStart;
            _data = EnsureCapacity(ix,_data);
            return (ix);
        }

        /// <summary>
        /// Return the Q(s,a) value for the given state and action.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public override double GetValue(T stateKey, int action)
        {
            uint ix = _StateMap[stateKey];
            uint ax = ActionSpace.ToIndex(action);
            if (ix < _StateIndexStart && ax < _ActionIndexStart)
            {
                return (_Q[ix, ax]);
            }
            if (ix < _StateIndexStart)
            {
                if (_overlapData == null)
                {
                    _overlapData = new List<QRow>();
                }
                ax -= _ActionIndexStart;
                _overlapData = EnsureCapacity(ix, ax, _overlapData);
                return _overlapData[(int)ix][ax];
            }
            if (_data == null)
            {
                _data = new List<QRow>();
            }
            ix -= _StateIndexStart;
            ax -= _ActionIndexStart;
            _data = EnsureCapacity(ix, ax, _data);
            return (_data[(int)ix][ax]);
        }

        protected virtual void UpdateArgMinMax(T state)
        {
            // Determine the min and max
            uint ix = _StateMap[state];
            QArg<double> arg = new QArg<double>(0.0, -1);
            if (ix < _StateIndexStart)
            {
                for (uint i = 0; i < _Q.GetLength(1); i++)
                {
                    int a = ActionSpace.FromIndex(i);
                    arg.Set(_Q[ix, i], a);
                }
                if (_overlapData != null)
                {
                    foreach (uint i in _overlapData[(int)ix].Keys)
                    {
                        int a = ActionSpace.FromIndex(i + _ActionIndexStart);
                        arg.Set(_overlapData[(int)ix][i], a);
                    }
                }
            }
            ix -= _StateIndexStart;
            foreach (uint i in _data[(int)ix].Keys)
            {
                int a = ActionSpace.FromIndex(i + _ActionIndexStart);
                arg.Set(_data[(int)ix][i], a);
            }
            _StateArgs[state] = arg;
        }

        /// <summary>
        /// Set the Q(s,a) value for the given problem space.
        /// </summary>
        /// <param name="stateKey">The state, in problem space, in which the action is taken</param>
        /// <param name="action">The action, in problem space</param>
        /// <param name="v">The Q return value for this (s,a)</param>
        public override void SetValue(T stateKey, int action, double v)
        {
            bool bUpdateArgMinMax = false;
            if (!_StateArgs.ContainsKey(stateKey))
            {
                _StateArgs[stateKey] = new QArg<double>(v, action);
            }
            else
            {
                bUpdateArgMinMax = _StateArgs[stateKey].Set(v, action);
            }
            // Convert the (s,a) into Q coordinates
            uint ix = _StateMap[stateKey];
            uint ax = ActionSpace.ToIndex(action);
            if (ix < _StateIndexStart && ax < _ActionIndexStart)
            {
                // In the cached _Q matrix, no reason to offset, just look it up
                _Q[ix, ax] = v;
            }
            else
            {
                // In the lists, need to adjust for the _Q cache, if it exists
                if (ix < _StateIndexStart)
                {
                    // In the overlap region, applied to states visited, but actions that are new
                    if (_overlapData == null)
                    {
                        _overlapData = new List<QRow>();
                    }
                    ax -= _ActionIndexStart;
                    _overlapData = EnsureCapacity(ix, ax, _overlapData);
                    _overlapData[(int)ix][ax] = v;
                }
                else
                {
                    if (_data == null)
                    {
                        _data = new List<QRow>();
                    }
                    ix -= _StateIndexStart;
                    ax -= _ActionIndexStart;
                    _data = EnsureCapacity(ix, ax,_data);
                    _data[(int)ix][ax] = v;
                }
            }
            if (bUpdateArgMinMax)
                UpdateArgMinMax(stateKey);
        }

        /// <summary>
        /// Returns the action, in action space, with the max Q(s,a) value for the given state.
        /// </summary>
        /// <param name="stateKey">The state key that will map to a state index in the adaptive Q matrix</param>
        /// <returns>A tuple(action,Q) with the action index and the value of Q at the state/action combination</returns>
        public override QAction ArgMax(T stateKey)
        {
            if (_StateArgs.ContainsKey(stateKey))
            {
                QArg<double> arg = _StateArgs[stateKey];
                return new QAction(arg.MaxIndex, arg.Max, ActionSpace.ToIndex(arg.MaxIndex));
            }
            // Convert the (s,*) into Q coordinates
            uint ix = _StateMap[stateKey];
            double r = 0.0;
            int amax = -1;
            uint index = 0;
            bool found = false;
            if (ix < _StateIndexStart)
            {
                r = _Q[ix, 0];
                index = 0;
                for (uint i = 1; i < _Q.GetLength(1); i++)
                {
                    if (_Q[ix, i] > r)
                    {
                        index = i;
                        r = _Q[ix, i];
                    }
                }
                // Check the overlap
                if (ix < _overlapData.Count && _overlapData[(int)ix].Count > 0)
                {
                    foreach (uint j in _overlapData[(int)ix].Keys)
                    {
                        if (_overlapData[(int)ix][j] > r)
                        {
                            index = j+_ActionIndexStart;
                            r = _overlapData[(int)ix][j];
                            found = true;
                        }
                    }
                }
                // Demap the action to a bona fide action
                if (found)
                    amax = ActionSpace.FromIndex(index);
            }
            else
            {
                if (_data == null)
                {
                    _data = new List<QRow>();
                }
                ix -= _StateIndexStart;
                _data = EnsureCapacity(ix,_data);
                if (_data[(int)ix] == null || _data[(int)ix].Count == 0) {
                    return (null);
                }
                r = double.MinValue;
                index = 0;
                foreach (uint i in _data[(int)ix].Keys)
                {
                    if (_data[(int)ix][i] > r)
                    {
                        index = i;
                        r = _data[(int)ix][i];
                        found = true;
                    }
                }
                // Demap the action to a bona fide action
                if(found)
                    amax = ActionSpace.FromIndex(index);
            }
            if (found)
            {
                _StateArgs[stateKey] = new QArg<double>(r, amax);
                return (new QAction(amax, r,index));
            }
            return (null);
        }

        /// <summary>
        /// Returns the action, in action space, that has the minimum Q(s,a) value for the given state.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>A tuple(action,Q) with the action index and the value of Q at the state/action combination</returns>
        public override QAction ArgMin(T stateKey)
        {
            if (_StateArgs.ContainsKey(stateKey))
            {
                QArg<double> arg = _StateArgs[stateKey];
                return new QAction(arg.MinIndex, arg.Min, ActionSpace.ToIndex(arg.MinIndex));
            }
            // Convert the (s,*) into Q coordinates
            uint ix = _StateMap[stateKey];
            double r = 0.0;
            int amin = -1;
            uint index = 0;
            bool found = false;
            if (ix < _StateIndexStart)
            {
                r = _Q[ix, 0];
                index = 0;
                for (uint i = 1; i < _Q.GetLength(1); i++)
                {
                    if (_Q[ix, i] < r)
                    {
                        index = i;
                        r = _Q[ix, i];
                    }
                }
                // Check the overlap
                if (ix < _overlapData.Count && _overlapData[(int)ix].Count > 0)
                {
                    foreach(uint j in _overlapData[(int)ix].Keys)
                    {
                        if (_overlapData[(int)ix][j] < r)
                        {
                            index = j + _ActionIndexStart;
                            r = _overlapData[(int)ix][j];
                            found = true;
                        }
                    }
                }
                // Demap the action to a bona fide action
                if(found)
                    amin = ActionSpace.FromIndex(index);
            }
            else
            {
                if (_data == null)
                {
                    _data = new List<QRow>();
                }
                ix -= _StateIndexStart;
                _data = EnsureCapacity(ix, _data);
                if (_data[(int)ix] == null || _data[(int)ix].Count == 0)
                {
                    return (null);
                }
                r = double.MaxValue;
                index = 0;
                foreach(uint i in _data[(int)ix].Keys)
                {
                    if (_data[(int)ix][i] < r)
                    {
                        index = i;
                        r = _data[(int)ix][i];
                        found = true;
                    }
                }
                // Demap the action to a bona fide action in problem space
                if(found)
                    amin = ActionSpace.FromIndex(index);
            }
            if (found)
            {
                _StateArgs[stateKey] = new QArg<double>(r, amin);
                return (new QAction(amin, r,index));
            }
            return (null);
        }

        public override double OnPolicyUpdate(T s, int a, T sprime, int aprime, double r, HyperParams hp)
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
        public override double OffPolicyUpdate(T s, int a, T sprime, double r, HyperParams hp, EvalMethodType evalType = EvalMethodType.Max)
        {
            AddState(s);
            AddState(sprime);
            int aprime = a;
            QAction result = null;
            switch (evalType)
            {
                case EvalMethodType.Max:
                    result = ArgMax(sprime);
                    break;
                case EvalMethodType.Min:
                    result = ArgMin(sprime);
                    break;
                default:
                    result = ArgMax(sprime);
                    break;
            }
            if (result != null)
            {
                aprime = result.Item1;
            }
            double prime_v = this[sprime, aprime];
            double curr_v = this[s, a];
            double baseline = QUpdateCore.Baseline(GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            this[s, a] = new_v;
            return new_v;
        }

        public override double this[T state, int action]
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
        public override void Dump(string label = "Mapped Q", int level=0, bool dumpData = false)
        {
            Console.WriteLine("=={0}==", label);
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
                        foreach(uint i in d.Keys)
                        {
                            Console.Write("{0:g3}", d[i]);
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

        // ── ICheckpointable ──

        /// <summary>
        /// Serializer for the state keys in checkpoints; set it for a key type without a built-in serializer
        /// (see <see cref="QKeySerializerFactory"/>).
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
            CheckpointIO.WriteIndexMapper(writer, _StateMap, Keys);
            writer.Write(_StateIndexStart);
            writer.Write(_ActionIndexStart);
            CheckpointIO.WriteMatrix(writer, _Q);
            CheckpointIO.WriteRows(writer, _data);
            CheckpointIO.WriteRows(writer, _overlapData);
        }

        public virtual void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            CheckpointIO.ReadActionSpace(reader, ActionSpace);
            CheckpointIO.ReadIndexMapper(reader, _StateMap, Keys);
            _StateIndexStart = reader.ReadUInt32();
            _ActionIndexStart = reader.ReadUInt32();
            _Q = CheckpointIO.ReadMatrix(reader);
            _data = CheckpointIO.ReadRows(reader);
            _overlapData = CheckpointIO.ReadRows(reader);
            _StateArgs.Clear();
        }
    }
}
