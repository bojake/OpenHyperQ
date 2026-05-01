using System.IO;

namespace HyperQ.Util
{
    /// <summary>
    /// Marks a component whose state can be checkpointed to a binary stream
    /// and restored later. All implementations must handle versioning via the
    /// <see cref="CheckpointVersion"/> property.
    /// </summary>
    public interface ICheckpointable
    {
        /// <summary>
        /// The version of the checkpoint format this component writes.
        /// Readers must handle older versions gracefully.
        /// </summary>
        int CheckpointVersion { get; }

        /// <summary>Writes current state to the stream.</summary>
        void SaveCheckpoint(BinaryWriter writer);

        /// <summary>Restores state from the stream.</summary>
        void LoadCheckpoint(BinaryReader reader);
    }
}
