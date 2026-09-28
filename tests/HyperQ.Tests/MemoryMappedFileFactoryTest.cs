using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using HyperQ.Util;

namespace HyperQ.Test
{
    [TestClass]
    public class MemoryMappedFileFactoryTest
    {
        private string MakeTempDir()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "MMFTest" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(baseDir);
            return baseDir;
        }

        [TestMethod]
        public void FactoryReturnsSameInstanceForSamePath()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "MMFTest" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(baseDir);
            string basePath = Path.Combine(baseDir, "map");

            FileIndexMapper<int> m1 = new FileIndexMapper<int>(basePath);
            FileIndexMapper<int> m2 = new FileIndexMapper<int>(basePath);

            FieldInfo fiMap = typeof(FileIndexMapper<int>).GetField("_indexMap", BindingFlags.NonPublic | BindingFlags.Instance);
            var mmf1 = fiMap.GetValue(m1);
            var mmf2 = fiMap.GetValue(m2);

            Assert.AreSame(mmf1, mmf2, "Factory did not return the same MemoryMappedFile instance");

            FieldInfo fiStream = typeof(FileIndexMapper<int>).GetField("_dataStream", BindingFlags.NonPublic | BindingFlags.Instance);
            var fs1 = fiStream.GetValue(m1);
            var fs2 = fiStream.GetValue(m2);

