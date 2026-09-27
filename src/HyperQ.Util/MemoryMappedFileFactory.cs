using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace HyperQ.Util
{
    /// <summary>
    /// Factory for MemoryMappedFile instances. Instances are keyed by the
    /// backing file name so multiple index mappers can share the same
    /// mapping without creating duplicate mappings.
    /// The factory owns the lifetime of everything it hands out. If a
    /// consumer disposes a shared mapping the factory detects this and
    /// re-creates it on the next <see cref="Get"/> call, so a disposed
    /// instance can never poison the cache for the remaining consumers.
    /// Use <see cref="Flush"/> to force dirty pages to disk and
    /// <see cref="DisposeAll"/> to tear every mapping down.
    /// </summary>
    public static class MemoryMappedFileFactory
    {
        private sealed class Entry
        {
            public MemoryMappedFile Map;
            public FileStream Stream;
            public long Capacity;
        }

        private static readonly Dictionary<string, Entry> _maps = new Dictionary<string, Entry>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Get or create a MemoryMappedFile for the given path.
        /// </summary>
        /// <param name="path">Path to the .idx file backing the map.</param>
        /// <param name="capacity">
        /// Capacity to use when creating the file. A live mapping is reused
        /// as-is unless the requested capacity is larger, in which case the
        /// mapping is re-created so it covers the growth. The capacity never
        /// shrinks below the current length of the backing file.
        /// </param>
        /// <returns>A shared MemoryMappedFile instance.</returns>
        public static MemoryMappedFile Get(string path, long capacity)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A path is required", nameof(path));
            }
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must be positive");
            }

            lock (_lock)
            {
                Entry entry;
                if (_maps.TryGetValue(path, out entry) && IsAlive(entry) && capacity <= entry.Capacity)
                {
                    return entry.Map;
                }

                entry = Create(path, capacity, entry);
                _maps[path] = entry;
                return entry.Map;
            }
        }

        /// <summary>
        /// Flush the dirty pages of the mapping for the given path to disk.
        /// Does nothing when no live mapping exists for the path.
        /// </summary>
        /// <param name="path">Path the mapping was created for.</param>
        public static void Flush(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A path is required", nameof(path));
            }

            lock (_lock)
            {
                Entry entry;
                if (_maps.TryGetValue(path, out entry))
                {
                    FlushEntry(entry);
                }
            }
        }

        /// <summary>
        /// Flush and dispose every mapping and stream created by the factory.
        /// </summary>
        public static void DisposeAll()
        {
            lock (_lock)
            {
                foreach (Entry entry in _maps.Values)
                {
                    if (entry == null)
                    {
                        continue;
                    }
                    try
                    {
                        FlushEntry(entry);
                    }
                    catch (Exception)
                    {
                        // Best effort flush during teardown.
                    }
                    entry.Map?.Dispose();
                    entry.Stream?.Dispose();
                }
                _maps.Clear();
            }
        }

        private static bool IsAlive(Entry entry)
        {
            return entry != null
                && entry.Map != null
                && !entry.Map.SafeMemoryMappedFileHandle.IsClosed
                && !entry.Map.SafeMemoryMappedFileHandle.IsInvalid;
        }

        private static bool StreamIsAlive(FileStream stream)
        {
            // A disposed FileStream throws ObjectDisposedException from SafeFileHandle (the getter
            // flushes first), so probe the capability flags, which are simply cleared on dispose.
            return stream != null && (stream.CanRead || stream.CanWrite);
        }

        private static Entry Create(string path, long capacity, Entry previous)
        {
            string dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir))
            {
                dir = ".";
            }
            Directory.CreateDirectory(dir);

            // Reuse the backing stream when possible so the file stays open
            // across re-creations of the mapping.
            FileStream stream = StreamIsAlive(previous == null ? null : previous.Stream)
                ? previous.Stream
                : new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            // A prior larger mapping may have already extended the file, so
            // never shrink the coverage below the current file length.
            long effectiveCapacity = Math.Max(capacity, stream.Length);
            MemoryMappedFile map = MemoryMappedFile.CreateFromFile(
                stream,
                null,
                effectiveCapacity,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                leaveOpen: true);

            // The previous mapping (if any) is intentionally left undisposed:
            // existing consumers still hold views over it and their view
            // handles keep the OS section alive until they are released.
            return new Entry { Map = map, Stream = stream, Capacity = effectiveCapacity };
        }

        private static void FlushEntry(Entry entry)
        {
            if (!IsAlive(entry))
            {
                return;
            }
            using (MemoryMappedViewAccessor view = entry.Map.CreateViewAccessor(0, entry.Capacity, MemoryMappedFileAccess.ReadWrite))
            {
                view.Flush();
            }
            if (StreamIsAlive(entry.Stream))
            {
                entry.Stream.Flush(true);
            }
        }
    }
}
