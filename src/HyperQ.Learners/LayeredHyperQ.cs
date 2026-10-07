using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using HyperQ.Util;
using System.IO;
using HyperQ.Util.Licensing;

namespace HyperQ.Learners
{
    /// <summary>
    /// Controls how reward signals propagate across layers in a LayeredHyperQ.
    /// </summary>
    public enum LayerUpdateMode
    {
        /// <summary>Each coarser layer receives r × τ^k (original behavior).</summary>
        ScaledLayerUpdates,
        /// <summary>Every layer receives the full reward r.</summary>
        UnscaledLayerUpdates,
        /// <summary>Finest layer gets r; coarser layers receive the TD advantage from the finest layer.</summary>
        AdvantageLayerUpdates,
        /// <summary>Every layer gets full r, and new fine states are warm-started from coarser layer values.</summary>
        CoarseToFineLayerUpdates
    }

    /// <summary>
    /// The layered hyperQ implementation. Provide a generator to create Q layers for each element in 
    /// the state vector.
    /// </summary>
    [Serializable]
    public class LayeredHyperQ<T> : IHyperQ<T>, ICheckpointable
    {
        private QActionSpace<int> _actionSpace;
        public QActionSpace<int> ActionSpace
        {
            get
            {
                return _actionSpace;
            }
        }
        private List<IHyperQ<T>> _Layers = new List<IHyperQ<T>>();
        /// <summary>The layers, coarsest first: layer i holds the prefixes of length i + 1. They are created as longer states arrive.</summary>
        internal IReadOnlyList<IHyperQ<T>> LayerList { get { return _Layers; } }
        private Func<IHyperQ<T>> _Generator = null;
        private bool _GlobalQuery = false;
        /// <summary>
        /// Controls how reward signals propagate to coarser layers during updates.
        /// </summary>
        public LayerUpdateMode UpdateMode { get; set; } = LayerUpdateMode.ScaledLayerUpdates;
        /// <summary>
        /// The action used to get a default value for a (s,a) pair.
        /// </summary>
        public Func<double> DefaultValueFunc { get; set; } = () => 0.0;
        public string Label { get; set; } = "LayeredHyperQ";
        /// <summary>
        /// Registry of the complete states this learner has seen (and the shared index space of double-Q
        /// layers). Like every mapper it draws its indexes from a repository of its own, so registering a
        /// state here never disturbs the index sequence of the layers.
        /// </summary>
        private MemoryBackedHyperMapper<T> _StateMap = new MemoryBackedHyperMapper<T>();
        public virtual bool RequiresIndexRepositoryLinking { get { return false; } }
        public QAdvantage Advantage
        {
            get
            {
                // TODO: Implement a smarter layered advantage
                if (_Layers.Count == 0) return null;
                return _Layers[_Layers.Count-1].Advantage;
            }
        }

        public LayeredHyperQ(Func<IHyperQ<T>> qGenerator, QActionSpace<int> actionSpace, Func<double> valueFunc = null, LayerUpdateMode updateMode = LayerUpdateMode.ScaledLayerUpdates)
        {
            FeatureGate.Require(HyperQFeatures.LearnerLayered);
            _actionSpace = actionSpace;
            _Generator = qGenerator;
            UpdateMode = updateMode;
            if (valueFunc != null)
            {
                DefaultValueFunc = valueFunc;
            }
        }
        /// <summary>
        /// Item1 is the number of complete states (one element per layer) the learner has seen, Item2 the
        /// number of known actions.
        /// </summary>
        public virtual Tuple<uint, uint> Shape { get { return new Tuple<uint, uint>(_StateMap.MappingCountAtDepth(_Layers.Count), ActionSpace.NumberOfKnownActions); } }
        public virtual uint MapState(QState<T> stateKey)
        {
            return _StateMap[stateKey.Enumerator];
        }

        public virtual void Link(MemoryBackedHyperMapper<T> stateMap, QActionSpace<int> actionSpace)
        {
            foreach (IHyperQ<T> l in _Layers)
            {
                l.Link(stateMap, actionSpace);
            }
            _StateMap = stateMap;
        }

        public double this[QState<T> state, int action] 
        {
            get
            {
                return (GetValue(state, action));
            }
            set
            {
                SetValue(state, action, value);
            } 
        }

