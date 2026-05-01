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
    /// </summary>
    public static class MemoryMappedFileFactory
    {
        private static readonly Dictionary<string, MemoryMappedFile> _maps = new Dictionary<string, MemoryMappedFile>();
        private static readonly Dictionary<string, FileStream> _streams = new Dictionary<string, FileStream>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Get or create a MemoryMappedFile for the given path.
        /// </summary>
        /// <param name="path">Path to the .idx file backing the map.</param>
        /// <param name="capacity">Capacity to use when creating the file.</param>
        /// <returns>A shared MemoryMappedFile instance.</returns>
        public static MemoryMappedFile Get(string path, long capacity)
        {
            lock (_lock)
            {
                if (_maps.TryGetValue(path, out var mmf))
                {
                    return mmf;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                mmf = MemoryMappedFile.CreateFromFile(
                    fs,
                    null,
                    capacity,
                    MemoryMappedFileAccess.ReadWrite,
                    HandleInheritability.None,
                    leaveOpen: true);
                _maps[path] = mmf;
                _streams[path] = fs;
                return mmf;
            }
        }

        public static void DisposeAll()
        {
            lock (_lock)
            {
                foreach (var mmf in _maps.Values)
                {
                    mmf?.Dispose();
                }
                foreach (var fs in _streams.Values)
                {
                    fs?.Dispose();
                }
                _maps.Clear();
                _streams.Clear();
            }
        }
    }
}
