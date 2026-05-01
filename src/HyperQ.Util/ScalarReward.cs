using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public readonly struct ScalarReward : IReward
    {
        public readonly double Value;
        private readonly double[] _span;
        public ScalarReward(double value)
        {
            Value = value;
            _span = new double[1] { value };
        }

        public ScalarReward(double[] value)
        {
            if(value == null) throw new ArgumentNullException("value");
            if (value.Length != 1) throw new ArgumentException("value must contain exactly one scalar.");
            Value = value[0];
            _span = value;
        }

        public int Dims => 1;
        public double this[int i] => i == 0 ? Value : throw new IndexOutOfRangeException();

        public ReadOnlySpan<double> AsSpan() {
            ReadOnlySpan<double> perHead = _span;
            return perHead;
        }
        public bool IsNegative => Value < 0.0;
        public static implicit operator double(ScalarReward r) => r.Value;
        public static implicit operator ScalarReward(double v) => new ScalarReward(v);
        public static ScalarReward operator +(ScalarReward a, ScalarReward b) => new ScalarReward(a.Value + b.Value);
        public static ScalarReward operator -(ScalarReward a, ScalarReward b) => new ScalarReward(a.Value - b.Value);
        public static ScalarReward operator *(ScalarReward a, double s) => new ScalarReward(a.Value * s);
        public static ScalarReward operator /(ScalarReward a, double s) => new ScalarReward(a.Value / s);
    }
    public static class RewardExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double AsDouble(this IReward r)
            => r.Dims == 1 ? r[0] : throw new InvalidOperationException("Vector reward must be scalarized explicitly.");
    }
}
