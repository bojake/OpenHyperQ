using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Env;
using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// Implements the SARSA {(S,a)<-R : (S',a')} evaluation method, supporting on-policy and off-policy evaluation. Use this
    /// class to evaluate the model that is trained using the PvESARSATrainer class.
    /// </summary>
    /// <typeparam name="T">The type for the state discretization, e.g. int, double, decimal</typeparam>
    [Serializable]
    public class QEvaluator<T,RT> where RT : IReward
    {
        protected Q<T> _Q;
        public IActionSelector<T> ActionSelector { get; protected set; } = null;
        public int MaxIterations { get; set; } = 0;

        public QEvaluator(Q<T> q, IActionSelector<T> actionSelector)
        {
            _Q = q;
            ActionSelector = actionSelector;
            if (ActionSelector == null)
            {
                throw new ArgumentNullException("actionSelector", "The action selector can not be null.");
            }
        }

        /// <summary>
        /// Runs an episode
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
                QAction a = ActionSelector.SelectAction(_Q, env.Discretize(), hp.Epsilon);
                EnvResult<RT> result = env.Step(a);
                env.Metrics.EndAction(a, ActionSelector.LastActionWasRandom);
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