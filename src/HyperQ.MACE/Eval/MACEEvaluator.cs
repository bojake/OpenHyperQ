using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Learners;
using HyperQ.Env;
using HyperQ.Util;
using HyperQ.Util.Licensing;

namespace HyperQ.MACE.Eval
{
    /// <summary>
    /// Learning environment for the MACE method of multi-action learning.
    /// </summary>
    /// <typeparam name="T">The type for the state discretization, e.g. int, double, decimal</typeparam>
    [Serializable]
    public class MACEEvaluator<T,RT> where RT : IReward
    {
        private List<MACEMind<T>> _Minds = new List<MACEMind<T>>();
        protected QRandom _random = QRandom.Instance;
        public int MaxIterations { get; set; } = 0;

        public MACEEvaluator(QRandom ran = null)
        {
            FeatureGate.Require(HyperQFeatures.Mace);
            if (ran != null)
            {
                _random = ran;
            }
        }

        public virtual void Add(Q<T> m, IActionSelector<T> actionSelector = null)
        {
            if (actionSelector == null)
            {
                throw new ArgumentNullException("actionSelector", "The action selector can not be null.");
            }
            _Minds.Add(new MACEMind<T>(m,actionSelector));
        }

        /// <summary>
        /// Runs an episode in evaluation mode (no learning).
        /// </summary>
        /// <param name="env">The world environment</param>
        /// <param name="hp">The hyper params for the Q</param>
        public virtual void Episode(IMACEPvEEnv<T,RT> env, HyperParams hp)
        {
            bool done = false;
            env.Reset();
            env.Metrics.StartEpisode();
            env.Render();
            int iter = 0;
            QAction[] action = new QAction[_Minds.Count];
            while (!done)
            {
                env.Metrics.StartAction();
                T s = env.Discretize();
                for (int i = 0; i < _Minds.Count; i++)
                {
                    action[i] = _Minds[i].ActionSelector.SelectAction(_Minds[i].Mind, s, hp.Epsilon);
                }
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