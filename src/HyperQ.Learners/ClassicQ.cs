using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HyperQ.Learners
{
    [Serializable]
    public class ClassicQ : BaseQ<decimal>, ICheckpointable
    {
        /// <summary>
        /// The matrix version of the Q data
        /// </summary>
        private double[,] _Q = null;
        /// <summary>
        /// The adaptive Q data
        /// </summary>
        private List<double[]> _data = null;
        /// <summary>
        /// Maps a state key to its index in the '_data' list. _data[_stateMap[key]][action] = Q(s,a)
        /// </summary>
        private Dictionary<decimal, uint> _stateMap = new Dictionary<decimal, uint>();
        private Dictionary<uint,decimal> _stateIndex = new Dictionary<uint,decimal>();
        private uint _IndexOffset = 0;
        /// <summary>
        /// The list of free indices that result from deleting state keys.
        /// </summary>
        private List<uint> _FreeIndices = new List<uint>();
        /// <summary>
        /// Constructor for the Q. You must define the actions for the Q and then a function to act as
        /// the lazy initializer of action data when new states are added. The default is to set the 
        /// action Q values to zero.
        /// </summary>
        /// <param name="defaultValueFunc">The lazy action initializer</param>
        public ClassicQ(QActionSpace<int> actionSpace,Func<double> defaultValueFunc = null) : base(actionSpace)
        {
            if (defaultValueFunc != null)
            {
                DefaultValueFunc = defaultValueFunc;
            }
        }
        /// <summary>
        /// Returns the current shape of the Q 
        /// </summary>
        public override Tuple<uint, uint> Shape { get { return new Tuple<uint, uint>((uint)(_stateMap.Count + _IndexOffset), ActionSpace.MaximumNumberOfActions); } }

        public override double[,] AsMatrix
        {
            get
            {
                return (makeQ());
            }
        }
        private double[,] makeQ()
        {
            uint rows = _IndexOffset;
            if (_data != null)
                rows += (uint)_data.Count;
            double[,] qq = new double[rows, ActionSpace.MaximumNumberOfActions];
            // Copy the original Q
            if (_IndexOffset > 0)
            {
                for (int j = 0; j < _Q.GetLength(0); j++)
                {
                    for (int i = 0; i < _Q.GetLength(1); i++)
                    {
                        qq[j, i] = _Q[j, i];
                    }
                }
            }
            if (_data != null)
            {
                double[][] nd = _data.ToArray();
                for (int j = 0; j < nd.GetLength(0); j++)
                {
                    for (int i = 0; i < ActionSpace.MaximumNumberOfActions; i++)
                    {
                        qq[j + _IndexOffset, i] = nd[j][i];
                    }
                }
            }
            return (qq);
        }
        /// <summary>
        /// Returns a copy of the actions for the given state key.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns></returns>
        public override double[] GetActionArray(decimal stateKey)
        {
            double[] actionArray = new double[ActionSpace.MaximumNumberOfActions];
            if (!_stateMap.ContainsKey(stateKey))
            {
                AddState(stateKey);
            }
            uint ix = _stateMap[stateKey];
            if (ix < _IndexOffset)
            {
                for (int j = 0; j < ActionSpace.MaximumNumberOfActions; j++)
                {
                    actionArray[j] = _Q[ix, j];
                }
            }
            else
            {
                _data[(int)(ix - _IndexOffset)].CopyTo(actionArray,0);
            }
            return (actionArray);
        }
        /// <summary>
        /// Merge the Q data in 'from' into this Q. New states in 'from' are added to this Q. Existing state Q
        /// data is maxxed or minned depending on the argument passed.
        /// </summary>
        /// <param name="from">The source to be copied into this Q</param>
        /// <param name="methodType">The type of evaluation performed on conflicting states</param>
        public override void MergeInto(Q<decimal> from, EvalMethodType methodType = EvalMethodType.Max) 
        {
            ClassicQ bq = from as ClassicQ;
            if (bq == null)
            {
                // Not a BasicQ!
                throw (new ArgumentException("'from' must be a BasicQ for the merge to work."));
            }
            if (this.ActionSpace.MaximumNumberOfActions != bq.ActionSpace.MaximumNumberOfActions)
            {
                throw (new ArgumentException("Incompatible Q learners - must have the same number of actions."));
            }
            foreach (int key in bq._stateMap.Keys)
            {
                double[] from_actions = from.GetActionArray(key);
                uint this_ix = 0;
                // Do I have this state already?
                if (!_stateMap.ContainsKey(key))
                {
                    // New state to add
                    this_ix = AddState(key) - _IndexOffset;
                    from_actions.CopyTo(_data[(int)this_ix], 0);
                    continue;
                }
                // Get the action array for each Q
                this_ix = _stateMap[key];
                double[] this_actions = GetActionArray(key);
                double[] final = new double[ActionSpace.MaximumNumberOfActions];
                for (int i = 0; i < ActionSpace.MaximumNumberOfActions; i++)
                {
                    switch (methodType)
                    {
                        case EvalMethodType.Min:
                            final[i] = Math.Min(this_actions[i], from_actions[i]);
                            break;
                        case EvalMethodType.Max:
                            final[i] = Math.Max(this_actions[i], from_actions[i]);
                            break;
                        case EvalMethodType.Avg:
                            final[i] = 0.5 * (this_actions[i] + from_actions[i]);
                            break;
                    }
                }
                // Write the new actions back to this Q
                if (this_ix < _IndexOffset)
                {
                    // Copy it into the Q
                    for (int j = 0; j < ActionSpace.MaximumNumberOfActions; j++)
                    {
                        _Q[this_ix, j] = final[j];
                    }
                }
                else
                {
                    this_ix -= _IndexOffset;
                    final.CopyTo(_data[(int)this_ix], 0);
                }
            }
        }
        public override Q<decimal> Clone()
        {
            ClassicQ newQ = new ClassicQ(ActionSpace, DefaultValueFunc);
            newQ._Q = makeQ();
            newQ._IndexOffset = _IndexOffset + (uint)_data.Count;
            newQ._stateMap = new Dictionary<decimal, uint>(_stateMap);
            newQ._stateIndex = new Dictionary<uint, decimal>(_stateIndex);
            return (newQ);
        }

        public override bool RemoveState(decimal stateKey) {
            if (_stateMap.ContainsKey(stateKey))
            {
                uint ix = _stateMap[stateKey];
                _FreeIndices.Add(ix);
                _stateMap.Remove(stateKey);
                _stateIndex.Remove(ix);
                return (true);
            }
            return (false);
        }
        public override uint AddState(decimal stateKey)
        {
            if (_data == null)
            {
                _data = new List<double[]>();
            }
            if (_stateMap.ContainsKey(stateKey))
            {
                // Sanity check to prevent erroneous duplicates
                return (_stateMap[stateKey]);
            }
            double[] d = null;
            uint ix = 0;
            if (_FreeIndices.Count > 0) {
                ix = _FreeIndices[0];
                _FreeIndices.RemoveAt(0);

                d = _data[(int)(ix - _IndexOffset)];
            }
            else {
                d = new double[ActionSpace.MaximumNumberOfActions];
                _data.Add(d);
                ix = (uint)_data.Count - 1 + _IndexOffset;
            }
            for (int i = 0; i < ActionSpace.MaximumNumberOfActions; i++)
            {
                if (DefaultValueFunc != null)
                {
                    d[i] = DefaultValueFunc();
                }
                else
                {
                    d[i] = 0.0;
                }
            }
            _stateMap[stateKey] = ix;
            _stateIndex[ix] = stateKey;
            return (ix);
        }

        /// <summary>
        /// Return the Q(s,a) value for the given state and action.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public override double GetValue(decimal stateKey, int action)
        {
            if (!_stateMap.ContainsKey(stateKey))
            {
                return DefaultValueFunc != null ? DefaultValueFunc() : 0.0;
            }
            uint ix = _stateMap[stateKey];
            if (ix < _IndexOffset)
            {
                return (_Q[ix, action]);
            }
            ix -= _IndexOffset;
            return (_data[(int)ix][action]);
        }

        public override void SetValue(decimal stateKey, int action, double v)
        {
            uint ix = 0;
            ix = AddState(stateKey);
            if (ix < _IndexOffset)
            {
                _Q[ix, action] = v;
            }
            else
            {
                ix -= _IndexOffset;
                _data[(int)ix][action] = v;
            }
        }

        /// <summary>
        /// Returns the action with the max Q(s,a) value for the given state.
        /// </summary>
        /// <param name="stateKey">The state key that will map to a state index in the adaptive Q matrix</param>
        /// <returns>A tuple(action,Q) with the action index and the value of Q at the state/action combination</returns>
        public override QAction ArgMax(decimal stateKey)
        {
            double[] actions = GetActionArray(stateKey);
            // Console.WriteLine("(ArgMax) {0}:{1}", stateKey, string.Join(",", actions));
            double r = actions[0];
            int imax = 0;
            for (int i = 1; i < actions.Length; i++)
            {
                if (actions[i] > r)
                {
                    r = actions[i];
                    imax = i;
                }
            }
            return (new QAction(imax, r, (uint)imax));
        }

        /// <summary>
        /// Returns the action that has the minimum Q(s,a) value for the given state.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>A tuple(action,Q) with the action index and the value of Q at the state/action combination</returns>
        public override QAction ArgMin(decimal stateKey)
        {
            double[] actions = GetActionArray(stateKey);
            // Console.WriteLine("(ArgMin) {0}:{1}", stateKey, string.Join(",",actions));
            double r = actions[0];
            int imin = 0;
            for (int i = 1; i < actions.Length; i++)
            {
                if (actions[i] < r)
                {
                    r = actions[i];
                    imin = i;
                }
            }
            return (new QAction(imin,r, (uint)imin));
        }

        private Tuple<decimal, uint> _lastStateQuery = null;
        public override uint MapState(decimal stateKey)
        {
            if (_lastStateQuery != null && _lastStateQuery.Item1 == stateKey)
                return _lastStateQuery.Item2;
            uint ix = 0;
            if (!_stateMap.ContainsKey(stateKey))
                ix = AddState(stateKey);
            else 
                ix = _stateMap[stateKey];
            _lastStateQuery = new Tuple<decimal, uint>(stateKey, ix);
            return ix;
        }
        public override double OnPolicyUpdate(decimal s, int a, decimal sprime, int aprime, double r, HyperParams hp)
        {
            double prime_v = GetValue(sprime, aprime);
            double curr_v = GetValue(s, a);
            double baseline = QUpdateCore.Baseline(GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            SetValue(s, a, new_v);
            return new_v;
        }
        public override double OffPolicyUpdate(decimal s, int a, decimal sprime, double r, HyperParams hp, EvalMethodType evalType = EvalMethodType.Max)
        {
            int aprime = QUpdateCore.SelectGreedyAction(sprime, a, evalType, ArgMax, ArgMin);
            double prime_v = GetValue(sprime, aprime);
            double curr_v = GetValue(s, a);
            double baseline = QUpdateCore.Baseline(GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            SetValue(s, a, new_v);
            return new_v;
        }

        public override double this[decimal state, int action]
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
        public override void Dump(string label="Classic Q", int level = 0, bool dumpData = false)
        {
            Console.WriteLine("=={0}==",label);
            if (dumpData)
            {
                Console.WriteLine(_Q);
                Console.WriteLine("==data==");
                if (_data != null)
                {
                    uint iState = 0;
                    foreach (double[] d in _data)
                    {
                        if (_stateIndex.ContainsKey(iState))
                        {
                            Console.Write("{0}({1}):", iState + _IndexOffset, _stateIndex[iState]);
                        }
                        for (int i = 0; i < d.Length; i++)
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
            Console.WriteLine("Num Actions={0}, offset={1}", ActionSpace.MaximumNumberOfActions,_IndexOffset);
        }

        // ── ICheckpointable ──

        public int CheckpointVersion => 1;

        public void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            writer.Write((int)ActionSpace.MaximumNumberOfActions);

            // State map: count + entries
            writer.Write(_stateMap.Count);
            foreach (var kvp in _stateMap)
            {
                writer.Write(kvp.Key);  // decimal state key
                writer.Write(kvp.Value); // uint index
            }

            // Index offset (for legacy matrix Q)
            writer.Write(_IndexOffset);

            // Legacy matrix Q (if any)
            if (_Q != null && _IndexOffset > 0)
            {
                writer.Write(true);
                int rows = _Q.GetLength(0);
                int cols = _Q.GetLength(1);
                writer.Write(rows);
                writer.Write(cols);
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        writer.Write(_Q[r, c]);
            }
            else
            {
                writer.Write(false);
            }

            // Adaptive data
            if (_data != null)
            {
                writer.Write(_data.Count);
                foreach (var row in _data)
                {
                    writer.Write(row.Length);
                    for (int i = 0; i < row.Length; i++)
                        writer.Write(row[i]);
                }
            }
            else
            {
                writer.Write(0);
            }

            // Free indices
            writer.Write(_FreeIndices.Count);
            foreach (uint fi in _FreeIndices)
                writer.Write(fi);
        }

        public void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            int actionCount = reader.ReadInt32();

            // State map
            int stateCount = reader.ReadInt32();
            _stateMap.Clear();
            _stateIndex.Clear();
            for (int i = 0; i < stateCount; i++)
            {
                decimal key = reader.ReadDecimal();
                uint idx = reader.ReadUInt32();
                _stateMap[key] = idx;
                _stateIndex[idx] = key;
            }

            // Index offset
            _IndexOffset = reader.ReadUInt32();

            // Legacy matrix Q
            bool hasMatrix = reader.ReadBoolean();
            if (hasMatrix)
            {
                int rows = reader.ReadInt32();
                int cols = reader.ReadInt32();
                _Q = new double[rows, cols];
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        _Q[r, c] = reader.ReadDouble();
            }
            else
            {
                _Q = null;
            }

            // Adaptive data
            int dataCount = reader.ReadInt32();
            if (dataCount > 0)
            {
                _data = new List<double[]>(dataCount);
                for (int i = 0; i < dataCount; i++)
                {
                    int len = reader.ReadInt32();
                    double[] row = new double[len];
                    for (int j = 0; j < len; j++)
                        row[j] = reader.ReadDouble();
                    _data.Add(row);
                }
            }
            else
            {
                _data = null;
            }

            // Free indices
            int freeCount = reader.ReadInt32();
            _FreeIndices.Clear();
            for (int i = 0; i < freeCount; i++)
                _FreeIndices.Add(reader.ReadUInt32());
        }
    }
}
