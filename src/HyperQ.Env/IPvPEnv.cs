using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Env
{
    /// <summary>
    /// Environment that has multiple actors. Each actor gets to make a step in the
    /// environment as part of their turn-based step update.
    /// </summary>
    public interface IPvPEnv<T, RT> : IEnv<T> where RT : IReward
    {
        /// <summary>
        /// Converts the current internal state of the environment as observed by the given player. The
        /// mapping is an ordinal value that is a 1:1 mapping of the internal state. This value can be 
        /// used as the state in a Q learner.
        /// </summary>
        /// <returns></returns>
        T Discretize(int player, int vs_player);
        /// <summary>
        /// Performs an action in the environment for the given player and returns the reward for that action and
        /// a boolean indicating if the environment has ended. This method updates the current
        /// state. 
        /// </summary>
        /// <param name="action">The action to take</param>
        /// <returns>Tuple of (reward, done indicator)</returns>
        EnvResult<RT> Step(int player, int vs_player, QAction action);
    }
}