        /// <summary>
        /// Returns the Q matrix of the complete states the learner has seen: one row per state, in the order
        /// returned by <see cref="KnownStates"/>, and one column per action-space index. A cell holds the value
        /// of the finest layer that has learned that action in that state, so coarser layers fill in the actions
        /// the finest layer has not tried yet, and the default value where no layer has seen the action.
        /// Building the matrix does not modify any layer.
        /// </summary>
        public virtual double[,] AsMatrix
        {
            get
            {
                uint maxActions = ActionSpace.MaximumNumberOfActions;
                List<QState<T>> states = KnownStates();
                double[,] m = new double[states.Count, maxActions];
                bool[] filled = new bool[maxActions];
                for (int r = 0; r < states.Count; r++)
                {
                    Array.Clear(filled, 0, filled.Length);
                    foreach (KeyValuePair<uint, double> kv in KnownActionValues(states[r]))
                    {
                        if (kv.Key < maxActions)
                        {
                            m[r, kv.Key] = kv.Value;
                            filled[kv.Key] = true;
                        }
                    }
                    for (uint i = 0; i < maxActions; i++)
                    {
                        if (!filled[i])
                        {
                            m[r, i] = DefaultValueFunc != null ? DefaultValueFunc() : 0.0;
                        }
                    }
                }
                return m;
            }
        }

        /// <summary>
        /// Returns the complete states (one element per layer) the learner has seen, ordered by state index.
        /// This is the row order of <see cref="AsMatrix"/>.
        /// </summary>
        public virtual List<QState<T>> KnownStates()
        {
            int depth = _Layers.Count;
            List<QState<T>> states = new List<QState<T>>();
            if (depth == 0)
            {
                return states;
            }
            foreach (KeyValuePair<QState<T>, uint> kv in _StateMap.Mappings().Where(kv => kv.Key.Count == depth).OrderBy(kv => kv.Value))
            {
                states.Add(kv.Key);
            }
            return states;
        }

        /// <summary>
        /// Per action index, the value of the finest layer that has learned the action for (its slice of) the
        /// state; coarser layers only contribute the actions the finer layers have not seen. Nothing is written.
        /// </summary>
        public virtual IEnumerable<KeyValuePair<uint, double>> KnownActionValues(QState<T> stateKey)
        {
            Dictionary<uint, double> values = new Dictionary<uint, double>();
            int depth = Math.Min(stateKey.Count, _Layers.Count);
            for (int i = depth - 1; i >= 0; i--)
            {
                QState<T> slice = stateKey.Slice(i + 1);
                if (!_Layers[i].IsKnownState(slice))
                {
                    continue;
                }
                foreach (KeyValuePair<uint, double> kv in _Layers[i].KnownActionValues(slice))
                {
                    if (!values.ContainsKey(kv.Key))
                    {
                        values[kv.Key] = kv.Value;
                    }
                }
            }
            return values.OrderBy(kv => kv.Key).ToList();
        }

        /// <summary>
        /// Creates layers until there is one per state element. New layers are linked to the shared state map
        /// when the layer type needs it (double-Q layers keep both tables on one index space).
        /// </summary>
        private void EnsureLayers(int count)
        {
            while (count > _Layers.Count)
            {
                IHyperQ<T> g = _Generator();
                _Layers.Add(g);
                if (g.RequiresIndexRepositoryLinking)
                    g.Link(_StateMap, ActionSpace);
                g.Label = string.Format("L{0}", _Layers.Count);
            }
        }

        public uint AddState(QState<T> stateKey)
        {
            uint r = 0;
            EnsureLayers(stateKey.Count);
            // CoarseToFine warm-starts every layer whose slice of the state is new. Ask the layers before the
            // complete state is registered below: layers linked to the shared state map (double-Q layers) answer
            // from that map, so the finest of them would otherwise always report the state as known.
            bool[] isNew = null;
            if (UpdateMode == LayerUpdateMode.CoarseToFineLayerUpdates)
            {
                isNew = new bool[_Layers.Count];
                for (int i = 1; i < _Layers.Count; i++)
                {
                    isNew[i] = !_Layers[i].IsKnownState(stateKey.Slice(i + 1));
                }
            }
            // Register the complete state so Shape, KnownStates and AsMatrix can enumerate it.
            _StateMap.MapState(stateKey.Enumerator);
            for (int i = 0; i < _Layers.Count; i++)
            {
                QState<T> slice = stateKey.Slice(i + 1);
                r = _Layers[i].AddState(slice);

                // CoarseToFine: a new fine state starts at the values its parent has learned. They are read by
                // action-space index without side effects on the parent, and written to every estimate of the
                // layer (both tables of a double-Q layer), so the new state reads them back whole.
                if (isNew != null && isNew[i])
                {
                    QState<T> coarserSlice = stateKey.Slice(i);
                    if (_Layers[i - 1].IsKnownState(coarserSlice))
                    {
                        foreach (KeyValuePair<uint, double> kv in _Layers[i - 1].KnownActionValues(coarserSlice).ToList())
                        {
                            _Layers[i].InitializeValue(slice, ActionSpace.FromIndex(kv.Key), kv.Value);
                        }
                    }
                }
            }
            return (r);
        }

