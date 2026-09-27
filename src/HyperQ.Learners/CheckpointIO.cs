using System;
using System.Collections.Generic;
using System.IO;
using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// Shared pieces of the learners' checkpoint formats: action spaces, state maps, value matrices and
    /// sparse rows. Every writer here is paired with a reader that consumes exactly what was written.
    /// </summary>
    internal static class CheckpointIO
    {
        /// <summary>
        /// Writes the action space's own checkpoint when it supports one, so that the action-to-column mapping
        /// a learner's rows depend on is restored together with the rows.
        /// </summary>
        public static void WriteActionSpace(BinaryWriter writer, QActionSpace<int> space)
        {
            bool supported = space != null && space.SupportsCheckpoints;
            writer.Write(supported);
            if (supported)
            {
                space.SaveCheckpoint(writer);
            }
        }

        public static void ReadActionSpace(BinaryReader reader, QActionSpace<int> space)
        {
            bool present = reader.ReadBoolean();
            if (!present)
            {
                return;
            }
            if (space == null || !space.SupportsCheckpoints)
            {
                throw new InvalidDataException("The checkpoint carries action space state but the current action space (" +
                    (space == null ? "null" : space.GetType().Name) + ") cannot restore it.");
            }
            space.LoadCheckpoint(reader);
        }

        public static void WriteMatrix(BinaryWriter writer, double[,] m)
        {
            if (m == null)
            {
                writer.Write(false);
                return;
            }
            writer.Write(true);
            int rows = m.GetLength(0);
            int cols = m.GetLength(1);
            writer.Write(rows);
            writer.Write(cols);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    writer.Write(m[r, c]);
        }

        public static double[,] ReadMatrix(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
            {
                return null;
            }
            int rows = reader.ReadInt32();
            int cols = reader.ReadInt32();
            double[,] m = new double[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    m[r, c] = reader.ReadDouble();
            return m;
        }

        /// <summary>Writes a list of sparse rows: the row count, then per row the (column, value) pairs.</summary>
        public static void WriteRows(BinaryWriter writer, List<QRow> rows)
        {
            if (rows == null)
            {
                writer.Write(-1);
                return;
            }
            writer.Write(rows.Count);
            foreach (QRow row in rows)
            {
                writer.Write(row.Count);
                foreach (KeyValuePair<uint, double> kv in row)
                {
                    writer.Write(kv.Key);
                    writer.Write(kv.Value);
                }
            }
        }

        public static List<QRow> ReadRows(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0)
            {
                return null;
            }
            List<QRow> rows = new List<QRow>(count);
            for (int i = 0; i < count; i++)
            {
                int n = reader.ReadInt32();
                QRow row = new QRow();
                for (int j = 0; j < n; j++)
                {
                    uint col = reader.ReadUInt32();
                    row[col] = reader.ReadDouble();
                }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// Writes a state index mapper. Only the memory backed <see cref="IndexMapper{T}"/> can be checkpointed;
        /// a file backed mapper already persists itself.
        /// </summary>
        public static void WriteIndexMapper<T>(BinaryWriter writer, IIndexMapper<T> map, IQKeySerializer<T> keys)
        {
            IndexMapper<T> m = map as IndexMapper<T>;
            if (m == null)
            {
                throw new NotSupportedException("Only a memory backed IndexMapper can be written to a checkpoint (found " +
                    (map == null ? "null" : map.GetType().Name) + ").");
            }
            m.SaveCheckpoint(writer, keys);
        }

        public static void ReadIndexMapper<T>(BinaryReader reader, IIndexMapper<T> map, IQKeySerializer<T> keys)
        {
            IndexMapper<T> m = map as IndexMapper<T>;
            if (m == null)
            {
                throw new NotSupportedException("Only a memory backed IndexMapper can be restored from a checkpoint (found " +
                    (map == null ? "null" : map.GetType().Name) + ").");
            }
            m.LoadCheckpoint(reader, keys);
        }

        /// <summary>
        /// Loads the checkpoint of a component that was written by <see cref="ICheckpointable.SaveCheckpoint"/>,
        /// failing with a clear message when the component does not support checkpoints.
        /// </summary>
        public static ICheckpointable Checkpointable(object component, string role)
        {
            ICheckpointable c = component as ICheckpointable;
            if (c == null)
            {
                throw new NotSupportedException(role + " (" + (component == null ? "null" : component.GetType().Name) +
                    ") does not support checkpoints.");
            }
            return c;
        }
    }
}
