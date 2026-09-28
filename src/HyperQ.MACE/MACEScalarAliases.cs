using HyperQ.Util;
using HyperQ.Env;

namespace HyperQ.MACE
{
    /// <summary>
    /// Convenience 1-arg alias for <see cref="IMACEPvEEnv{T,RT}"/> that uses
    /// <see cref="ScalarReward"/> as the reward type, matching the pre-.NET10 DLL contract.
    /// </summary>
    public interface IMACEPvEEnv<T> : IMACEPvEEnv<T, ScalarReward>
    {
    }
}

namespace HyperQ.MACE.Training
{
    using HyperQ.Util;
    using HyperQ.Learners;

    /// <summary>Convenience 1-arg alias for <see cref="MACEPvESARSATrainer{T,RT}"/> using <see cref="ScalarReward"/>.</summary>
    public class MACEPvESARSATrainer<T> : MACEPvESARSATrainer<T, ScalarReward>
    {
        public MACEPvESARSATrainer(
            QRandom ran,
            QEvalType evalType = QEvalType.OnPolicy)
            : base(ran, evalType) { }
    }

    /// <summary>Convenience 1-arg alias for <see cref="QMemory{T,RT}"/> using <see cref="ScalarReward"/>.</summary>
    public class QMemory<T> : QMemory<T, ScalarReward>
    {
        public QMemory(int maxSize, QRandom ran) : base(maxSize, ran) { }
    }

    /// <summary>Convenience 1-arg alias for <see cref="QNegPosMemory{T,RT}"/> using <see cref="ScalarReward"/>.</summary>
    public class QNegPosMemory<T> : QNegPosMemory<T, ScalarReward>
    {
        public QNegPosMemory(int size, QRandom ran) : base(size, ran) { }
    }

    /// <summary>Convenience 1-arg alias for <see cref="QEpisodicMemory{T,RT}"/> using <see cref="ScalarReward"/>.</summary>
    public class QEpisodicMemory<T> : QEpisodicMemory<T, ScalarReward>
    {
        public QEpisodicMemory(int maxSize, QRandom ran) : base(maxSize, ran) { }
    }

    /// <summary>Convenience 1-arg alias for <see cref="QEpisodicNegPosMemory{T,RT}"/> using <see cref="ScalarReward"/>.</summary>
    public class QEpisodicNegPosMemory<T> : QEpisodicNegPosMemory<T, ScalarReward>
    {
        public QEpisodicNegPosMemory(int maxSize, QRandom ran) : base(maxSize, ran) { }
    }

    /// <summary>Convenience 1-arg alias for <see cref="DynaState{T,RT}"/> using <see cref="ScalarReward"/>.</summary>
    public class DynaState<T> : DynaState<T, ScalarReward>
    {
        public DynaState(int iters = 500, int max_histories = 0, bool positive_only = false)
            : base(iters, max_histories, positive_only) { }
    }
}

namespace HyperQ.MACE.Eval
{
    using HyperQ.Util;

    /// <summary>Convenience 1-arg alias for <see cref="MACEEvaluator{T,RT}"/> using <see cref="ScalarReward"/>.</summary>
    public class MACEEvaluator<T> : MACEEvaluator<T, ScalarReward>
    {
        public MACEEvaluator() : base() { }
    }
}
