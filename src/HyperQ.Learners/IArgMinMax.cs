using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    public interface IArgMinMax<T>
    {
        /// <summary>
        /// Returns the action with the max Q(s,a) value for the given state.
        /// </summary>
        /// <param name="stateKey">The state key that will map to a state index in the adaptive Q matrix</param>
        /// <returns>A tuple with the action index and the value of Q at the state/action combination</returns>
        QAction ArgMax(T stateKey);

        /// <summary>
        /// Returns the action that has the minimum Q(s,a) value for the given state.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>A tuple with the action index and the value of Q at the state/action combination</returns>
        QAction ArgMin(T stateKey);
    }
}