        public QAction ArgMax(QState<T> stateKey)
        {
            // If the stateKey is not known to the layers, then find the argmax across 
            // the known layers.
            QAction result = null;
            for (int i = _Layers.Count-1; i >= 0; i--)
            {
                QState<T> s = stateKey.Slice(i + 1);
                if (_Layers[i].IsKnownState(s))
                {
                    QAction mx = _Layers[i].ArgMax(s);
                    if (!_GlobalQuery)
                    {
                        return (mx);
                    }
                    if (result == null || mx.Item2 > result.Item2)
                    {
                        result = mx;
                    }
                }
            }
            if (result == null)
            {
                // No argmax found, so pick randomly
                AddState(stateKey);
                return (_Layers[_Layers.Count - 1].ArgMax(stateKey));
            }
            return (result);
        }

        public QAction ArgMin(QState<T> stateKey)
        {
            // If the stateKey is not known to the layers, then find the argmin across 
            // the known layers.
            QAction result = null;
            for (int i = _Layers.Count - 1; i >= 0; i--)
            {
                QState<T> s = stateKey.Slice(i + 1);
                if (_Layers[i].IsKnownState(s))
                {
                    QAction mx = _Layers[i].ArgMin(s);
                    if (!_GlobalQuery)
                    {
                        return (mx);
                    }
                    if (result == null || mx.Item2 < result.Item2)
                    {
                        result = mx;
                    }
                }
            }
            if (result == null)
            {
                // No argmax found, so pick randomly
                AddState(stateKey);
                return (_Layers[_Layers.Count - 1].ArgMin(stateKey));
            }
            return (result);
        }

        public Q<QState<T>> Clone()
        {
            LayeredHyperQ<T> q = new LayeredHyperQ<T>(_Generator,_actionSpace);
            q._Layers = new List<IHyperQ<T>>();
            foreach (IHyperQ<T> qx in this._Layers)
            {
                q._Layers.Add((IHyperQ<T>)qx.Clone());
            }
            q.DefaultValueFunc = DefaultValueFunc;
            q._GlobalQuery = _GlobalQuery;
            q._StateMap = _StateMap;
            return (q);
        }

        public void Dump(string label = "LayeredHyperQ", int level = 0, bool dumpData = false)
        {
            Console.WriteLine("=== Layered Q Members ===");
            for (int i = 0; i < _Layers.Count; i++)
            {
                Console.WriteLine("{0}. LayeredHyperQ", i + 1);
                _Layers[i].Dump(level:level,dumpData:dumpData);
            }
        }

        public virtual double[] GetKnownActionArray(QState<T> stateKey)
        {
            QState<T> qs = stateKey.Clone();
            while (qs.Count > _Layers.Count)
            {
                qs.PopLast();
            }
            for (int i = _Layers.Count - 1; i >= 0; i--)
            {
                if (_Layers[i].IsKnownState(qs))
                {
                    // Query the layer with its own slice of the state, not the complete state.
                    return (_Layers[i].GetKnownActionArray(qs));
                }
                qs.PopLast();
            }
            return (GetActionArray(stateKey));
        }

