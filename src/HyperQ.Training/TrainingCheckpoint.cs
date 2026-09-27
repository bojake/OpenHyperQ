using HyperQ.Learners;
using HyperQ.Util;
using System;
using System.IO;

namespace HyperQ.Training
{
    /// <summary>
    /// Aggregates and coordinates checkpoint save/restore across all training components.
    /// Provides a single file-based save/load API for <see cref="PvESARSATrainer{T}"/>
    /// to support batch learning and crash recovery.
    /// </summary>
    public static class TrainingCheckpoint
    {
        /// <summary>
        /// File format version for forward-compatibility detection. Version 2 frames the optional action
        /// selector payload with its length so it can be skipped; version 1 stored it inline.
        /// </summary>
        public const int FORMAT_VERSION = 2;

        /// <summary>File extension for HyperQ checkpoint files.</summary>
        public const string FILE_EXTENSION = ".hqc";

        /// <summary>
        /// Saves a complete training checkpoint to disk.
        /// </summary>
        /// <param name="path">File path for the checkpoint.</param>
        /// <param name="q">The Q-learner (must implement <see cref="ICheckpointable"/>).</param>
        /// <param name="hp">Current hyperparameters with decayed positions.</param>
        /// <param name="episodeCount">Number of completed episodes.</param>
        /// <param name="elapsedMs">Total wall-clock training time in milliseconds.</param>
        /// <param name="advantageMode">Active advantage estimation mode.</param>
        /// <param name="sweepMode">Active Dyna sweep mode.</param>
        /// <param name="selector">Optional: action selector (must implement <see cref="ICheckpointable"/>).</param>
        public static void Save(
            string path,
            ICheckpointable q,
            HyperParams hp,
            int episodeCount,
            long elapsedMs = 0,
            AdvantageMode advantageMode = AdvantageMode.OneStep,
            DynaSweepMode sweepMode = DynaSweepMode.Uniform,
            ICheckpointable selector = null)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                // ── Header ──
                writer.Write(FORMAT_VERSION);
                writer.Write(episodeCount);
                writer.Write(elapsedMs);
                writer.Write((int)advantageMode);
                writer.Write((int)sweepMode);

                // ── HyperParams ──
                hp.SaveCheckpoint(writer);

                // ── Q-Table ──
                q.SaveCheckpoint(writer);

                // ── Action Selector (optional) ──
                if (selector != null)
                {
                    writer.Write(true);
                    // Frame the selector payload with its length so a restore
                    // without a selector can skip over it cleanly.
                    using (var selectorStream = new MemoryStream())
                    {
                        using (var selectorWriter = new BinaryWriter(selectorStream, System.Text.Encoding.UTF8, leaveOpen: true))
                        {
                            selector.SaveCheckpoint(selectorWriter);
                        }

                        byte[] selectorBytes = selectorStream.ToArray();
                        writer.Write(selectorBytes.Length);
                        writer.Write(selectorBytes);
                    }
                }
                else
                {
                    writer.Write(false);
                }
            }
        }

        /// <summary>
        /// Restores a training checkpoint from disk.
        /// </summary>
        /// <param name="path">Path to the checkpoint file.</param>
        /// <param name="q">The Q-learner to restore into (must implement <see cref="ICheckpointable"/>).</param>
        /// <param name="hp">HyperParams to restore into.</param>
        /// <param name="selector">Optional: action selector to restore into.</param>
        /// <returns>A <see cref="CheckpointMetadata"/> with episode count and configuration to resume from.</returns>
        public static CheckpointMetadata Load(
            string path,
            ICheckpointable q,
            HyperParams hp,
            ICheckpointable selector = null)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                // ── Header ──
                int version = reader.ReadInt32();
                if (version > FORMAT_VERSION)
                    throw new InvalidOperationException(
                        $"Checkpoint file version {version} is newer than supported version {FORMAT_VERSION}.");

                int episodeCount = reader.ReadInt32();
                long elapsedMs = reader.ReadInt64();
                var advantageMode = (AdvantageMode)reader.ReadInt32();
                var sweepMode = (DynaSweepMode)reader.ReadInt32();

                // ── HyperParams ──
                hp.LoadCheckpoint(reader);

                // ── Q-Table ──
                q.LoadCheckpoint(reader);

                // ── Action Selector (optional) ──
                bool hasSelector = reader.ReadBoolean();
                if (hasSelector)
                {
                    if (version >= 2)
                    {
                        // Version 2 frames the selector payload with its length so a restore without a
                        // selector can skip over it cleanly.
                        int selectorLength = reader.ReadInt32();
                        byte[] selectorBytes = reader.ReadBytes(selectorLength);

                        if (selector != null)
                        {
                            using (var selectorStream = new MemoryStream(selectorBytes))
                            {
                                using (var selectorReader = new BinaryReader(selectorStream))
                                {
                                    selector.LoadCheckpoint(selectorReader);
                                }
                            }
                        }
                    }
                    else if (selector != null)
                    {
                        // Version 1 wrote the selector state inline, right after the Q table. It is the last
                        // section of the file, so a caller without a selector simply stops reading here.
                        selector.LoadCheckpoint(reader);
                    }
                }

                return new CheckpointMetadata
                {
                    EpisodeCount = episodeCount,
                    ElapsedMilliseconds = elapsedMs,
                    AdvantageMode = advantageMode,
                    SweepMode = sweepMode,
                    FormatVersion = version
                };
            }
        }
    }

    /// <summary>
    /// Metadata returned from <see cref="TrainingCheckpoint.Load"/> to allow the caller
    /// to resume training from the correct episode and configuration.
    /// </summary>
    public class CheckpointMetadata
    {
        /// <summary>Number of episodes completed when the checkpoint was saved.</summary>
        public int EpisodeCount { get; set; }

        /// <summary>Total wall-clock training time in milliseconds.</summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>Advantage mode that was active when the checkpoint was saved.</summary>
        public AdvantageMode AdvantageMode { get; set; }

        /// <summary>Dyna sweep mode that was active when the checkpoint was saved.</summary>
        public DynaSweepMode SweepMode { get; set; }

        /// <summary>Format version of the loaded checkpoint file.</summary>
        public int FormatVersion { get; set; }
    }
}
