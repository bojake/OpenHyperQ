using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.MACE;
using HyperQ.Util;
using System;

namespace HyperQ.MultiHead.Eval
{
    /// <summary>
    /// Learning environment for the MACE method of multi-action learning.
    /// </summary>
    /// <typeparam name="T">The type for the state discretization, e.g. int, double, decimal</typeparam>
    [Serializable]
    public class MultiHeadQEvaluator<T, RT> where RT : IReward
    {
        private MultiHeadQLearner<T> _Q;
        public IActionSelector<T> ActionSelector { get; protected set; } = null;
        protected QRandom _random = QRandom.Instance;
        public int MaxIterations { get; set; } = 0;

        public MultiHeadQEvaluator(MultiHeadQLearner<T> qLearner, IActionSelector<T> actionSelector, QRandom ran = null)
        {
            if (qLearner == null)
            {
                throw new ArgumentNullException(nameof(qLearner));
            }
            if (actionSelector == null)
            {
                throw new ArgumentNullException(nameof(actionSelector));
            }
            if (ran != null)
            {
                _random = ran;
            }
            _Q = qLearner;
            ActionSelector = actionSelector;
        }

        /// <summary>
        /// Runs an episode in evaluation mode (no learning).
        /// </summary>
        /// <param name="env">The world environment</param>
        /// <param name="hp">The hyper params for the Q</param>
        public virtual void Episode(IPvEEnv<T, RT> env, HyperParams hp)
        {
            bool done = false;
            env.Reset();
            env.Metrics.StartEpisode();
            env.Render();
            int iter = 0;
            while (!done)
            {
                env.Metrics.StartAction();
                T s = env.Discretize();
                QAction action = ActionSelector.SelectAction(_Q, s, hp.Epsilon);
                EnvResult<RT> result = env.Step(action);
                env.Metrics.EndAction();
                env.Metrics.TotalReward.Add(result.Reward);
                done = result.Done;
                env.Render();
                iter++;
                if (MaxIterations > 0 && iter > MaxIterations)
                {
                    break;
                }
            }
            env.Metrics.EndEpisode(null);
        }

    }
}