        /// <summary>
        /// Returns the action arrays for each layer for the given state.
        /// Layer 0 corresponds to the first state element.
        /// </summary>
        /// <param name="stateKey">The state to query</param>
        /// <returns>List of action arrays, outermost layer first</returns>
        public virtual IList<double[]> GetLayerActionArrays(QState<T> stateKey)
        {
            List<double[]> r = new List<double[]>();
            QState<T> qs = stateKey.Clone();
            while (qs.Count > _Layers.Count)
            {
                qs.PopLast();
            }
            for (int i = 0; i < _Layers.Count; i++)
            {
                r.Add(_Layers[i].GetActionArray(stateKey.Slice(i + 1)));
            }
            return r;
        }

        public virtual double[] GetActionArray(QState<T> stateKey)
        {
            QMappedActionSpace<int> qmap = ActionSpace as QMappedActionSpace<int>;
            uint max = ActionSpace.MaximumNumberOfActions;
            double[] r = new double[max];
            for (uint i = 0; i < max; i++)
            {
                // A mapped space can only translate the indexes it has handed out. Other spaces translate every
                // index below their maximum, and GetValue answers the default for an action that is not known.
                if (qmap == null || qmap.IsValidIndex(i))
                    r[i] = this[stateKey, ActionSpace.FromIndex(i)];
                else if (DefaultValueFunc != null)
                    r[i] = DefaultValueFunc();
                else
                    r[i] = 0.0;
            }
            return (r);
        }

        public virtual double GetValue(QState<T> stateKey, int action)
        {
            QState<T> qs = stateKey.Clone();
            while (qs.Count > _Layers.Count)
            {
                qs.PopLast();
            }
            if (ActionSpace.IsKnownAction(action))
            {
                for (int i = _Layers.Count - 1; i >= 0; i--)
                {
                    if (_Layers[i].IsKnownState(qs) && _Layers[i].IsKnownAction(action))
                    {
                        return (_Layers[i][qs, action]);
                    }
                    qs.PopLast();
                }
            }
            if (DefaultValueFunc != null)
            {
                return (DefaultValueFunc());
            }
            return (0.0);
        }

        public void MergeInto(Q<QState<T>> from, EvalMethodType methodType = EvalMethodType.Max)
        {
            throw new NotImplementedException();
        }

        public double OffPolicyUpdate(QState<T> s, int a, QState<T> sprime, double r, HyperParams hp, EvalMethodType evalType)
        {
            AddState(s);
            AddState(sprime);
            double rx = 0.0;

            switch (UpdateMode)
            {
                case LayerUpdateMode.ScaledLayerUpdates:
                    for (int i = _Layers.Count - 1; i >= 0; i--)
                    {
                        double x = _Layers[i].OffPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), r, hp, evalType);
                        if (i == _Layers.Count - 1) rx = x;
                        r = r * hp.Tau;
                    }
                    break;

