using System;
using System.Collections.Generic;
using System.IO;

namespace HyperQ.Util
{
    /// <summary>
    /// Factory for FileStream instances for .dat files. Instances are keyed by the
    /// backing file name so multiple index mappers can share the same stream.
    /// </summary>
    public static class DataFileStreamFactory
    {
        private static readonly Dictionary<string, FileStream> _streams = new Dictionary<string, FileStream>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Get or create a FileStream for the given path.
        /// </summary>
        /// <param name="path">Path to the .dat file backing the map.</param>
        /// <returns>A shared FileStream instance.</returns>
        public static FileStream Get(string path)
        {
            lock (_lock)
            {
                if (_streams.TryGetValue(path, out var fs))
                {
                    return fs;
                }
                string dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir))
                {
                    dir = ".";
                }
                else
                {
                    Directory.CreateDirectory(dir);
                }
                fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                _streams[path] = fs;
                return fs;
            }
        }
    }
}
