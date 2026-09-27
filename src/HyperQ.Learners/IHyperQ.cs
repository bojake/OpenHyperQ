using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// The Hyper Q interface that uses a decimal type for the state management.
    /// </summary>
    public interface IHyperQ<T> : Q<QState<T>>
    {
        /// <summary>
        /// Returns true if the given state has been explored at least once, meaning that at least
        /// one action has been chosen from this state.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns></returns>
        bool IsKnownState(QState<T> stateKey);
        /// <summary>
        /// Returns true if the given action has been explored
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        bool IsKnownAction(int action);

        /// <summary>
        /// Reference label for toString() output
        /// </summary>
        string Label { get; set; }
        /// <summary>
        /// Links the state mapper and index repository to those given
        /// </summary>
        /// <param name="stateMap"></param>
        /// <param name="actionMap"></param>
        void Link(MemoryBackedHyperMapper<T> stateMap, QActionSpace<int> actionSpace);
        /// <summary>
        /// Gets the known/explored action array for the given state
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns></returns>
        double[] GetKnownActionArray(QState<T> stateKey);
        /// <summary>
        /// Enumerates the (action index, Q value) pairs that have been learned for the given state, keyed by the
        /// action-space index (see <see cref="QActionSpace{T}.ToIndex"/>). Unlike <see cref="Q{T}.GetActionArray"/>
        /// this neither fills in defaults for actions that were never tried nor modifies the learner.
        /// </summary>
        /// <param name="stateKey">The state to query</param>
        IEnumerable<KeyValuePair<uint, double>> KnownActionValues(QState<T> stateKey);
        /// <summary>
        /// Returns true if the index repository must be linked with other instances, such as with
        /// a Double-Q implementation where the repositories must be linked for index coherence to
        /// be preserved.
        /// </summary>
        bool RequiresIndexRepositoryLinking { get; }
    }
}