                case LayerUpdateMode.UnscaledLayerUpdates:
                case LayerUpdateMode.CoarseToFineLayerUpdates:
                    for (int i = _Layers.Count - 1; i >= 0; i--)
                    {
                        double x = _Layers[i].OffPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), r, hp, evalType);
                        if (i == _Layers.Count - 1) rx = x;
                    }
                    break;

                case LayerUpdateMode.AdvantageLayerUpdates:
                {
                    int finest = _Layers.Count - 1;
                    QState<T> finestS = s.Slice(finest + 1);
                    double curr_v = _Layers[finest].GetValue(finestS, a);
                    rx = _Layers[finest].OffPolicyUpdate(finestS, a, sprime.Slice(finest + 1), r, hp, evalType);
                    double advantage = rx - curr_v;
                    for (int i = finest - 1; i >= 0; i--)
                    {
                        _Layers[i].OffPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), advantage, hp, evalType);
                    }
                    break;
                }
            }

            return (rx);
        }

        public double OnPolicyUpdate(QState<T> s, int a, QState<T> sprime, int aprime, double r, HyperParams hp)
        {
            AddState(s);
            AddState(sprime);
            double rx = 0.0;

            switch (UpdateMode)
            {
                case LayerUpdateMode.ScaledLayerUpdates:
                    for (int i = _Layers.Count - 1; i >= 0; i--)
                    {
                        double x = _Layers[i].OnPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), aprime, r, hp);
                        if (i == _Layers.Count - 1) rx = x;
                        r = r * hp.Tau;
                    }
                    break;

                case LayerUpdateMode.UnscaledLayerUpdates:
                case LayerUpdateMode.CoarseToFineLayerUpdates:
                    for (int i = _Layers.Count - 1; i >= 0; i--)
                    {
                        double x = _Layers[i].OnPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), aprime, r, hp);
                        if (i == _Layers.Count - 1) rx = x;
                    }
                    break;

                case LayerUpdateMode.AdvantageLayerUpdates:
                {
                    int finest = _Layers.Count - 1;
                    QState<T> finestS = s.Slice(finest + 1);
                    double curr_v = _Layers[finest].GetValue(finestS, a);
                    rx = _Layers[finest].OnPolicyUpdate(finestS, a, sprime.Slice(finest + 1), aprime, r, hp);
                    double advantage = rx - curr_v;
                    for (int i = finest - 1; i >= 0; i--)
                    {
                        _Layers[i].OnPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), aprime, advantage, hp);
                    }
                    break;
                }
            }

            return (rx);
        }

        public bool RemoveState(QState<T> stateKey)
        {
            if (stateKey.Count == _Layers.Count)
            {
                bool removed = _Layers[_Layers.Count - 1].RemoveState(stateKey);
                _StateMap.RemoveKey(stateKey.Enumerator);
                return removed;
            }
            return (false);
        }

        public void SetValue(QState<T> stateKey, int action, double v)
        {
            WriteFinest(stateKey, (layer, qs) => layer[qs, action] = v);
        }

        /// <summary>
        /// Like <see cref="SetValue"/>, but writes through the layer's <see cref="IHyperQ{T}.InitializeValue"/>, so
        /// a double-Q layer holds the value in both of its tables.
        /// </summary>
        public void InitializeValue(QState<T> stateKey, int action, double v)
        {
            WriteFinest(stateKey, (layer, qs) => layer.InitializeValue(qs, action, v));
        }

        /// <summary>
        /// Writes the finest layer. While the state has more elements than there are layers, a layer is added
        /// and written with its slice of the state. The complete state is then registered.
        /// </summary>
        private void WriteFinest(QState<T> stateKey, Action<IHyperQ<T>, QState<T>> write)
        {
            if (stateKey.Count > _Layers.Count)
            {
                while (stateKey.Count > _Layers.Count)
                {
                    EnsureLayers(_Layers.Count + 1);
                    write(_Layers[_Layers.Count - 1], stateKey.Slice(_Layers.Count));
                }
            }
            else
            {
                write(_Layers[_Layers.Count - 1], stateKey);
            }
            if (stateKey.Count == _Layers.Count)
            {
                _StateMap.MapState(stateKey.Enumerator);
            }
        }

        public bool IsKnownState(QState<T> stateKey)
        {
            if (stateKey.Count != _Layers.Count)
            {
                return (false);
            }
            for (int i = 0; i < _Layers.Count; i++)
            {
                if (!_Layers[i].IsKnownState(stateKey.Slice(i+1)))
                {
                    return (false);
                }
            }
            return (true);
        }

        public bool IsKnownAction(int action)
        {
            for (int i = 0; i < _Layers.Count; i++)
            {
                if (_Layers[i].IsKnownAction(action))
                {
                    return (true);
                }
            }
            return (false);
        }

        public void ResetAdvantageTrace()
        {
            foreach (IHyperQ<T> layer in _Layers)
                layer.ResetAdvantageTrace();
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

        /// <summary>
        /// Writes the update mode, the action space, the registry of complete states and every layer. Layers
        /// that share the registry (double-Q layers) write it again inside their own checkpoints; the restore
        /// is idempotent.
        /// </summary>
        public virtual void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            writer.Write((int)UpdateMode);
            CheckpointIO.WriteActionSpace(writer, ActionSpace);
            _StateMap.SaveCheckpoint(writer, Keys);
            writer.Write(_Layers.Count);
            for (int i = 0; i < _Layers.Count; i++)
            {
                CheckpointIO.Checkpointable(_Layers[i], "Layer " + i).SaveCheckpoint(writer);
            }
        }

        public virtual void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            UpdateMode = (LayerUpdateMode)reader.ReadInt32();
            CheckpointIO.ReadActionSpace(reader, ActionSpace);
            _StateMap.LoadCheckpoint(reader, Keys);
            int count = reader.ReadInt32();
            EnsureLayers(count);
            for (int i = 0; i < count; i++)
            {
                CheckpointIO.Checkpointable(_Layers[i], "Layer " + i).LoadCheckpoint(reader);
            }
        }
    }
}
