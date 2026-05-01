using HyperQ.Util;
using System;

namespace HyperQ.Env
{
    /// <summary>
    /// Convenience 1-argument form of the environment interface.
    /// Matches the contract exposed by the pre-.NET10 pre-built DLLs:
    /// Step returns a Tuple&lt;double,bool&gt; (reward, done) rather than the
    /// generic EnvResult&lt;RT&gt; of IPvEEnv&lt;T,RT&gt;. Environments that implement
    /// this interface (e.g. LEM, HuntTheWumpus) compile without modification.
    /// </summary>
    public interface IPvEEnv<T> : IEnv<T>
    {
        /// <summary>
        /// Converts the current state to a discrete representation for the Q learner.
        /// </summary>
        T Discretize();

        /// <summary>
        /// Performs an action and returns (reward, done).
        /// </summary>
        Tuple<double, bool> Step(QAction action);

        /// <summary>
        /// Performs a multi-agent action and returns (reward, done).
        /// </summary>
        Tuple<double, bool> Step(QAction[] action);
    }
}
