using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;

namespace HyperQ.MACE
{
    /// <summary>
    /// The base type for the action selector for the MACE learning
    /// framework.
    /// </summary>
    /// <typeparam name="T">The state type</typeparam>
    public interface IMACEActionSelector<T>
    {
        /// <summary>
        /// Chooses an action for a given Q
        /// </summary>
        /// <param name="state">The state from which to select an action</param>
        /// <param name="hp">The hyper parameters for the learner</param>
        int SelectAction(T state, HyperParams hp);
    }
}
