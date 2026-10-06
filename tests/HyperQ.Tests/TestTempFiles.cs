using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace HyperQ.Test
{
    /// <summary>
    /// Temp space for tests that write files. The file-backed mappers keep their files open through process-wide
    /// caches (<see cref="MemoryMappedFileFactory"/> and <see cref="DataFileStreamFactory"/>) that only DisposeAll
    /// releases, so a test cannot delete its files when it ends. Instead each test takes a directory under one root per
    /// test run. The assembly cleanup releases the caches and deletes the root, and the assembly initialization removes
    /// roots that crashed or killed runs left behind.
    /// (These tests used to write straight into the temp directory and never clean up. Every node of a
    /// FileBackedHyperMapper is an 8 MB file, and over five months the runs left about 2.5 TB.)
    /// </summary>
    [TestClass]
    public class TestTempFiles
    {
        private static readonly string Parent = Path.Combine(Path.GetTempPath(), "HyperQ.Test");
        private static readonly string Root = Path.Combine(Parent, Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N"));

        /// <summary>A new, empty directory for one test, named after it.</summary>
        public static string NewDirectory(string name)
        {
            string dir = Path.Combine(Root, name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Removes the roots of earlier runs that ended without their cleanup, once they are a day old.</summary>
        [AssemblyInitialize]
        public static void RemoveStaleRuns(TestContext context)
        {
            if (!Directory.Exists(Parent))
            {
                return;
            }
            foreach (string dir in Directory.EnumerateDirectories(Parent))
            {
                if (Directory.GetLastWriteTimeUtc(dir) < DateTime.UtcNow.AddDays(-1))
                {
                    TryDelete(dir);
                }
            }
        }

        /// <summary>Releases the cached mappings and streams, then deletes this run's files.</summary>
        [AssemblyCleanup]
        public static void DeleteAll()
        {
            MemoryMappedFileFactory.DisposeAll();
            DataFileStreamFactory.DisposeAll();
            // Views and streams that tests never disposed hold their files until they are finalized.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            TryDelete(Root);
        }

        private static void TryDelete(string dir)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                    return;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                Thread.Sleep(200);
            }
        }
    }
}
