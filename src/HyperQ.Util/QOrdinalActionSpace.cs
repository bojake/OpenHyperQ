using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HyperQ.Util
{
    [Serializable]
    public class QOrdinalActionSpace : QActionSpace<int>
    {
        /// <summary>
        /// The largest action seen so far, or -1 while no action has been seen. Actions are ordinals, so
        /// every action up to this one counts as known.
        /// </summary>
        private int _KnownActions = -1;
        private int _MaxActions = 0;
        public QOrdinalActionSpace(QRandom ran, int maxActions) : base(ran)
        {
            _MaxActions = maxActions;
        }

        /// <summary>
        /// Returns true if the action is known in this space, and false otherwise.
        /// </summary>
        /// <param name="action">The action, in action space</param>
        /// <returns>true or false</returns>
        public override bool IsKnownAction(int action)
        {
            return (action >= 0 && action <= _KnownActions);
        }

        /// <summary>
        /// Clones the contents of this instance into a new one and returns it.
        /// </summary>
        /// <returns></returns>
        public override QActionSpace<int> Clone()
        {
            QOrdinalActionSpace a = new QOrdinalActionSpace(Random, _MaxActions);
            a._KnownActions = _KnownActions;
            return (a);
        }
        /// <summary>
        /// Returns the number of known actions: one more than the largest action seen so far, since the
        /// actions are the ordinals 0 .. max. (This used to return the largest action itself, one short of
        /// the count, so callers iterating the known actions never reached the last one.)
        /// </summary>
        public override uint NumberOfKnownActions
        {
            get
            {
                return (uint)(_KnownActions + 1);
            }
        }
        /// <summary>
        /// Returns the maximum number of actions in this space.
        /// </summary>
        public override uint MaximumNumberOfActions
        {
            get {
                return (uint)_MaxActions;
            }
        }

        /// <summary>
        /// Creates an unknown random action in action space
        /// </summary>
        /// <returns></returns>
        public override int UnknownRandomAction()
        {
            return Random.Ran.Next(_MaxActions);
        }
        /// <summary>
        /// Returns a random action and updates the known actions
        /// </summary>
        /// <param name="includeUnknowns"></param>
        /// <returns></returns>
        public override int RandomAction(bool includeUnknowns = true)
        {
            int a = -1;
            if (includeUnknowns)
            {
                a = UnknownRandomAction();
            }
            else
                a = Random.Ran.Next(_KnownActions + 1);
            _KnownActions = Math.Max(_KnownActions, a);
            return a;
        }
        /// <summary>
        /// Returns the action space "action" from the given mapped index
        /// </summary>
        /// <param name="index">The mapped index of the action</param>
        /// <returns></returns>
        public override int FromIndex(uint index)
        {
            return (int) index;
        }
        /// <summary>
        /// Returns the mapped index for the given action space "action". Like the mapped spaces, mapping
        /// an action makes it known.
        /// </summary>
        /// <param name="action">The action, in action space</param>
        /// <returns></returns>
        public override uint ToIndex(int action)
        {
            if (action >= 0 && action < _MaxActions)
            {
                _KnownActions = Math.Max(_KnownActions, action);
            }
            return (uint)action;
        }

        // ── checkpoints ──

        public override bool SupportsCheckpoints { get { return true; } }
        public override void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            writer.Write(_MaxActions);
            writer.Write(_KnownActions);
        }
        public override void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            int maxActions = reader.ReadInt32();
            if (maxActions != _MaxActions)
            {
                throw new InvalidDataException("The checkpoint's action space has " + maxActions + " actions; this one has " + _MaxActions + ".");
            }
            _KnownActions = reader.ReadInt32();
        }
    }
}
