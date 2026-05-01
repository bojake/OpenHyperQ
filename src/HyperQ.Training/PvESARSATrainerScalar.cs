using HyperQ.Learners;
using HyperQ.Env;
using HyperQ.Util;
using System;

namespace HyperQ.Training
{
    /// <summary>
    /// Adapts a 1-arg <see cref="IPvEEnv{T}"/> (Tuple-returning Step) to the
    /// <see cref="IPvEEnv{T,ScalarReward}"/> interface expected by <see cref="PvESARSATrainer{T}"/>.
    /// </summary>
    internal sealed class LegacyEnvAdapter<T> : IPvEEnv<T, ScalarReward>
    {
        private readonly IPvEEnv<T> _inner;

        public LegacyEnvAdapter(IPvEEnv<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public T Discretize() => _inner.Discretize();

        public EnvResult<ScalarReward> Step(QAction action)
        {
            var t = _inner.Step(action);
            return new EnvResult<ScalarReward>((ScalarReward)t.Item1, t.Item2);
        }

        public void Reset() => _inner.Reset();
        public void Render() => _inner.Render();
        public EnvMetrics Metrics => _inner.Metrics;
    }

    public partial class PvESARSATrainer<T>
    {
        /// <summary>
        /// Overload that accepts the legacy 1-arg <see cref="IPvEEnv{T}"/> interface.
        /// Wraps it in a <see cref="LegacyEnvAdapter{T}"/> before calling the main Episode path.
        /// </summary>
        public virtual void Episode(IPvEEnv<T> env, HyperParams hp)
            => Episode(new LegacyEnvAdapter<T>(env), hp);

        /// <summary>
        /// Overload that accepts the legacy 1-arg <see cref="IPvEEnv{T}"/> interface.
        /// </summary>
        public virtual void Warmup(IPvEEnv<T> env, HyperParams hp, int n_episodes = 1000, IActionSelector<T> warmupSelector = null)
            => Warmup(new LegacyEnvAdapter<T>(env), hp, n_episodes, warmupSelector);
    }
}
