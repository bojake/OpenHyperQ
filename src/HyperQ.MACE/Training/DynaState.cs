using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;

namespace HyperQ.MACE.Training
{
    public interface IDynaEntry : IEquatable<IDynaEntry>
    {
        // Just a placeholder to abstract the DynaEntry for MACE and non-MACE environments
    }

    [Serializable]
    public class DynaEntry<T> : Tuple<T, QAction[]>, IDynaEntry
    {
        public DynaEntry(T s, QAction[] a) : base(s, a)
        {
        }
        public bool Equals(IDynaEntry other)
        {
            if (other is null) return false;
            DynaEntry<T> b = other as DynaEntry<T>;
            if (EqualityComparer<T>.Default.Equals(b.Item1, Item1))
            {
                for (int i = 0; i < Item2.Length; i++)
                {
                    if (b.Item2[i] != Item2[i]) return false;
                }
                return true;
            }
            return false;
        }
        public override bool Equals(object o)
        {
            return o.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            return Item1.GetHashCode() ^ Item2.GetHashCode();
        }
    }

    [Serializable]
    public class DynaState<T, RT> where RT : IReward
    {
        private List<IDynaEntry> _History = null;

        /// <summary>
        /// (s,a)(sprime) = count
        /// </summary>
        private readonly Dictionary<IDynaEntry, Dictionary<T, int>> _Tc = new Dictionary<IDynaEntry, Dictionary<T, int>>();

        /// <summary>
        /// Cache: sum(Tc(s,a,*)) so UpdateT is O(1) instead of summing each time
        /// </summary>
        private readonly Dictionary<IDynaEntry, int> _TcSum = new Dictionary<IDynaEntry, int>();

        /// <summary>
        /// (s,a)(sprime) = probability (double), not RT
        /// </summary>
        private readonly Dictionary<IDynaEntry, Dictionary<T, double>> _T = new Dictionary<IDynaEntry, Dictionary<T, double>>();

        /// <summary>
        /// Cached max of T[de]. Tuple is (state, probability)
        /// </summary>
        private readonly Dictionary<IDynaEntry, Tuple<T, double>> _Tmax = new Dictionary<IDynaEntry, Tuple<T, double>>();

        /// <summary>
        /// (s,a) = reward model (RT)
        /// </summary>
        private readonly Dictionary<IDynaEntry, RT> _R = new Dictionary<IDynaEntry, RT>();

        // Optional: avoid _R.Keys.ToList() allocations in RandomSA
        private readonly List<IDynaEntry> _Keys = new List<IDynaEntry>();
        private readonly Dictionary<IDynaEntry, int> _KeyIndex = new Dictionary<IDynaEntry, int>();

        // Reward math helper (works for any RT with a ctor(double[]) or ctor(ReadOnlySpan<double>))
        private readonly RewardOps _ops;

        public int DynaIterations { get; private set; } = 0;
        public int HistoryCapacity => _History?.Capacity ?? 0;
        public bool PositiveOnly { get; private set; } = false;


        public DynaState(int iters = 500, int max_histories = 0, bool positive_only = false)
        {
            DynaIterations = iters;

            if (max_histories > 0)
                _History = new List<IDynaEntry>(max_histories);

            PositiveOnly = positive_only;
            _ops = new RewardOps();

        }


        private void AddKeyIfMissing(IDynaEntry de)
        {
            if (_KeyIndex.ContainsKey(de)) return;
            _KeyIndex[de] = _Keys.Count;
            _Keys.Add(de);
        }

        private void RemoveKeyIfPresent(IDynaEntry de)
        {
            if (!_KeyIndex.TryGetValue(de, out int ix)) return;

            int last = _Keys.Count - 1;
            var lastDe = _Keys[last];

            _Keys[ix] = lastDe;
            _KeyIndex[lastDe] = ix;

            _Keys.RemoveAt(last);
            _KeyIndex.Remove(de);
        }

        public DynaState<T, RT> Update(T s, QAction[] a, T sprime, RT r, HyperParams hp)
        {
            if (PositiveOnly && r.IsNegative)
            {
                throw new InvalidOperationException("DynaState is configured for positive-only rewards, and r was negative");
            }
            return (UpdateT(s, a, sprime).UpdateR(s, a, r, hp));
        }

        public IDynaEntry RandomSA(QRandom ran = null)
        {
            if (ran == null)
                ran = QRandom.Instance;
            if (_Keys.Count == 0) throw new InvalidOperationException("DynaState has no learned (s,a) entries.");
            int ix = ran.Ran.Next(0, _Keys.Count);
            return _Keys[ix];
        }

