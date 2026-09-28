using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HyperQ.Util
{
    [Serializable]
    public abstract class QActionSpace<T> : ICheckpointable
    {
        /// <summary>
        /// The source random actions are drawn from. Learners, selectors and trainers built on this space draw
        /// from it too unless they are given a source of their own.
        /// </summary>
        public QRandom Random { get; protected set; }
        public QActionSpace(QRandom ran)
        {
            Random = ran ?? throw new ArgumentNullException(nameof(ran));
        }

        /// <summary>
        /// Returns the number of known actions in this space
        /// </summary>
        public abstract uint NumberOfKnownActions
        {
            get;
        }

        /// <summary>
        /// Returns the maximum number of actions in this space.
        /// </summary>
        public abstract uint MaximumNumberOfActions
        {
            get;
        }
        /// <summary>
        /// Returns true if the action is known in this space, and false otherwise.
        /// </summary>
        /// <param name="action">The action, in action space</param>
        /// <returns>true or false</returns>
        public abstract bool IsKnownAction(T action);

        public abstract QActionSpace<T> Clone();
        /// <summary>
        /// Creates an unknown random action
        /// </summary>
        /// <returns></returns>
        public abstract T UnknownRandomAction();

        /// <summary>
        /// Returns a random action from the known space, or from the unknown space
        /// when the includeUnknowns is true.
        /// </summary>
        /// <param name="includeUnknowns">True if an unknown action is to be returned along with knowns.</param>
        /// <returns></returns>
        public abstract T RandomAction(bool includeUnknowns = true);

        /// <summary>
        /// Returns the action space "action" from the given mapped index
        /// </summary>
        /// <param name="index">The mapped index of the action</param>
        /// <returns></returns>
        public abstract T FromIndex(uint index);

        /// <summary>
        /// Returns the mapped index for the given action space "action"
        /// </summary>
        /// <param name="action">The action, in action space</param>
        /// <returns></returns>
        public abstract uint ToIndex(T action);

        // ── checkpoints ──

        /// <summary>
        /// True when this action space can save and restore its action-to-index mapping. Learners save it with
        /// their rows, because the column an action occupies depends on the order the actions were seen.
        /// </summary>
        public virtual bool SupportsCheckpoints { get { return false; } }
        public virtual int CheckpointVersion { get { return 1; } }
        public virtual void SaveCheckpoint(BinaryWriter writer)
        {
            throw new NotSupportedException(GetType().Name + " does not support checkpoints.");
        }
        public virtual void LoadCheckpoint(BinaryReader reader)
        {
            throw new NotSupportedException(GetType().Name + " does not support checkpoints.");
        }
    }
}
