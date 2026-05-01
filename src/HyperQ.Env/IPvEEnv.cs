using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Env
{
    public interface IPvEEnv<T,RT> : IEnv<T> where RT : IReward
    {
        /// <summary>
        /// Converts the current internal state to an ordinal value that is a 1:1 mapping of
        /// the internal state. This value can be used as the state in a Q learner.
        /// </summary>
        /// <returns></returns>
        T Discretize();
        /// <summary>
        /// Performs an action in the environment and returns the reward for that action and
        /// a boolean indicating if the environment has ended. This method updates the current
        /// state. 
        /// </summary>
        /// <param name="action">The action to take</param>
        /// <returns>Tuple of (reward, done indicator)</returns>
        EnvResult<RT> Step(QAction action);
    }

}