            Assert.AreSame(fs1, fs2, "Factory did not return the same FileStream instance");
        }

        [TestMethod]
        public void GetReturnsSameInstanceForSamePathAndCapacity()
        {
            string baseDir = MakeTempDir();
            string path = Path.Combine(baseDir, "same.idx");

            MemoryMappedFile m1 = MemoryMappedFileFactory.Get(path, 1024 * 1024);
            MemoryMappedFile m2 = MemoryMappedFileFactory.Get(path, 1024 * 1024);

            Assert.AreSame(m1, m2, "Factory did not reuse the live mapping");
        }

        [TestMethod]
        public void GetRecreatesMappingAfterConsumerDisposesIt()
        {
            string baseDir = MakeTempDir();
            string path = Path.Combine(baseDir, "selfheal.idx");
            const long capacity = 8L * 1024 * 1024;

            MemoryMappedFile first = MemoryMappedFileFactory.Get(path, capacity);
            using (MemoryMappedViewAccessor view = first.CreateViewAccessor(0, 1024))
            {
                view.Write(0, 9.5f);
            }
            first.Dispose();
            Assert.IsTrue(first.SafeMemoryMappedFileHandle.IsClosed, "Precondition: shared mapping was disposed by the consumer");

            MemoryMappedFile second = MemoryMappedFileFactory.Get(path, capacity);
            Assert.AreNotSame(first, second, "Factory returned the disposed instance from the cache");

            using (MemoryMappedViewAccessor view = second.CreateViewAccessor(0, 1024))
            {
                view.Write(0, 4.5f);
                Assert.AreEqual(4.5f, view.ReadSingle(0), "Re-created mapping is not usable");
            }
        }

        [TestMethod]
        public void GetGrowsCapacityWhenLargerCapacityRequested()
        {
            string baseDir = MakeTempDir();
            string path = Path.Combine(baseDir, "grow.idx");

            MemoryMappedFile small = MemoryMappedFileFactory.Get(path, 1024 * 1024);
            MemoryMappedFile big = MemoryMappedFileFactory.Get(path, 8L * 1024 * 1024);

            Assert.AreNotSame(small, big, "Factory ignored the larger capacity request");

            using (MemoryMappedViewAccessor view = big.CreateViewAccessor(0, 8L * 1024 * 1024))
            {
                view.Write(8L * 1024 * 1024 - 4, 1.25f);
                Assert.AreEqual(1.25f, view.ReadSingle(8L * 1024 * 1024 - 4), "Grown mapping does not cover the requested range");
            }

            Assert.IsTrue(new FileInfo(path).Length >= 8L * 1024 * 1024, "Backing file was not extended");
        }

        [TestMethod]
        public void GetReusesMappingWhenSmallerCapacityRequested()
        {
            string baseDir = MakeTempDir();
            string path = Path.Combine(baseDir, "reuse.idx");

            MemoryMappedFile big = MemoryMappedFileFactory.Get(path, 8L * 1024 * 1024);
            MemoryMappedFile smaller = MemoryMappedFileFactory.Get(path, 1024 * 1024);

            Assert.AreSame(big, smaller, "Factory created a new mapping for a smaller capacity request");
        }

        [TestMethod]
        public void GetRejectsInvalidArguments()
        {
            string baseDir = MakeTempDir();
            string path = Path.Combine(baseDir, "invalid.idx");

            Assert.ThrowsException<ArgumentException>(() => MemoryMappedFileFactory.Get(null, 1024));
            Assert.ThrowsException<ArgumentException>(() => MemoryMappedFileFactory.Get(string.Empty, 1024));
            Assert.ThrowsException<ArgumentException>(() => MemoryMappedFileFactory.Get("   ", 1024));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryMappedFileFactory.Get(path, 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryMappedFileFactory.Get(path, -1024));
        }

        [TestMethod]
        public void GetAcceptsBareFileNameWithoutDirectory()
        {
            string name = "MmfBareName" + Guid.NewGuid().ToString("N") + ".idx";

            MemoryMappedFile map = MemoryMappedFileFactory.Get(name, 1024 * 1024);
            Assert.IsNotNull(map, "Get failed for a file name without a directory component");

            using (MemoryMappedViewAccessor view = map.CreateViewAccessor(0, 512))
            {
                view.Write(0, 1.5f);
                Assert.AreEqual(1.5f, view.ReadSingle(0), "Mapping for a bare file name is not usable");
            }

            MemoryMappedFileFactory.DisposeAll();
            File.Delete(name);
        }

        [TestMethod]
        public void FlushPersistsDataAcrossDisposeAll()
        {
            string baseDir = MakeTempDir();
            string path = Path.Combine(baseDir, "persist.idx");
            const long capacity = 8L * 1024 * 1024;

            MemoryMappedFile map = MemoryMappedFileFactory.Get(path, capacity);
            using (MemoryMappedViewAccessor view = map.CreateViewAccessor(0, 1024))
            {
                view.Write(0, 3.14f);
            }
            MemoryMappedFileFactory.Flush(path);
            MemoryMappedFileFactory.DisposeAll();

            MemoryMappedFile reopened = MemoryMappedFileFactory.Get(path, capacity);
            using (MemoryMappedViewAccessor view = reopened.CreateViewAccessor(0, 1024))
            {
                Assert.AreEqual(3.14f, view.ReadSingle(0), "Written data did not survive the teardown");
            }
        }

        [TestMethod]
        public void FlushUnknownPathDoesNotThrow()
        {
            string baseDir = MakeTempDir();
            MemoryMappedFileFactory.Flush(Path.Combine(baseDir, "never-created.idx"));
        }

        [TestMethod]
        public void FactorySelfHealsAfterMapperDisposesSharedResources()
        {
            string baseDir = MakeTempDir();
            string basePath = Path.Combine(baseDir, "map");

            FileIndexMapper<int> m1 = new FileIndexMapper<int>(basePath);
            uint idx1 = m1[42];
            m1.Dispose();

            FileIndexMapper<int> m2 = new FileIndexMapper<int>(basePath);
            uint idx2 = m2[42];
            m2.Dispose();

            Assert.AreEqual(idx1, idx2, "Index changed after the shared mapping and stream were disposed and re-created");
        }

        [TestMethod]
        public void MapperDataSurvivesFactoryTeardown()
        {
            string baseDir = MakeTempDir();
            string basePath = Path.Combine(baseDir, "map");

            // Start the index sequence at 1 so the key under test lands at slot >= 1.
            // That forces gap-fill tombstones into the .idx file, which is the
            // fragile case for slot discovery on restart.
            FileIndexMapper<int> m1 = new FileIndexMapper<int>(basePath, repo: new IndexRepo(1));
            uint idx1 = m1[42];
            Assert.IsTrue(idx1 > 0, "Precondition: the key under test must not be at slot zero");
            m1.Dispose();

            MemoryMappedFileFactory.DisposeAll();
            DataFileStreamFactory.DisposeAll();

            FileIndexMapper<int> m2 = new FileIndexMapper<int>(basePath);
            uint idx2 = m2[42];
            m2.Dispose();

            Assert.AreEqual(idx1, idx2, "Mapped index did not survive a simulated restart");
        }
    }
}
