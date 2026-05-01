using HyperQ.Util;

namespace HyperQ.Training
{
    /// <summary>
    /// Convenience 1-arg aliases for Training generics that default RT to
    /// <see cref="ScalarReward"/>, matching the pre-.NET10 DLL API used by the sample runners.
    /// </summary>

    public class QMemory<T> : QMemory<T, ScalarReward>
    {
        public QMemory(int capacity, QRandom random = null) : base(capacity, random) { }
    }

    public class QNegPosMemory<T> : QNegPosMemory<T, ScalarReward>
    {
        public QNegPosMemory(int capacity, QRandom random = null) : base(capacity, random) { }
        // Implicit conversion to QMemory<T> (scalar alias)
        public static implicit operator QMemory<T>(QNegPosMemory<T> m)
        {
            // Because QNegPosMemory<T,ScalarReward> derives from QMemory<T,ScalarReward>, and
            // QMemory<T> derives from QMemory<T,ScalarReward>, C# won't allow direct assignment.
            // We need a wrapper that holds this as its base.
            // The cleanest approach: caller assigns via the 2-arg base — this is a no-op alias.
            throw new System.InvalidCastException("Use (QMemory<T,ScalarReward>)m or EnableMemory with the typed overload.");
        }
    }

    public class QEpisodicMemory<T> : QEpisodicMemory<T, ScalarReward>
    {
        public QEpisodicMemory(int capacity, QRandom random = null) : base(capacity, random) { }
    }

    public class QEpisodicNegPosMemory<T> : QEpisodicNegPosMemory<T, ScalarReward>
    {
        public QEpisodicNegPosMemory(int capacity, QRandom random = null) : base(capacity, random) { }
    }

    public class DynaState<T> : DynaState<T, ScalarReward>
    {
        public DynaState(int iterations, int capacity) : base(iterations, capacity) { }
    }
}
