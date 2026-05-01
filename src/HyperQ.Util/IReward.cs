using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public interface IReward
    {
        int Dims { get; }
        double this[int i] { get; }
        ReadOnlySpan<double> AsSpan();
        bool IsNegative { get; }

    }
}
