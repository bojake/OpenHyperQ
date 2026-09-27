using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// A self-describing container for one checkpointable component (a learner, a selector): a magic string,
    /// a format version, the component's type name, and the component's own checkpoint framed with its
    /// length. Files whose name ends in ".gz" are compressed. This replaces the BinaryFormatter
    /// serialization the samples used to persist trained learners.
    /// </summary>
    public static class LearnerCheckpoint
    {
        public const string MAGIC = "HQLEARNER";
        public const int FORMAT_VERSION = 1;

        /// <summary>The name recorded for a component: the runtime type without assembly versions.</summary>
        public static string TypeName(object component)
        {
            return component.GetType().ToString();
        }

        public static void Save(BinaryWriter writer, ICheckpointable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            writer.Write(MAGIC);
            writer.Write(FORMAT_VERSION);
            writer.Write(TypeName(component));
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    component.SaveCheckpoint(w);
                }
                byte[] payload = ms.ToArray();
                writer.Write(payload.Length);
                writer.Write(payload);
            }
        }

        /// <summary>
        /// Restores a component from the stream. The component must be of the type that was saved; the
        /// caller constructs it (with its action space, generator and options) before loading.
        /// </summary>
        public static void Load(BinaryReader reader, ICheckpointable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            string typeName;
            byte[] payload = ReadFrame(reader, out typeName);
            string expected = TypeName(component);
            if (!string.Equals(typeName, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The checkpoint holds a " + typeName + "; it cannot be loaded into a " + expected + ".");
            }
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader r = new BinaryReader(ms, Encoding.UTF8))
            {
                component.LoadCheckpoint(r);
            }
        }

        /// <summary>Reads past one framed component without restoring it. Returns the recorded type name.</summary>
        public static string Skip(BinaryReader reader)
        {
            string typeName;
            ReadFrame(reader, out typeName);
            return typeName;
        }

        private static byte[] ReadFrame(BinaryReader reader, out string typeName)
        {
            string magic = reader.ReadString();
            if (magic != MAGIC)
            {
                throw new InvalidDataException("Not a HyperQ learner checkpoint (magic '" + magic + "').");
            }
            int version = reader.ReadInt32();
            if (version > FORMAT_VERSION)
            {
                throw new InvalidDataException("Checkpoint format " + version + " is newer than the supported " + FORMAT_VERSION + ".");
            }
            typeName = reader.ReadString();
            int length = reader.ReadInt32();
            byte[] payload = reader.ReadBytes(length);
            if (payload.Length != length)
            {
                throw new EndOfStreamException("The checkpoint is truncated.");
            }
            return payload;
        }

        public static void SaveFile(string path, ICheckpointable component)
        {
            using (Stream s = OpenWrite(path))
            using (BinaryWriter w = new BinaryWriter(s, Encoding.UTF8))
            {
                Save(w, component);
            }
        }

        public static void LoadFile(string path, ICheckpointable component)
        {
            using (Stream s = OpenRead(path))
            using (BinaryReader r = new BinaryReader(s, Encoding.UTF8))
            {
                Load(r, component);
            }
        }

        /// <summary>Opens a file for writing, compressing when the name ends in ".gz".</summary>
        public static Stream OpenWrite(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            if (IsCompressed(path))
            {
                return new GZipStream(fs, CompressionLevel.Optimal);
            }
            return fs;
        }

        /// <summary>Opens a file for reading, decompressing when the name ends in ".gz".</summary>
        public static Stream OpenRead(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            if (IsCompressed(path))
            {
                return new GZipStream(fs, CompressionMode.Decompress);
            }
            return fs;
        }

        public static bool IsCompressed(string path)
        {
            return path != null && path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        }
    }
}
