using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using HyperQ.Util;
using System.Linq;
using System.Threading.Tasks;

namespace HyperQ.Test
{
    [TestClass]
    public class FileIndexMapperTest
    {
        private IIndexMapper<int> MakeIntTestMapper(int cacheSize=5,int flushAfter=2)
        {
            string baseDir = TestTempFiles.NewDirectory("IntFileIndexMapperTest");
            Directory.CreateDirectory(baseDir);
            string basePath = Path.Combine(baseDir, "map");
            var mapper = new FileIndexMapper<int>(basePath, cacheSize, flushAfter);
            mapper.Repo = new IndexRepo(0);
            return mapper;
        }
        private IIndexMapper<decimal> MakeDecimalTestMapper(int cacheSize = 5, int flushAfter = 2)
        {
            string baseDir = TestTempFiles.NewDirectory("DecFileIndexMapperTest");
            Directory.CreateDirectory(baseDir);
            string basePath = Path.Combine(baseDir, "map");
            var mapper = new FileIndexMapper<decimal>(basePath, cacheSize, flushAfter);
            mapper.Repo = new IndexRepo(0);
            return mapper;
        }

        [TestMethod]
        public void TestFileBackedMapping()
        {
            var mapper = MakeDecimalTestMapper(5, 2);
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            uint[] indices = new uint[values.Length];
            for (int iter = 0; iter < 20; iter++)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    uint idx = mapper.ToIndex(values[i]);
                    indices[i] = idx;
                    Assert.AreEqual(idx, mapper.ToIndex(values[i]));
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (idx == indices[j])
                        {
                            Assert.Fail("Index replay detected.");
                        }
                    }
                }
                ((FileIndexMapper<decimal>)mapper).FlushAsync().Wait();
                for (int i = 0; i < values.Length; i++)
                {
                    Assert.AreEqual(indices[i], mapper.ToIndex(values[i]));
                    Assert.AreEqual(values[i], mapper.FromIndex(indices[i]));
                }
            }
        }

        [TestMethod]
        public void TestFileBackedLookupAfterEviction()
        {
            var mapper = (FileIndexMapper<int>)MakeIntTestMapper(cacheSize: 1, flushAfter: 1);
            uint i10 = mapper.ToIndex(10);
            uint i20 = mapper.ToIndex(20);
            uint i30 = mapper.ToIndex(30);
            mapper.FlushAsync().Wait();

            // With a tiny cache, this forces disk lookups for earlier entries.
            Assert.AreEqual(i20, mapper.ToIndex(20), "Expected stable mapping for evicted key.");
            Assert.AreEqual(20, mapper.FromIndex(i20), "Expected reverse lookup to return the original key.");
            Assert.AreEqual(i10, mapper.ToIndex(10), "Expected first mapped key to remain stable.");
            Assert.AreEqual(i30, mapper.ToIndex(30), "Expected latest mapped key to remain stable.");
        }

        [TestMethod]
        public void TestFileBackedRemoveUpdatesKnownAndCount()
        {
            var mapper = (FileIndexMapper<int>)MakeIntTestMapper(cacheSize: 3, flushAfter: 1);
            uint i1 = mapper.ToIndex(1);
            uint i2 = mapper.ToIndex(2);
            uint i3 = mapper.ToIndex(3);
            mapper.FlushAsync().Wait();

            Assert.AreEqual(3u, mapper.MappingCount, "Expected three known mappings.");
            Assert.IsTrue(mapper.IsKnownIndex(i2), "Expected index to be known before removal.");
            Assert.IsTrue(mapper.RemoveKey(2), "Expected removal of existing key to succeed.");
            Assert.IsFalse(mapper.IsKnownIndex(i2), "Expected removed index to be unknown.");
            Assert.AreEqual(2u, mapper.MappingCount, "Expected mapping count to exclude removed entries.");

            uint i4 = mapper.ToIndex(4);
            Assert.AreEqual(i2, i4, "Expected released index to be reused.");
            Assert.IsTrue(mapper.IsKnownIndex(i1), "Expected untouched index to remain known.");
            Assert.IsTrue(mapper.IsKnownIndex(i3), "Expected untouched index to remain known.");
        }

        [TestMethod]
        public void TestFileBackedClonePreservesMappingsAndTombstones()
        {
            var mapper = (FileIndexMapper<int>)MakeIntTestMapper(cacheSize: 3, flushAfter: 2);
            uint i10 = mapper.ToIndex(10);
            uint i20 = mapper.ToIndex(20);
            uint i30 = mapper.ToIndex(30);
            mapper.FlushAsync().Wait();
            Assert.IsTrue(mapper.RemoveKey(20), "Expected removal from source mapper to succeed.");

            var clone = (FileIndexMapper<int>)mapper.Clone();

            Assert.AreEqual(i10, clone.ToIndex(10), "Expected cloned mapper to preserve index for key 10.");
            Assert.AreEqual(i30, clone.ToIndex(30), "Expected cloned mapper to preserve index for key 30.");
            Assert.AreEqual(10, clone.FromIndex(i10), "Expected reverse lookup for key 10 index to be preserved.");
            Assert.AreEqual(30, clone.FromIndex(i30), "Expected reverse lookup for key 30 index to be preserved.");
            Assert.IsFalse(clone.Known(20), "Expected removed key to stay removed in clone.");
            Assert.IsFalse(clone.IsKnownIndex(i20), "Expected removed index tombstone to be preserved in clone.");
        }

        [TestMethod]
        public void TestFileBackedRandomKeySkipsRemovedIndices()
        {
            var mapper = (FileIndexMapper<int>)MakeIntTestMapper(cacheSize: 8, flushAfter: 3);
            var indexByKey = new Dictionary<int, uint>();
            for (int k = 0; k < 40; k++)
            {
                indexByKey[k] = mapper.ToIndex(k);
            }
            mapper.FlushAsync().Wait();

            var expected = new HashSet<uint>();
            for (int k = 0; k < 40; k++)
            {
                if (k % 2 == 0)
                {
                    Assert.IsTrue(mapper.RemoveKey(k), $"Expected remove to succeed for key {k}.");
                }
                else
                {
                    expected.Add(indexByKey[k]);
                }
            }

            QRandom ran = new QRandom(12345);
            for (int i = 0; i < 200; i++)
            {
                uint idx = mapper.RandomKey(ran);
                Assert.IsTrue(expected.Contains(idx), $"Random key returned removed or unknown index {idx}.");
                Assert.IsTrue(mapper.IsKnownIndex(idx), $"Random key returned index {idx} that is not known.");
                int key = mapper.FromIndex(idx);
                Assert.IsTrue(key % 2 == 1, $"Random key returned index for removed key {key}.");
            }
        }

        [TestMethod]
        public void TestConcurrentSameKeyLookupAllocatesOneIndex()
        {
            var mapper = (FileIndexMapper<int>)MakeIntTestMapper(cacheSize: 4, flushAfter: 4);

            // Hammer one fresh key from many threads. Every caller must observe
            // the same slot: the write-lock re-check consults the cache and the
            // pending queue, which FindKeyInFile alone cannot see.
            var indices = new System.Collections.Concurrent.ConcurrentBag<uint>();
            Parallel.For(0, 100, i => indices.Add(mapper.ToIndex(777)));

            Assert.AreEqual(1, indices.Distinct().Count(), "Concurrent lookups allocated more than one slot for the same key");
            uint idx = mapper.ToIndex(777);
            Assert.AreEqual(777, mapper.FromIndex(idx), "Reverse lookup broke under concurrency");
        }

        [TestMethod]
        public void TestFileBackedIndexGrowsPastItsStart()
        {
            // A new index file holds 1,024 slots and doubles when a key lands past its end. A mapper's indexes come
            // from a repository it may share, so they can jump far ahead. The file used to start at 8 MB and refuse
            // any slot past 1,048,575.
            string basePath = Path.Combine(TestTempFiles.NewDirectory("IntFileIndexMapperTest"), "map");
            string indexPath = basePath + ".idx";
            IndexRepo repo = new IndexRepo(0);
            uint[] indexes = { 0, 3000, 200000, 1100000 };
            using (var mapper = new FileIndexMapper<int>(basePath, cacheSize: 1, flushThreshold: 1, repo: repo))
            {
                for (int key = 0; key < indexes.Length; key++)
                {
                    repo.RestoreAt(indexes[key]);
                    Assert.AreEqual(indexes[key], mapper.ToIndex(key));
                    if (key == 0)
                    {
                        Assert.AreEqual(8 * 1024L, new FileInfo(indexPath).Length, "A new index file should start at 1,024 slots.");
                    }
                }
                Assert.IsTrue(new FileInfo(indexPath).Length >= (indexes[indexes.Length - 1] + 1L) * sizeof(long), "Expected the index file to grow.");
                for (int key = 0; key < indexes.Length; key++)
                {
                    Assert.AreEqual(indexes[key], mapper.ToIndex(key), $"Key {key} moved when the index file grew.");
                    Assert.AreEqual(key, mapper.FromIndex(indexes[key]));
                }
            }
            using (var reopened = new FileIndexMapper<int>(basePath, repo: new IndexRepo(0)))
            {
                Assert.AreEqual((uint)indexes.Length, reopened.MappingCount);
                for (int key = 0; key < indexes.Length; key++)
                {
                    Assert.AreEqual(indexes[key], reopened.ToIndex(key), $"Key {key} moved after reopening.");
                    Assert.AreEqual(key, reopened.FromIndex(indexes[key]));
                }
            }
        }

    }
}
