using HyperQ.Util;

namespace HyperQ.Training
{
    /// <summary>
    /// Convenience 1-arg aliases for Training generics that default RT to
    /// <see cref="ScalarReward"/>, matching the pre-.NET10 DLL API used by the sample runners.
    /// The memory aliases are siblings, not subtypes of one another: a variable that may hold any of them
    /// is declared as the shared base type, <c>QMemory&lt;T, ScalarReward&gt;</c>, which is also what
    /// <see cref="PvESARSATrainer{T}.EnableMemory"/> takes.
    /// </summary>

    public class QMemory<T> : QMemory<T, ScalarReward>
    {
        public QMemory(int capacity, QRandom random) : base(capacity, random) { }
    }

    /// <summary>
    /// The scalar alias of <see cref="QNegPosMemory{T, RT}"/>. It has no conversion to <see cref="QMemory{T}"/>:
    /// the two are siblings, so code that needs either one uses the base type <c>QMemory&lt;T, ScalarReward&gt;</c>.
    /// (An implicit conversion used to exist and always threw, which turned that mistake from a compile error
    /// into a run-time crash, for example in <c>flag ? new QNegPosMemory&lt;T&gt;(...) : new QMemory&lt;T&gt;(...)</c>.)
    /// </summary>
    public class QNegPosMemory<T> : QNegPosMemory<T, ScalarReward>
    {
        public QNegPosMemory(int capacity, QRandom random) : base(capacity, random) { }
    }

    public class QEpisodicMemory<T> : QEpisodicMemory<T, ScalarReward>
    {
        public QEpisodicMemory(int capacity, QRandom random) : base(capacity, random) { }
    }

    public class QEpisodicNegPosMemory<T> : QEpisodicNegPosMemory<T, ScalarReward>
    {
        public QEpisodicNegPosMemory(int capacity, QRandom random) : base(capacity, random) { }
    }

    public class DynaState<T> : DynaState<T, ScalarReward>
    {
        public DynaState(int iterations, int capacity) : base(iterations, capacity) { }
    }
}
