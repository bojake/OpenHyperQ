using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public readonly struct MultiReward : IReward
    {
        private readonly double[] _v;
        public int Dims => _v?.Length ?? 0;
        public double this[int i]
        {
            get
            {
                if (_v == null)
                {
                    throw new InvalidOperationException("Reward is not initialized.");
                }
                return _v[i];
            }
        }

        public MultiReward(double[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            if (values.Length == 0)
            {
                throw new ArgumentException("Reward vector can not be empty.", nameof(values));
            }
            _v = values.ToArray();
        }

        public MultiReward(ReadOnlySpan<double> values)
        {
            if (values.Length == 0)
            {
                throw new ArgumentException("Reward vector can not be empty.", nameof(values));
            }
            _v = values.ToArray();
        }
        public bool IsNegative
        {
            get
            {
                if (_v == null || _v.Length == 0)
                {
                    return false;
                }
                for (int i = 0; i < _v.Length; i++)
                {
                    if (_v[i] >= 0) return false;
                }
                return true;
            }
        }
        public static MultiReward operator +(MultiReward a, MultiReward b)
        {
            if (a.Dims != b.Dims) throw new InvalidOperationException("Reward dim mismatch");
            var r = new double[a.Dims];
            for (int i = 0; i < r.Length; i++)
                r[i] = a._v[i] + b._v[i];
            return new MultiReward(r);
        }

        public static MultiReward operator -(MultiReward a, MultiReward b)
        {
            if (a.Dims != b.Dims) throw new InvalidOperationException("Reward dim mismatch");
            var r = new double[a.Dims];
            for (int i = 0; i < r.Length; i++)
                r[i] = a._v[i] - b._v[i];
            return new MultiReward(r);
        }

        public static MultiReward operator *(MultiReward a, double s)
        {
            var r = new double[a.Dims];
            for (int i = 0; i < r.Length; i++)
                r[i] = a._v[i] * s;
            return new MultiReward(r);
        }

        public static MultiReward operator /(MultiReward a, double s)
        {
            var r = new double[a.Dims];
            for (int i = 0; i < r.Length; i++)
                r[i] = a._v[i] / s;
            return new MultiReward(r);
        }

        public ReadOnlySpan<double> AsSpan() {
            ReadOnlySpan<double> perHead = _v ?? Array.Empty<double>();
            return perHead;
        }
    }
    public static class RewardCompare
    {
        /// <summary>
        /// Returns true if all items in a and b are equal within tolerance tol.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="tol"></param>
        /// <returns></returns>
        public static bool AreEqual(IReward a, IReward b, double tol = 1e-8)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Dims != b.Dims)
                return false;
            for (int i = 0; i < a.Dims; i++)
            {
                if (Math.Abs(a[i] - b[i]) > tol)
                    return false;
            }
            return true;
        }
        /// <summary>
        /// Returns true if all items in a are greater than those in b.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static bool IsGreater(IReward a, IReward b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Dims != b.Dims)
                throw new ArgumentException();
            for (int i = 0; i < a.Dims; i++)
            {
                if (a[i] <= b[i])
                    return false;
            }
            return true;
        }
    }
    public static class RewardMath<RT> where RT : IReward
    {
        public static IReward Add(RT a, RT b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Dims != b.Dims)
                throw new ArgumentException();
            if (a.Dims <= 0)
                throw new ArgumentException("Reward must have at least one dimension.", nameof(a));
            Span<double> dst = new double[a.Dims];
            for (int i = 0; i < a.Dims; i++) {
                dst[i] = a[i] + b[i];
            }
            return new MultiReward(dst.ToArray());
        }

        public static IReward Add(RT a, double scalar)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.Dims <= 0)
                throw new ArgumentException("Reward must have at least one dimension.", nameof(a));
            Span<double> dst = new double[a.Dims];
            for (int i = 0; i < a.Dims; i++)
            {
                dst[i] = a[i] + scalar;
            }
            return new MultiReward(dst.ToArray());
        }

        public static IReward Scale(RT r, double s)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (r.Dims <= 0)
                throw new ArgumentException("Reward must have at least one dimension.", nameof(r));
            Span<double> span = new double[r.Dims];
            for (int i = 0; i < r.Dims; i++)
                span[i] = r[i] * s;
            return new MultiReward(span.ToArray());
        }
    }
}
