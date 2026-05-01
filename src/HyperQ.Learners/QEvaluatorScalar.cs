using HyperQ.Util;
using HyperQ.Env;

namespace HyperQ.Learners
{
    /// <summary>
    /// Convenience 1-argument alias for <see cref="QEvaluator{T,RT}"/> that uses
    /// <see cref="ScalarReward"/> as the reward type, matching the pre-.NET10 DLL contract.
    /// </summary>
    public class QEvaluator<T> : QEvaluator<T, ScalarReward>
    {
        public QEvaluator(Q<T> q, IActionSelector<T> actionSelector)
            : base(q, actionSelector) { }

        /// <summary>
        /// Runs an episode using the 1-arg legacy IPvEEnv&lt;T&gt; (Tuple-returning) interface.
        /// Adapts the Tuple&lt;double,bool&gt; return from Step into the evaluator loop.
        /// </summary>
        public virtual void Episode(IPvEEnv<T> env, HyperQ.Util.HyperParams hp)
        {
            bool done = false;
            env.Reset();
            env.Metrics.StartEpisode();
            env.Render();
            int iter = 0;
            while (!done)
            {
                env.Metrics.StartAction();
                QAction a = ActionSelector.SelectAction(_Q, env.Discretize(), hp.Epsilon);
                var result = env.Step(a);
                env.Metrics.EndAction(a, ActionSelector.LastActionWasRandom);
                env.Metrics.TotalReward.Add((IReward)(ScalarReward)result.Item1);
                done = result.Item2;
                env.Render();
                iter++;
                if (MaxIterations > 0 && iter > MaxIterations)
                    break;
            }
            env.Metrics.EndEpisode(null);
        }
    }
}
