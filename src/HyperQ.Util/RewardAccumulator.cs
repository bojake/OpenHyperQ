using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public sealed class RewardAccumulator
    {
        private double[] _sum;

        public int Dims => _sum?.Length ?? 0;

        public RewardAccumulator Add(IReward r)
        {
            if (r == null)
            {
                throw new ArgumentNullException(nameof(r));
            }
            if (r.Dims <= 0)
            {
                throw new ArgumentException("Reward must have at least one dimension.", nameof(r));
            }
            if (_sum == null)
            {
                _sum = new double[r.Dims];
            }
            else if (_sum.Length != r.Dims)
            {
                throw new InvalidOperationException("Reward dimension mismatch");
            }

            for (int i = 0; i < _sum.Length; i++)
                _sum[i] += r[i];
            return this;
        }

        public double this[int i]
        {
            get
            {
                if (_sum == null)
                {
                    throw new InvalidOperationException("No rewards have been accumulated.");
                }
                return _sum[i];
            }
        }

        public RewardAccumulator Add(double d)
        {
            if (_sum == null)
            {
                _sum = new double[1];
            }
            _sum[0] += d;
            return this;
        }
        public RewardAccumulator Add(double[] d)
        {
            if (d == null)
            {
                throw new ArgumentNullException(nameof(d));
            }
            if (d.Length == 0)
            {
                throw new ArgumentException("Reward vector can not be empty.", nameof(d));
            }
            if (_sum == null)
            {
                _sum = new double[d.Length];
            }
            else if (_sum.Length != d.Length)
            {
                throw new InvalidOperationException("Reward dimension mismatch");
            }
            for (int i = 0; i < d.Length; i++)
                _sum[i] += d[i];
            return this;
        }
        // canonical scalar for ordering / neg-pos
        public double Scalar0 => (_sum == null || _sum.Length == 0) ? 0.0 : _sum[0];

        public override string ToString()
        {
            if (_sum == null || _sum.Length == 0)
            {
                return 0.0.ToString("G", CultureInfo.InvariantCulture);
            }
            if (_sum.Length == 1)
            {
                return _sum[0].ToString("G", CultureInfo.InvariantCulture);
            }
            return "[" + string.Join(", ", _sum.Select(v => v.ToString("G", CultureInfo.InvariantCulture))) + "]";
        }

        public static implicit operator double(RewardAccumulator r) => r.Scalar0;
    }
}