        /// <summary>
        /// Samples the Dyna state and returns a new state,reward from the T/R
        /// </summary>
        public Tuple<T, RT> Sample(IDynaEntry de)
        {
            if (!_T.TryGetValue(de, out var sT))
                throw new KeyNotFoundException("DynaState has no transition model for the provided (s,a).");

            if (!_R.TryGetValue(de, out var r))
                throw new KeyNotFoundException("DynaState has no reward model for the provided (s,a).");

            // Cached best next state
            if (_Tmax.TryGetValue(de, out var cached))
                return new Tuple<T, RT>(cached.Item1, r);

            // Find argmax probability
            var best = sT.Aggregate((a, b) => a.Value >= b.Value ? a : b);
            var ret = new Tuple<T, RT>(best.Key, r);
            _Tmax[de] = new Tuple<T, double>(best.Key, best.Value);

            return ret;
        }

        public DynaState<T, RT> UpdateT(T s, QAction[] a, T sprime)
        {
            var de = new DynaEntry<T>(s, a);

            if (!_Tc.ContainsKey(de))
            {
                // History eviction (if enabled)
                if (_History != null)
                {
                    if (_History.Count == _History.Capacity)
                    {
                        var x = _History[0];
                        _History.RemoveAt(0);

                        _Tc.Remove(x);
                        _TcSum.Remove(x);
                        _T.Remove(x);
                        _R.Remove(x);
                        _Tmax.Remove(x);
                        RemoveKeyIfPresent(x);
                    }

                    _History.Add(de);
                }

                _Tc[de] = new Dictionary<T, int>();
                _T[de] = new Dictionary<T, double>();
                _TcSum[de] = 0;

                // Do NOT add to _R here; UpdateR controls reward initialization
                AddKeyIfMissing(de);
            }

            // Initialize per-sprime bucket if first time seen
            if (!_Tc[de].ContainsKey(sprime))
            {
                _Tc[de][sprime] = 0;
                _T[de][sprime] = 0.0;
            }

            _Tc[de][sprime] += 1;
            _TcSum[de] += 1;

            double p = (double)_Tc[de][sprime] / _TcSum[de];
            _T[de][sprime] = p;

            // Update cached max
            if (!_Tmax.TryGetValue(de, out var mx) || p > mx.Item2)
                _Tmax[de] = new Tuple<T, double>(sprime, p);

            return this;
        }

        public DynaState<T, RT> UpdateR(T s, QAction[] a, RT r, HyperParams hp)
        {
            if (PositiveOnly && r.IsNegative)
                return this;

            var de = new DynaEntry<T>(s, a);

            // Ensure key list includes this (s,a) so RandomSA can sample it even if UpdateT wasn't called first
            AddKeyIfMissing(de);

            if (!_R.ContainsKey(de))
            {
                // First observation: start at alpha-scaled reward
                _R[de] = _ops.Scale(r, hp.Alpha);
            }
            else
            {
                // Exponential moving average: R <- (1-a)R + a*r
                double f1 = 1.0 - hp.Alpha;
                var r1 = _ops.Scale(_R[de], f1);
                var r2 = _ops.Scale(r, hp.Alpha);
                _R[de] = _ops.Add(r1, r2);
            }

            return this;
        }

        /// <summary>
        /// Reward math that does NOT rely on RewardMath and does not require RT to be mutable.
        /// It constructs RT from a double[] via:
        /// - ctor(double[]) OR
        /// - ctor(ReadOnlySpan&lt;double&gt;) OR
        /// - ctor(Span&lt;double&gt;)
        /// </summary>
        private sealed class RewardOps
        {
            private readonly Func<double[], RT> _ctorFromArray;

            public RewardOps()
            {
                _ctorFromArray = BuildCtor();
            }

            public RT Add(RT a, RT b)
            {
                if (a.Dims != b.Dims) throw new InvalidOperationException("Reward dim mismatch");
                var dst = new double[a.Dims];
                for (int i = 0; i < dst.Length; i++)
                    dst[i] = a[i] + b[i];
                return _ctorFromArray(dst);
            }

            public RT Scale(RT r, double s)
            {
                var dst = new double[r.Dims];
                for (int i = 0; i < dst.Length; i++)
                    dst[i] = r[i] * s;
                return _ctorFromArray(dst);
            }

            private static Func<double[], RT> BuildCtor()
            {
                var t = typeof(RT);

                // Prefer ctor(double[])
                var cArr = t.GetConstructor(new[] { typeof(double[]) });
                if (cArr != null)
                    return (double[] v) => (RT)cArr.Invoke(new object[] { v });

                // If you hit this, add a ctor(double[]) to your reward type (recommended).
                return _ =>
                    throw new NotSupportedException(
                        $"{t.FullName} must provide a constructor: .ctor(double[]) (recommended), or .ctor(ReadOnlySpan<double>)");
            }
        }
    }
}
