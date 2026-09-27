using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HyperQ.Learners;
using HyperQ.Util;

namespace HyperQ.MACE
{
    /// <summary>
    /// Checkpoints a set of MACE minds: every mind's learner and, when it keeps state, its action selector.
    /// The minds are constructed by the caller exactly as they were when saved (same learner types, action
    /// spaces and selectors); the checkpoint restores their contents.
    /// </summary>
    public static class MACECheckpoint
    {
        public const string MAGIC = "HQMINDS";
        public const int FORMAT_VERSION = 1;

        public static void Save<T>(BinaryWriter writer, IReadOnlyList<MACEMind<T>> minds)
        {
            if (minds == null) throw new ArgumentNullException(nameof(minds));
            writer.Write(MAGIC);
            writer.Write(FORMAT_VERSION);
            writer.Write(minds.Count);
            for (int i = 0; i < minds.Count; i++)
            {
                MACEMind<T> mind = minds[i];
                ICheckpointable learner = mind.Mind as ICheckpointable;
                if (learner == null)
                {
                    throw new NotSupportedException("Mind " + i + " (" + mind.Mind.GetType().Name + ") does not support checkpoints.");
                }
                LearnerCheckpoint.Save(writer, learner);
                ICheckpointable selector = mind.ActionSelector as ICheckpointable;
                writer.Write(selector != null);
                if (selector != null)
                {
                    LearnerCheckpoint.Save(writer, selector);
                }
            }
        }

        public static void Load<T>(BinaryReader reader, IReadOnlyList<MACEMind<T>> minds)
        {
            if (minds == null) throw new ArgumentNullException(nameof(minds));
            string magic = reader.ReadString();
            if (magic != MAGIC)
            {
                throw new InvalidDataException("Not a HyperQ minds checkpoint (magic '" + magic + "').");
            }
            int version = reader.ReadInt32();
            if (version > FORMAT_VERSION)
            {
                throw new InvalidDataException("Minds checkpoint format " + version + " is newer than the supported " + FORMAT_VERSION + ".");
            }
            int count = reader.ReadInt32();
            if (count != minds.Count)
            {
                throw new InvalidDataException("The checkpoint holds " + count + " minds but " + minds.Count + " were configured.");
            }
            for (int i = 0; i < count; i++)
            {
                MACEMind<T> mind = minds[i];
                ICheckpointable learner = mind.Mind as ICheckpointable;
                if (learner == null)
                {
                    throw new NotSupportedException("Mind " + i + " (" + mind.Mind.GetType().Name + ") does not support checkpoints.");
                }
                LearnerCheckpoint.Load(reader, learner);
                bool hasSelector = reader.ReadBoolean();
                if (hasSelector)
                {
                    ICheckpointable selector = mind.ActionSelector as ICheckpointable;
                    if (selector != null)
                    {
                        LearnerCheckpoint.Load(reader, selector);
                    }
                    else
                    {
                        // The saved selector kept state (a policy) but the configured one does not; the
                        // learner is still restored.
                        LearnerCheckpoint.Skip(reader);
                    }
                }
            }
        }

        public static void SaveFile<T>(string path, IReadOnlyList<MACEMind<T>> minds)
        {
            using (Stream s = LearnerCheckpoint.OpenWrite(path))
            using (BinaryWriter w = new BinaryWriter(s, Encoding.UTF8))
            {
                Save(w, minds);
            }
        }

        public static void LoadFile<T>(string path, IReadOnlyList<MACEMind<T>> minds)
        {
            using (Stream s = LearnerCheckpoint.OpenRead(path))
            using (BinaryReader r = new BinaryReader(s, Encoding.UTF8))
            {
                Load(r, minds);
            }
        }
    }
}
