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
        /// <summary>
        /// Sets the value of (s, a) in every estimate the learner keeps, so that GetValue reports
        /// <paramref name="v"/> afterwards. SetValue may write a single estimate: a double-Q learner writes its
        /// current table and moves on to the other one, which suits applying an update. This suits initializing a
        /// value, as <see cref="LayeredHyperQ{T}"/> does when it warm-starts a new fine state from its parent. The
        /// default writes through SetValue, which is right for a learner that keeps a single estimate.
        /// </summary>
        /// <param name="stateKey">The state</param>
        /// <param name="action">The action, in action space</param>
        /// <param name="v">The value</param>
        void InitializeValue(QState<T> stateKey, int action, double v)
        {
            SetValue(stateKey, action, v);
        }
    }
}
