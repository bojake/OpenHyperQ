using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.IO;

namespace HyperQ.Learners
{
    /// <summary>
    /// A layered learner that combines its layers only when it is read to act. Its layers learn exactly as those of a
    /// <see cref="LayeredHyperQ{T}"/> with <see cref="LayerUpdateMode.UnscaledLayerUpdates"/>: every layer updates its
    /// own prefix of each transition toward a target built from its own values, and nothing is written from one layer
    /// into another. A subclass decides how a state's layer values are combined (<see cref="Values"/>) and what it
    /// learns from a transition before the layers are updated (<see cref="Observe"/>). When <see cref="Values"/>
    /// returns null, the learner acts exactly as the unscaled layered learner.
    /// </summary>
    /// <remarks>
    /// The learner counts how many updates each layer has applied to each prefix and action, and subclasses read the
    /// counts as the evidence behind a layer's value. A layer's value for an action it does not know reads as 0, the
    /// default of the library's tables. <see cref="Clone"/> and <see cref="MergeInto"/> are not supported.
    /// </remarks>
    public abstract class BlendedLayeredQ<T> : IHyperQ<T>, ICheckpointable
    {
        /// <summary>Update counts by action index, keyed by prefix; a prefix's length names its layer.</summary>
        private readonly Dictionary<QState<T>, int[]> _evidence = new Dictionary<QState<T>, int[]>();

        /// <param name="qGenerator">Creates one layer; the same generator a <see cref="LayeredHyperQ{T}"/> takes.</param>
        /// <param name="actionSpace">The action space shared by the layers.</param>
        protected BlendedLayeredQ(Func<IHyperQ<T>> qGenerator, QActionSpace<int> actionSpace)
        {
            if (qGenerator == null)
            {
                throw new ArgumentNullException(nameof(qGenerator));
            }
            if (actionSpace == null)
            {
                throw new ArgumentNullException(nameof(actionSpace));
            }
            Inner = new LayeredHyperQ<T>(qGenerator, actionSpace, null, LayerUpdateMode.UnscaledLayerUpdates);
            ActionCount = (int)actionSpace.MaximumNumberOfActions;
        }

        /// <summary>The unscaled layered learner whose layers this learner reads.</summary>
        protected LayeredHyperQ<T> Inner { get; }

        /// <summary>The layers, coarsest first: layer i holds the prefixes of length i + 1.</summary>
        protected IReadOnlyList<IHyperQ<T>> Layers { get { return Inner.LayerList; } }

        /// <summary>The number of action indexes, the length of every value array.</summary>
        protected int ActionCount { get; }

        /// <summary>The number of layers that see state <paramref name="s"/>: one per element, up to the layers created so far.</summary>
        protected int LayersFor(QState<T> s)
        {
            return Math.Min(s.Count, Layers.Count);
        }

