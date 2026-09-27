using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HyperQ.Util
{
    /// <summary>
    /// Abstracted mapped action space implementation that requires the environment to implement the
    /// UnknownRandomAction and the MaxNumActions attribute.
    /// </summary>
    /// <typeparam name="T">The underlying type for the action, typically int</typeparam>
    [Serializable]
    public abstract class QMappedActionSpace<T> : QActionSpace<T>
    {
        protected IIndexMapper<T> _ActionMap = new IndexMapper<T>(0);
        public QMappedActionSpace(QRandom ran) : base(ran)
        {
        }

        public virtual bool IsValidIndex(uint index)
        {
            return _ActionMap.IsKnownIndex(index);
        }
        /// <summary>
        /// Returns true if the action is known in this space, and false otherwise.
        /// </summary>
        /// <param name="action">The action, in action space</param>
        /// <returns>true or false</returns>
        public override bool IsKnownAction(T action) 
        {
            return _ActionMap.Known(action);
        }

        /// <summary>
        /// Copies the action map (as a clone) to the given instance.
        /// </summary>
        /// <param name="dest">The instance to receive the action map</param>
        protected virtual void CopyTo(QMappedActionSpace<T> dest)
        {
            dest._ActionMap = _ActionMap.Clone();
        }
        /// <summary>
        /// Returns the number of mapped actions
        /// </summary>
        public override uint NumberOfKnownActions
        {
            get
            {
                return _ActionMap.MappingCount;
            }
        }

        /// <summary>
        /// Returns a random action from the known space, or from the unknown space
        /// when the includeUnknowns is true.
        /// </summary>
        /// <param name="includeUnknowns">True if an unknown action is to be returned along with knowns.</param>
        /// <returns></returns>
        public override T RandomAction(bool includeUnknowns = true)
        {
            if (includeUnknowns)
            {
                return UnknownRandomAction();
            }
            return FromIndex(_ActionMap.RandomKey(Random));
        }

        /// <summary>
        /// Returns the action space "action" from the given mapped index
        /// </summary>
        /// <param name="index">The mapped index of the action</param>
        /// <returns></returns>
        public override T FromIndex(uint index)
        {
            return _ActionMap.FromIndex(index);
        }

        /// <summary>
        /// Returns the mapped index for the given action space "action"
        /// </summary>
        /// <param name="action">The action, in action space</param>
        /// <returns></returns>
        public override uint ToIndex(T action)
        {
            return _ActionMap.ToIndex(action);
        }

        // ── checkpoints ──

        /// <summary>
        /// Serializer for the action keys in checkpoints. Override it for an action type without a built-in
        /// serializer (see <see cref="QKeySerializerFactory"/>).
        /// </summary>
        protected virtual IQKeySerializer<T> ActionKeySerializer { get { return QKeySerializerFactory.GetDefault<T>(); } }

        public override bool SupportsCheckpoints { get { return _ActionMap is IndexMapper<T>; } }

        public override void SaveCheckpoint(BinaryWriter writer)
        {
            IndexMapper<T> map = _ActionMap as IndexMapper<T>;
            if (map == null)
            {
                throw new NotSupportedException("Only a memory backed action map can be written to a checkpoint.");
            }
            writer.Write(CheckpointVersion);
            map.SaveCheckpoint(writer, ActionKeySerializer);
        }

        public override void LoadCheckpoint(BinaryReader reader)
        {
            IndexMapper<T> map = _ActionMap as IndexMapper<T>;
            if (map == null)
            {
                throw new NotSupportedException("Only a memory backed action map can be restored from a checkpoint.");
            }
            int version = reader.ReadInt32();
            map.LoadCheckpoint(reader, ActionKeySerializer);
        }
    }
}
