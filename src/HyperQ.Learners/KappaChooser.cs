using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HyperQ.Learners
{
    /// <summary>
    /// Learns the prior strength κ of a <see cref="ShrinkageLayeredQ{T}"/> per context. A context is a parent prefix,
    /// which names the layer below it (the parent of layer i has length i), and the evidence bucket
    /// (<see cref="EvidenceBuckets"/>) of the child's update count. Before each update the learner scores every candidate
    /// κ of every layer below the coarsest by the squared error with which its blend predicts the layer's TD target, and
    /// the chooser keeps a running mean of those errors per context. Every candidate is scored on every sample, so the
    /// chooser is a tabular learner with full information and γ = 0, and it needs no exploration.
    /// </summary>
    /// <remarks>
    /// A context takes the candidate with the lowest mean squared error, the smaller κ on a tie. A context with fewer
    /// than <see cref="MinSamples"/> samples takes the pooled choice for its layer and bucket, and κ = 0, no blending,
    /// when that has too few samples as well. The running mean moves at rate max(1 / count, <see cref="SlowestRate"/>):
    /// the exact mean for the first thousand samples, then an exponential average that keeps tracking the non-stationary
    /// targets. The selection rule is this library's own and carries no convergence guarantee.
    /// </remarks>
    public sealed class KappaChooser<T>
    {
        /// <summary>The candidate κ values a chooser uses unless it is given others.</summary>
        public static IReadOnlyList<double> DefaultCandidates { get; } = new double[] { 0, 1, 2, 4, 8, 16, 32, 64 };

        /// <summary>The samples a context needs before its own choice is used.</summary>
        public const int MinSamples = 20;

        /// <summary>The lowest rate of the running mean of a context's errors.</summary>
        public const double SlowestRate = 0.001;

        /// <summary>The running mean of each candidate's squared error in one context and bucket.</summary>
        internal sealed class Scores
        {
            internal long Count;
            internal readonly double[] Mean;

            internal Scores(int candidates)
            {
                Mean = new double[candidates];
            }

            /// <summary>The candidate with the lowest mean error; the first on a tie.</summary>
            internal int Best
            {
                get
                {
                    int best = 0;
                    for (int k = 1; k < Mean.Length; k++)
                    {
                        if (Mean[k] < Mean[best])
                        {
                            best = k;
                        }
                    }
                    return best;
                }
            }

            internal void Add(double[] losses)
            {
                Count++;
                double rate = Math.Max(1.0 / Count, SlowestRate);
                for (int k = 0; k < Mean.Length; k++)
                {
                    Mean[k] += rate * (losses[k] - Mean[k]);
                }
            }
        }

        /// <summary>One context's scores by evidence bucket; null where a bucket has no samples yet.</summary>
        internal sealed class Row
        {
            internal readonly Scores[] ByBucket = new Scores[EvidenceBuckets.Count];
        }

        private readonly double[] _candidates;
        private readonly Dictionary<QState<T>, Row> _specific = new Dictionary<QState<T>, Row>();
        /// <summary>The pooled scores by layer.</summary>
        private readonly List<Row> _pooled = new List<Row>();

        /// <param name="candidates">
        /// The κ values to choose from: at least one, each finite and at least 0, in increasing order. Null for
        /// <see cref="DefaultCandidates"/>.
        /// </param>
        public KappaChooser(IEnumerable<double> candidates = null)
        {
            double[] c = (candidates ?? DefaultCandidates).ToArray();
            if (c.Length == 0)
            {
                throw new ArgumentException("A chooser needs at least one candidate κ.", nameof(candidates));
            }
            for (int k = 0; k < c.Length; k++)
            {
                if (!(c[k] >= 0.0) || double.IsInfinity(c[k]))
                {
                    throw new ArgumentOutOfRangeException(nameof(candidates), "Every candidate κ must be a finite number of at least 0.");
                }
                if (k > 0 && !(c[k] > c[k - 1]))
                {
                    throw new ArgumentException("The candidates must be in increasing order.", nameof(candidates));
                }
            }
            _candidates = c;
        }

        /// <summary>The κ values the chooser picks from, in increasing order.</summary>
        public IReadOnlyList<double> Candidates { get { return _candidates; } }

        /// <summary>The κ for the children of <paramref name="parent"/> whose update count falls in <paramref name="bucket"/>.</summary>
        public double Kappa(QState<T> parent, int bucket)
        {
            CheckContext(parent, bucket);
            return Kappa(RowOf(parent), parent.Count, bucket);
        }

        /// <summary>The pooled choice for a layer and evidence bucket, or NaN while it has fewer than <see cref="MinSamples"/> samples.</summary>
        public double PooledKappa(int layer, int bucket)
        {
            Scores p = Pooled(layer, bucket);
            return p != null && p.Count >= MinSamples ? _candidates[p.Best] : double.NaN;
        }

        /// <summary>Adds one sample of squared errors, by candidate, to a context and to the pooled scores of its layer.</summary>
        public void Observe(QState<T> parent, int bucket, double[] losses)
        {
            CheckContext(parent, bucket);
            if (losses == null)
            {
                throw new ArgumentNullException(nameof(losses));
            }
            if (losses.Length != _candidates.Length)
            {
                throw new ArgumentException("There must be one loss per candidate κ.", nameof(losses));
            }
            if (!_specific.TryGetValue(parent, out Row row))
            {
                _specific[parent.Clone()] = row = new Row();
            }
            Add(row, bucket, losses);
            int layer = parent.Count;
            while (_pooled.Count <= layer)
            {
                _pooled.Add(new Row());
            }
            Add(_pooled[layer], bucket, losses);
        }

        /// <summary>
        /// What the chooser has settled on, by layer and evidence bucket: the pooled choice with its mean squared errors,
        /// and how the parents with enough samples chose.
        /// </summary>
        public IReadOnlyList<KappaChoice> Summarize()
        {
            Dictionary<(int Layer, int Bucket), int[]> chosen = new Dictionary<(int, int), int[]>();
            foreach (KeyValuePair<QState<T>, Row> kv in _specific)
            {
                for (int b = 0; b < EvidenceBuckets.Count; b++)
                {
                    Scores s = kv.Value.ByBucket[b];
                    if (s == null || s.Count < MinSamples)
                    {
                        continue;
                    }
                    if (!chosen.TryGetValue((kv.Key.Count, b), out int[] counts))
                    {
                        chosen[(kv.Key.Count, b)] = counts = new int[_candidates.Length];
                    }
                    counts[s.Best]++;
                }
            }
            List<KappaChoice> result = new List<KappaChoice>();
            for (int layer = 0; layer < _pooled.Count; layer++)
            {
                for (int b = 0; b < EvidenceBuckets.Count; b++)
                {
                    Scores p = _pooled[layer].ByBucket[b];
                    if (p == null)
                    {
                        continue;
                    }
                    result.Add(new KappaChoice(layer, b, p.Count, _candidates[p.Best], _candidates, (double[])p.Mean.Clone(),
                        chosen.TryGetValue((layer, b), out int[] counts) ? counts : new int[_candidates.Length]));
                }
            }
            return result;
        }

        /// <summary>A parent's scores, or null when it has none; read without side effects.</summary>
        internal Row RowOf(QState<T> parent)
        {
            return _specific.TryGetValue(parent, out Row row) ? row : null;
        }

        /// <summary>The κ for a parent's <paramref name="row"/> (null for a parent without scores) at a layer and bucket.</summary>
        internal double Kappa(Row row, int layer, int bucket)
        {
            Scores s = row?.ByBucket[bucket];
            if (s != null && s.Count >= MinSamples)
            {
                return _candidates[s.Best];
            }
            Scores p = Pooled(layer, bucket);
            if (p != null && p.Count >= MinSamples)
            {
                return _candidates[p.Best];
            }
            return 0.0;
        }

        private Scores Pooled(int layer, int bucket)
        {
            return layer >= 0 && layer < _pooled.Count && bucket >= 0 && bucket < EvidenceBuckets.Count ? _pooled[layer].ByBucket[bucket] : null;
        }

        private void Add(Row row, int bucket, double[] losses)
        {
            Scores s = row.ByBucket[bucket];
            if (s == null)
            {
                row.ByBucket[bucket] = s = new Scores(_candidates.Length);
            }
            s.Add(losses);
        }

        private static void CheckContext(QState<T> parent, int bucket)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
            if (parent.Count == 0)
            {
                throw new ArgumentException("A parent prefix has at least one element.", nameof(parent));
            }
            if (bucket < 0 || bucket >= EvidenceBuckets.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(bucket), "The bucket must be one of the EvidenceBuckets.");
            }
        }

        // ── Checkpoints, written inside the learner's ──

        internal void Save(BinaryWriter writer, QStateKeySerializer<T> states)
        {
            writer.Write(_candidates.Length);
            foreach (double c in _candidates)
            {
                writer.Write(c);
            }
            writer.Write(_pooled.Count);
            foreach (Row row in _pooled)
            {
                WriteRow(writer, row);
            }
            writer.Write(_specific.Count);
            foreach (KeyValuePair<QState<T>, Row> kv in _specific)
            {
                states.Serialize(writer.BaseStream, kv.Key);
                WriteRow(writer, kv.Value);
            }
        }

        internal void Load(BinaryReader reader, QStateKeySerializer<T> states)
        {
            int n = reader.ReadInt32();
            double[] saved = new double[n];
            for (int k = 0; k < n; k++)
            {
                saved[k] = reader.ReadDouble();
            }
            if (!saved.SequenceEqual(_candidates))
            {
                throw new InvalidDataException("The checkpoint's chooser picks from κ = {" + List(saved)
                    + "}; this chooser picks from {" + List(_candidates) + "}.");
            }
            _pooled.Clear();
            int layers = reader.ReadInt32();
            for (int i = 0; i < layers; i++)
            {
                _pooled.Add(ReadRow(reader));
            }
            _specific.Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                QState<T> parent = states.Deserialize(reader.BaseStream);
                _specific[parent] = ReadRow(reader);
            }
        }

        private static string List(IEnumerable<double> values)
        {
            return string.Join(", ", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
        }

        private static void WriteRow(BinaryWriter writer, Row row)
        {
            foreach (Scores s in row.ByBucket)
            {
                writer.Write(s != null);
                if (s == null)
                {
                    continue;
                }
                writer.Write(s.Count);
                foreach (double m in s.Mean)
                {
                    writer.Write(m);
                }
            }
        }

        private Row ReadRow(BinaryReader reader)
        {
            Row row = new Row();
            for (int b = 0; b < row.ByBucket.Length; b++)
            {
                if (!reader.ReadBoolean())
                {
                    continue;
                }
                Scores s = new Scores(_candidates.Length) { Count = reader.ReadInt64() };
                for (int k = 0; k < s.Mean.Length; k++)
                {
                    s.Mean[k] = reader.ReadDouble();
                }
                row.ByBucket[b] = s;
            }
            return row;
        }
    }

    /// <summary>What a <see cref="KappaChooser{T}"/> has settled on for one layer and evidence bucket.</summary>
    public sealed class KappaChoice
    {
        internal KappaChoice(int layer, int bucket, long samples, double pooledKappa, IReadOnlyList<double> candidates,
            IReadOnlyList<double> meanSquaredError, IReadOnlyList<int> parentsChoosing)
        {
            Layer = layer;
            Bucket = bucket;
            Samples = samples;
            PooledKappa = pooledKappa;
            Candidates = candidates;
            MeanSquaredError = meanSquaredError;
            ParentsChoosing = parentsChoosing;
        }

        /// <summary>The layer whose blend the choice sets; its parents have this many elements.</summary>
        public int Layer { get; }

        /// <summary>The evidence bucket, an index into <see cref="EvidenceBuckets.Names"/>.</summary>
        public int Bucket { get; }

        /// <summary>The bucket's name.</summary>
        public string Evidence { get { return EvidenceBuckets.Names[Bucket]; } }

        /// <summary>The samples behind the pooled scores.</summary>
        public long Samples { get; }

        /// <summary>The pooled choice, whatever its number of samples.</summary>
        public double PooledKappa { get; }

        /// <summary>The candidates, in the order of <see cref="MeanSquaredError"/> and <see cref="ParentsChoosing"/>.</summary>
        public IReadOnlyList<double> Candidates { get; }

        /// <summary>The pooled mean squared error of each candidate.</summary>
        public IReadOnlyList<double> MeanSquaredError { get; }

        /// <summary>For each candidate, how many parents with at least <see cref="KappaChooser{T}.MinSamples"/> samples chose it.</summary>
        public IReadOnlyList<int> ParentsChoosing { get; }
    }
}
