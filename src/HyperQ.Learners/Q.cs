using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    public enum EvalMethodType
    {
        Max,
        Min,
        Avg
    };


    public struct Index
    {
        public uint Value;
    }

    public interface Q<T> : IArgMinMax<T>, IStateMapper<T>
    {
        QActionSpace<int> ActionSpace { get; }
        /// <summary>
        /// The action used to get a default value for a (s,a) pair.
        /// </summary>
        Func<double> DefaultValueFunc { get; set; }
        /// <summary>
        /// Merge the Q data in 'from' into this Q. New states in 'from' are added to this Q. Existing state Q
        /// data is maxxed or minned depending on the argument passed.
        /// </summary>
        /// <param name="from">The source to be copied into this Q</param>
        /// <param name="methodType">The type of evaluation performed on conflicting states</param>
        void MergeInto(Q<T> from, EvalMethodType methodType = EvalMethodType.Max);
        Q<T> Clone();
        double[] GetActionArray(T stateKey);
        /// <summary>
        /// Return the Q(s,a) value for the given state and action.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        double GetValue(T stateKey, int action);

        void SetValue(T stateKey, int action, double v);
        /// <summary>
        /// Quick access to the Q data as an array
        /// </summary>
        /// <param name="state">The state being queried</param>
        /// <param name="action">The action being queried, in action space</param>
        /// <returns></returns>
        double this[T state, int action] { get; set; }

        /// <summary>
        /// Compute the on-policy Q(s,a) update for the current state, the last state and the reward from the action taken.
        /// </summary>
        /// <param name="s">The state we are in defined in state space</param>
        /// <param name="a">The action taken in this state defined in action space</param>
        /// <param name="sprime">The result state from (s,a)</param>
        /// <param name="aprime">The chosen action,in action space, resulting in r</param>
        /// <param name="r">The reward for this choice (s,a)->(s',a')</param>
        /// <param name="gamma">Decay parameter</param>
        /// <param name="alpha">Learning parameter</param>
        /// <returns></returns>
        double OnPolicyUpdate(T s, int a, T sprime, int aprime, double r, HyperParams hp);
        /// <summary>
        /// Computes the off-policy update for the current state.
        /// </summary>
        /// <param name="s">The state we are in defined in state space</param>
        /// <param name="a">The action taken in this state defined in action space</param>
        /// <param name="sprime">The result state from (s,a)</param>
        /// <param name="r">The reward for this choice (s,a)->(s')</param>
        /// <param name="gamma">Decay parameter</param>
        /// <param name="alpha">Learning parameter</param>
        /// <returns></returns>
        double OffPolicyUpdate(T s, int a, T sprime, double r, HyperParams hp,EvalMethodType evalType);

        QAdvantage Advantage { get; }
        void ResetAdvantageTrace();

        void Dump(string label="Q",int level=0,bool dumpData=false);
        /// <summary>
        /// Returns the 2D Q matrix.
        /// </summary>
        double[,] AsMatrix { get; }

        /// <summary>
        /// Returns the Q values for only the given set of states.
        /// </summary>
        IDictionary<T, double[]> Snapshot(IEnumerable<T> states);

        /// <summary>
        /// Returns the shape of the Q, Item1 is the number of states, Item2 is the number of actions.
        /// </summary>
        Tuple<uint, uint> Shape { get; }
    }
}