        /// <summary>
        /// The number of updates of action index <paramref name="actionIndex"/> that the layer holding the prefix of
        /// <paramref name="s"/> of length <paramref name="length"/> has applied to it.
        /// </summary>
        public int Count(QState<T> s, int length, uint actionIndex)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            if (length < 0 || length > s.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "A prefix is at most as long as the state.");
            }
            return _evidence.TryGetValue(s.Slice(length), out int[] n) ? n[actionIndex] : 0;
        }

        /// <summary>The update counts by action index for <paramref name="prefix"/>: a copy, or zeros when it has none.</summary>
        protected int[] Evidence(QState<T> prefix)
        {
            return _evidence.TryGetValue(prefix, out int[] n) ? (int[])n.Clone() : new int[ActionCount];
        }

        /// <summary>The update counts by action index for the prefix of <paramref name="s"/> of length <paramref name="length"/>.</summary>
        protected int[] Evidence(QState<T> s, int length)
        {
            return Evidence(s.Slice(length));
        }

        /// <summary>
        /// One layer's values for its prefix of a state, by action index, read without side effects. Actions the
        /// layer does not know read as 0 and are not marked in <paramref name="known"/>.
        /// </summary>
        protected void LayerValues(int layer, QState<T> prefix, double[] values, bool[] known)
        {
            Array.Clear(values, 0, values.Length);
            Array.Clear(known, 0, known.Length);
            IHyperQ<T> q = Layers[layer];
            if (!q.IsKnownState(prefix))
            {
                return;
            }
            foreach (KeyValuePair<uint, double> kv in q.KnownActionValues(prefix))
            {
                if (kv.Key < values.Length)
                {
                    values[kv.Key] = kv.Value;
                    known[kv.Key] = true;
                }
            }
        }

        /// <summary>
        /// The values the learner acts on in state <paramref name="s"/>, by action index, with the actions that have a
        /// value marked in <paramref name="candidate"/>. Return null to act exactly as the unscaled layered learner.
        /// </summary>
        protected abstract double[] Values(QState<T> s, out bool[] candidate);

        /// <summary>Learns from a transition before the layers are updated with it; <paramref name="aprime"/> is null off policy.</summary>
        protected abstract void Observe(QState<T> s, int a, QState<T> sprime, int? aprime, double r, HyperParams hp, EvalMethodType evalType);

        /// <summary>Writes the subclass's own state at the end of a checkpoint.</summary>
        protected abstract void SaveBlendState(BinaryWriter writer, QStateKeySerializer<T> states);

        /// <summary>Restores what <see cref="SaveBlendState"/> wrote.</summary>
        protected abstract void LoadBlendState(BinaryReader reader, QStateKeySerializer<T> states);

        /// <summary>The highest (or, for <see cref="EvalMethodType.Min"/>, lowest) value of the candidate actions; 0 if none.</summary>
        protected static double Best(double[] values, bool[] candidate, EvalMethodType evalType)
        {
            bool found = false;
            double best = 0.0;
            for (int x = 0; x < values.Length; x++)
            {
                if (candidate[x] && (!found || (evalType == EvalMethodType.Min ? values[x] < best : values[x] > best)))
                {
                    best = values[x];
                    found = true;
                }
            }
            return best;
        }

        private QAction Extreme(QState<T> s, bool max)
        {
            double[] values = Values(s, out bool[] candidate);
            int best = -1;
            if (values != null)
            {
                for (int a = 0; a < ActionCount; a++)
                {
                    if (candidate[a] && (best < 0 || (max ? values[a] > values[best] : values[a] < values[best])))
                    {
                        best = a;
                    }
                }
            }
            if (best < 0)
            {
                return max ? Inner.ArgMax(s) : Inner.ArgMin(s);
            }
            return new QAction(ActionSpace.FromIndex((uint)best), values[best], (uint)best);
        }

        /// <summary>The action with the highest value the learner acts on; the first by index on a tie.</summary>
        public QAction ArgMax(QState<T> stateKey)
        {
            return Extreme(stateKey, true);
        }

        /// <summary>The action with the lowest value the learner acts on; the first by index on a tie.</summary>
        public QAction ArgMin(QState<T> stateKey)
        {
            return Extreme(stateKey, false);
        }

        /// <summary>The value the learner acts on.</summary>
        public double GetValue(QState<T> stateKey, int action)
        {
            double[] values = Values(stateKey, out _);
            return values == null ? Inner.GetValue(stateKey, action) : values[ActionSpace.ToIndex(action)];
        }

        /// <summary>The values the learner acts on, by action index.</summary>
        public double[] GetActionArray(QState<T> stateKey)
        {
            double[] values = Values(stateKey, out _);
            return values ?? Inner.GetActionArray(stateKey);
        }

        /// <summary>The values the learner acts on, by action index, for each state.</summary>
        public IDictionary<QState<T>, double[]> Snapshot(IEnumerable<QState<T>> states)
        {
            Dictionary<QState<T>, double[]> snap = new Dictionary<QState<T>, double[]>();
            foreach (QState<T> s in states)
            {
                snap[s] = GetActionArray(s);
            }
            return snap;
        }

        public double OffPolicyUpdate(QState<T> s, int a, QState<T> sprime, double r, HyperParams hp, EvalMethodType evalType)
        {
            Observe(s, a, sprime, null, r, hp, evalType);
            double x = Inner.OffPolicyUpdate(s, a, sprime, r, hp, evalType);
            AddEvidence(s, a);
            return x;
        }

        public double OnPolicyUpdate(QState<T> s, int a, QState<T> sprime, int aprime, double r, HyperParams hp)
        {
            Observe(s, a, sprime, aprime, r, hp, EvalMethodType.Max);
            double x = Inner.OnPolicyUpdate(s, a, sprime, aprime, r, hp);
            AddEvidence(s, a);
            return x;
        }

        private void AddEvidence(QState<T> s, int a)
        {
            uint ai = ActionSpace.ToIndex(a);
            int layers = LayersFor(s);
            for (int i = 1; i <= layers; i++)
            {
                QState<T> prefix = s.Slice(i);
                if (!_evidence.TryGetValue(prefix, out int[] n))
                {
                    _evidence[prefix] = n = new int[ActionCount];
                }
                n[ai]++;
            }
        }

        // ── ICheckpointable ──

        /// <summary>
        /// Serializer for the state elements in checkpoints, shared with the inner layered learner; set it for an
        /// element type without a built-in serializer (see <see cref="QKeySerializerFactory"/>).
        /// </summary>
        public IQKeySerializer<T> KeySerializer
        {
            get { return Inner.KeySerializer ?? (Inner.KeySerializer = QKeySerializerFactory.GetDefault<T>()); }
            set { Inner.KeySerializer = value; }
        }

        public int CheckpointVersion { get { return 1; } }

        /// <summary>Writes the inner layered learner, the update counts, then the subclass's own state.</summary>
        public void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            Inner.SaveCheckpoint(writer);
            QStateKeySerializer<T> states = new QStateKeySerializer<T>(KeySerializer);
            writer.Write(ActionCount);
            writer.Write(_evidence.Count);
            foreach (KeyValuePair<QState<T>, int[]> kv in _evidence)
            {
                states.Serialize(writer.BaseStream, kv.Key);
                foreach (int n in kv.Value)
                {
                    writer.Write(n);
                }
            }
            SaveBlendState(writer, states);
        }

        /// <summary>
        /// Restores what <see cref="SaveCheckpoint"/> wrote, into a learner constructed with the same generator,
        /// action space and options.
        /// </summary>
        public void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            if (version > CheckpointVersion)
            {
                throw new InvalidDataException("Checkpoint version " + version + " is newer than the supported " + CheckpointVersion + ".");
            }
            Inner.LoadCheckpoint(reader);
            QStateKeySerializer<T> states = new QStateKeySerializer<T>(KeySerializer);
            int actions = reader.ReadInt32();
            if (actions != ActionCount)
            {
                throw new InvalidDataException("The checkpoint counts updates for " + actions + " action indexes; this learner has " + ActionCount + ".");
            }
            _evidence.Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                QState<T> prefix = states.Deserialize(reader.BaseStream);
                int[] n = new int[ActionCount];
                for (int x = 0; x < n.Length; x++)
                {
                    n[x] = reader.ReadInt32();
                }
                _evidence[prefix] = n;
            }
            LoadBlendState(reader, states);
        }

        // ── Everything else is the inner unscaled learner's ──

        public QActionSpace<int> ActionSpace { get { return Inner.ActionSpace; } }
        public Func<double> DefaultValueFunc { get { return Inner.DefaultValueFunc; } set { Inner.DefaultValueFunc = value; } }
        public string Label { get { return Inner.Label; } set { Inner.Label = value; } }
        public bool RequiresIndexRepositoryLinking { get { return false; } }
        public QAdvantage Advantage { get { return Inner.Advantage; } }
        public double[,] AsMatrix { get { return Inner.AsMatrix; } }
        public Tuple<uint, uint> Shape { get { return Inner.Shape; } }

        public double this[QState<T> state, int action]
        {
            get { return GetValue(state, action); }
            set { SetValue(state, action, value); }
        }

        public void SetValue(QState<T> stateKey, int action, double v) { Inner.SetValue(stateKey, action, v); }
        public void InitializeValue(QState<T> stateKey, int action, double v) { Inner.InitializeValue(stateKey, action, v); }
        public uint AddState(QState<T> stateKey) { return Inner.AddState(stateKey); }
        public uint MapState(QState<T> stateKey) { return Inner.MapState(stateKey); }
        public bool RemoveState(QState<T> stateKey) { return Inner.RemoveState(stateKey); }
        public bool IsKnownState(QState<T> stateKey) { return Inner.IsKnownState(stateKey); }
        public bool IsKnownAction(int action) { return Inner.IsKnownAction(action); }
        public void Link(MemoryBackedHyperMapper<T> stateMap, QActionSpace<int> actionSpace) { Inner.Link(stateMap, actionSpace); }
        public double[] GetKnownActionArray(QState<T> stateKey) { return Inner.GetKnownActionArray(stateKey); }
        public IEnumerable<KeyValuePair<uint, double>> KnownActionValues(QState<T> stateKey) { return Inner.KnownActionValues(stateKey); }
        public IList<double[]> GetLayerActionArrays(QState<T> stateKey) { return Inner.GetLayerActionArrays(stateKey); }
        public void ResetAdvantageTrace() { Inner.ResetAdvantageTrace(); }
        public void Dump(string label = "Q", int level = 0, bool dumpData = false) { Inner.Dump(label, level, dumpData); }
        public void MergeInto(Q<QState<T>> from, EvalMethodType methodType = EvalMethodType.Max) { throw new NotSupportedException("A blended layered learner cannot be merged."); }
        public Q<QState<T>> Clone() { throw new NotSupportedException("A blended layered learner cannot be cloned."); }
    }

    /// <summary>
    /// The evidence buckets the blending learners group update counts into: n = 0, 1, 2-3, 4-7, 8-15, and 16 or more.
    /// </summary>
    public static class EvidenceBuckets
    {
        /// <summary>The buckets' names, by bucket.</summary>
        public static IReadOnlyList<string> Names { get; } = new[] { "0", "1", "2-3", "4-7", "8-15", "16+" };

        /// <summary>The number of buckets.</summary>
        public static int Count { get { return Names.Count; } }

        /// <summary>The bucket of an update count.</summary>
        public static int Of(int n)
        {
            if (n <= 0) return 0;
            if (n == 1) return 1;
            if (n <= 3) return 2;
            if (n <= 7) return 3;
            if (n <= 15) return 4;
            return 5;
        }
    }
}
