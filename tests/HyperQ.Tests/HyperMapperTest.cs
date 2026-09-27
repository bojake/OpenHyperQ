using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using HyperQ.Learners;
using System.Collections.Generic;
using HyperQ.Util;
using System.IO;
using System.Linq;

namespace HyperQ.Test
{
    [TestClass]
    public class HyperMapperTest
    {
        private static string StateKey(QState<decimal> state)
        {
            // Stable text key for fast uniqueness/consistency checks in tests.
            return string.Join("|", Enumerable.Range(0, state.Count).Select(i => state[i].ToString()));
        }

        private static int ReadTestSize(string envVar, int defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }
            if (int.TryParse(value, out int parsed) && parsed > 0)
            {
                return parsed;
            }
            return defaultValue;
        }

        [TestMethod]
        public void MBTestMappingCountAndMappingsEnumerateStates()
        {
            MemoryBackedHyperMapper<decimal> f = new MemoryBackedHyperMapper<decimal>();
            QState<decimal>[] states =
            {
                new QState<decimal>(new decimal[] { 1M, 1M }),
                new QState<decimal>(new decimal[] { 1M, 2M }),
                new QState<decimal>(new decimal[] { 2M, 1M }),
                new QState<decimal>(new decimal[] { 2M, 2M }),
                new QState<decimal>(new decimal[] { 3M, 7M }),
            };
            uint[] indices = new uint[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                indices[i] = f[states[i].Enumerator];
            }
            // Five two-element states were mapped: the count is the number of mapped keys, not the
            // product of the level sizes (which would be 2 * 2 * 1 = 4 here).
            Assert.AreEqual(5u, f.MappingCount);
            Assert.AreEqual(5u, f.MappingCountAtDepth(2));
            Assert.AreEqual(0u, f.MappingCountAtDepth(1));
            List<KeyValuePair<QState<decimal>, uint>> mappings = f.Mappings();
            Assert.AreEqual(5, mappings.Count);
            for (int i = 0; i < states.Length; i++)
            {
                List<KeyValuePair<QState<decimal>, uint>> hit = mappings.Where(kv => kv.Value == indices[i]).ToList();
                Assert.AreEqual(1, hit.Count, "Every index appears exactly once.");
                Assert.IsTrue(hit[0].Key.IsSame(states[i], new QStateDecimalComparer()), "The path must reproduce the mapped state.");
            }
            // A one-element key at the top level counts at depth 1 only.
            uint top = f[9M];
            Assert.AreEqual(6u, f.MappingCount);
            Assert.AreEqual(1u, f.MappingCountAtDepth(1));
            Assert.AreEqual(5u, f.MappingCountAtDepth(2));
            Assert.IsTrue(f.Mappings().Any(kv => kv.Value == top && kv.Key.Count == 1 && kv.Key[0] == 9M));
        }

        [TestMethod]
        public void MBTestSingleLevelMapping()
        {
            MemoryBackedHyperMapper<decimal> f = new MemoryBackedHyperMapper<decimal>();
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            uint[] indices = new uint[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                uint idx = f[values[i]];
                indices[i] = idx;
                Assert.AreEqual(idx, f[values[i]]);
            }
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(indices[i], f[values[i]]);
            }
        }

        [TestMethod]
        public void FBTestSingleLevelMapping()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "FBTestSingleLevelMapping" + Guid.NewGuid().ToString());
            FileBackedHyperMapper<decimal> f = new FileBackedHyperMapper<decimal>(memoryFileName:baseDir);
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            uint[] indices = new uint[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                uint idx = f[values[i]];
                indices[i] = idx;
                Assert.AreEqual(idx, f[values[i]]);
            }
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(indices[i], f[values[i]]);
            }
        }

        [TestMethod]
        public void MBTestTwoLevelMapping()
        {
            MemoryBackedHyperMapper<decimal> f = new MemoryBackedHyperMapper<decimal>();
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            List<QState<decimal>> testv = new List<QState<decimal>>();
            for (int i = 0; i < 30; i++)
            {
                testv.Add(new QState<decimal>(new decimal[] { values[i % values.Length], values[(i + 1) % values.Length] }));
            }
            List<Tuple<QState<decimal>, uint>> indices = new List<Tuple<QState<decimal>, uint>>();
            for (int i = 0; i < testv.Count; i++)
            {
                uint idx = f[testv[i].Enumerator];
                indices.Add(new Tuple<QState<decimal>, uint>(testv[i], idx));
                // Test for coherence
                Assert.AreEqual(idx, f[testv[i].Enumerator]);
                // Test for uniqueness
                for (int j = i - 1; j >= 0; j--)
                {
                    if (idx == indices[j].Item2 && !testv[i].IsSame(indices[j].Item1, new QStateDecimalComparer()))
                    {
                        Assert.Fail($"Index reuse detected, idx={idx} at j={j}.");
                    }
                }
            }
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(indices[i].Item2, f[testv[i].Enumerator]);
            }
            f.Dump();
        }

        [TestMethod]
        public void FBTestTwoLevelMapping()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "FBTestTwoLevelMapping" + Guid.NewGuid().ToString());
            FileBackedHyperMapper<decimal> f = new FileBackedHyperMapper<decimal>(memoryFileName:baseDir);
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            List<QState<decimal>> testv = new List<QState<decimal>>();
            for (int i = 0; i < 30; i++)
            {
                testv.Add(new QState<decimal>(new decimal[] { values[i % values.Length], values[(i + 1) % values.Length] }));
            }
            List<Tuple<QState<decimal>, uint>> indices = new List<Tuple<QState<decimal>, uint>>();
            for (int i = 0; i < testv.Count; i++)
            {
                uint idx = f[testv[i].Enumerator];
                indices.Add(new Tuple<QState<decimal>, uint>(testv[i], idx));
                // Test for coherence
                Assert.AreEqual(idx, f[testv[i].Enumerator]);
                // Test for uniqueness
                for (int j = i - 1; j >= 0; j--)
                {
                    if (idx == indices[j].Item2 && !testv[i].IsSame(indices[j].Item1, new QStateDecimalComparer()))
                    {
                        Assert.Fail($"Index reuse detected, idx={idx} at j={j}.");
                    }
                }
            }
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(indices[i].Item2, f[testv[i].Enumerator]);
            }
            f.Dump();
        }
        [TestMethod]
        public void MBTestFiveLevelMapping()
        {
            MemoryBackedHyperMapper<decimal> f = new MemoryBackedHyperMapper<decimal>();
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            List<QState<decimal>> testv = new List<QState<decimal>>();
            for (int i = 0; i < 30; i++)
            {
                int i1 = i % values.Length;
                int i2 = (i+1) % values.Length;
                int i3 = (i + 2) % values.Length;
                int i4 = (i + 3) % values.Length;
                int i5 = (i + 4) % values.Length;
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3], values[i4], values[i5] }));
            }
            List<Tuple<QState<decimal>, uint>> indices = new List<Tuple<QState<decimal>, uint>>();
            for (int i = 0; i < testv.Count; i++)
            {
                uint idx = f[testv[i].Enumerator];
                indices.Add(new Tuple<QState<decimal>, uint>(testv[i], idx));
                Assert.AreEqual(idx, f[testv[i].Enumerator]);
                // Test for uniqueness
                for (int j = i - 1; j >= 0; j--)
                {
                    if (idx == indices[j].Item2 && !testv[i].IsSame(indices[j].Item1, new QStateDecimalComparer()))
                    {
                        Assert.Fail($"Index reuse detected, idx={idx} at j={j}.");
                    }
                }
            }
            for (int i = 0; i < indices.Count; i++)
            {
                Assert.AreEqual(indices[i].Item2, f[testv[i].Enumerator]);
            }
            f.Dump();
        }
        [TestMethod]
        public void FBTestFiveLevelMapping()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "FBTestFiveLevelMapping" + Guid.NewGuid().ToString());
            FileBackedHyperMapper<decimal> f = new FileBackedHyperMapper<decimal>(memoryFileName: baseDir);
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            List<QState<decimal>> testv = new List<QState<decimal>>();
            for (int i = 0; i < 30; i++)
            {
                int i1 = i % values.Length;
                int i2 = (i + 1) % values.Length;
                int i3 = (i + 2) % values.Length;
                int i4 = (i + 3) % values.Length;
                int i5 = (i + 4) % values.Length;
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3], values[i4], values[i5] }));
            }
            List<Tuple<QState<decimal>, uint>> indices = new List<Tuple<QState<decimal>, uint>>();
            for (int i = 0; i < testv.Count; i++)
            {
                uint idx = f[testv[i].Enumerator];
                indices.Add(new Tuple<QState<decimal>, uint>(testv[i], idx));
                Assert.AreEqual(idx, f[testv[i].Enumerator]);
                // Test for uniqueness
                for (int j = i - 1; j >= 0; j--)
                {
                    if (idx == indices[j].Item2 && !testv[i].IsSame(indices[j].Item1, new QStateDecimalComparer()))
                    {
                        Assert.Fail($"Index reuse detected, idx={idx} at j={j}.");
                    }
                }
            }
            for (int i = 0; i < indices.Count; i++)
            {
                Assert.AreEqual(indices[i].Item2, f[testv[i].Enumerator]);
            }
            f.Dump();
        }
        [TestMethod]
        public void FBTestVariableLevelMapping()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "FBTestVariableLevelMapping" + Guid.NewGuid().ToString());
            FileBackedHyperMapper<decimal> f = new FileBackedHyperMapper<decimal>(memoryFileName: baseDir);
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            List<QState<decimal>> testv = new List<QState<decimal>>();
            for (int i = 0; i < 30; i++)
            {
                int i1 = i % values.Length;
                int i2 = (i + 1) % values.Length;
                int i3 = (i + 2) % values.Length;
                int i4 = (i + 3) % values.Length;
                int i5 = (i + 4) % values.Length;
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3], values[i4], values[i5] }));
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3] }));
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3], values[i4] }));
                testv.Add(new QState<decimal>(new decimal[] { values[i1] }));
            }
            List<Tuple<QState<decimal>, uint>> indices = new List<Tuple<QState<decimal>, uint>>();
            for (int i = 0; i < testv.Count; i++)
            {
                uint idx = f[testv[i].Enumerator];
                indices.Add(new Tuple<QState<decimal>, uint>(testv[i], idx));
                Assert.AreEqual(idx, f[testv[i].Enumerator]);
                // Test for uniqueness
                for (int j = i - 1; j >= 0; j--)
                {
                    if (idx == indices[j].Item2 && !testv[i].IsSame(indices[j].Item1, new QStateDecimalComparer()))
                    {
                        Assert.Fail($"Index reuse detected, idx={idx} at j={j}.");
                    }
                }
            }
            for (int i = 0; i < indices.Count; i++)
            {
                Assert.AreEqual(indices[i].Item2, f[testv[i].Enumerator]);
            }
            f.Dump();
        }

        [TestMethod]
        public void MBTestVariableLevelMapping()
        {
            MemoryBackedHyperMapper<decimal> f = new MemoryBackedHyperMapper<decimal>();
            decimal[] values = new decimal[] { 1M, 2M, 5M, 600M, 23423M, 1000M, 0M, -59M };
            List<QState<decimal>> testv = new List<QState<decimal>>();
            for (int i = 0; i < 30; i++)
            {
                int i1 = i % values.Length;
                int i2 = (i + 1) % values.Length;
                int i3 = (i + 2) % values.Length;
                int i4 = (i + 3) % values.Length;
                int i5 = (i + 4) % values.Length;
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3], values[i4], values[i5] }));
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3] }));
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3], values[i4] }));
                testv.Add(new QState<decimal>(new decimal[] { values[i1] }));
            }
            List<Tuple<QState<decimal>, uint>> indices = new List<Tuple<QState<decimal>, uint>>();
            for (int i = 0; i < testv.Count; i++)
            {
                uint idx = f[testv[i].Enumerator];
                indices.Add(new Tuple<QState<decimal>, uint>(testv[i], idx));
                Assert.AreEqual(idx, f[testv[i].Enumerator]);
                // Test for uniqueness
                for (int j = i - 1; j >= 0; j--)
                {
                    if (idx == indices[j].Item2 && !testv[i].IsSame(indices[j].Item1, new QStateDecimalComparer()))
                    {
                        Assert.Fail($"Index reuse detected, idx={idx} at j={j}.");
                    }
                }
            }
            for (int i = 0; i < indices.Count; i++)
            {
                Assert.AreEqual(indices[i].Item2, f[testv[i].Enumerator]);
            }
            f.Dump();
        }
        [TestMethod]
        public void MBTestExhaustiveFiveLevelMapping()
        {
            MemoryBackedHyperMapper<decimal> f = new MemoryBackedHyperMapper<decimal>();
            List<decimal> d = new List<decimal>();
            QRandom.Instance.Seed(0);
            for (int i = 0; i < 1000000; i++)
            {
                d.Add((decimal)QRandom.Instance.Ran.Next(30000,30000000));
            }
            int n_tests = ReadTestSize("HYPERQ_MB_EXHAUSTIVE_N", 20000);
            decimal[] values = d.ToArray();
            List<QState<decimal>> testv = new List<QState<decimal>>();
            for (int i = 0; i < n_tests; i++)
            {
                int i1 = i % values.Length;
                int i2 = (i + 1) % values.Length;
                int i3 = (i + 2) % values.Length;
                int i4 = (i + 3) % values.Length;
                int i5 = (i + 4) % values.Length;
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3], values[i4], values[i5] }));
            }
            Assert.AreEqual(n_tests, testv.Count, "Unexpected count in the testv list.");
            List<Tuple<QState<decimal>, uint>> indices = new List<Tuple<QState<decimal>, uint>>();
            Dictionary<string, uint> expectedByState = new Dictionary<string, uint>(n_tests);
            Dictionary<uint, string> firstStateByIndex = new Dictionary<uint, string>(n_tests);
            for (int i = 0; i < testv.Count; i++)
            {
                uint idx = f[testv[i].Enumerator];
                indices.Add(new Tuple<QState<decimal>, uint>(testv[i], idx));
                Assert.AreEqual(idx, f[testv[i].Enumerator]);
                string key = StateKey(testv[i]);
                if (expectedByState.TryGetValue(key, out uint expectedIdx))
                {
                    Assert.AreEqual(expectedIdx, idx, $"State remapped unexpectedly at i={i}");
                    continue;
                }
                expectedByState[key] = idx;
                if (firstStateByIndex.TryGetValue(idx, out string otherKey))
                {
                    Assert.AreEqual(otherKey, key, $"Index reuse detected, idx={idx} at i={i}");
                }
                else
                {
                    firstStateByIndex[idx] = key;
                }
            }
            Assert.AreEqual(testv.Count, indices.Count, "Expected indices and testv lists to be the same size.");
            for (int i = 0; i < indices.Count; i++)
            {
                Assert.AreEqual(indices[i].Item2, f[testv[i].Enumerator]);
            }
            f.Dump();
        }
        [TestMethod]
        public void FBTestExhaustiveFiveLevelMapping()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "FBExhaustiveTestFiveLevelMapping" + Guid.NewGuid().ToString());
            FileBackedHyperMapper<decimal> f = new FileBackedHyperMapper<decimal>(memoryFileName: baseDir);
            List<decimal> d = new List<decimal>();
            QRandom.Instance.Seed(0);
            for (int i = 0; i < 1000000; i++)
            {
                d.Add((decimal)QRandom.Instance.Ran.Next(30000, 30000000));
            }
            int n_tests = ReadTestSize("HYPERQ_FB_EXHAUSTIVE_N", 6000);
            decimal[] values = d.ToArray();
            List<QState<decimal>> testv = new List<QState<decimal>>();
            for (int i = 0; i < n_tests; i++)
            {
                int i1 = i % values.Length;
                int i2 = (i + 1) % values.Length;
                int i3 = (i + 2) % values.Length;
                int i4 = (i + 3) % values.Length;
                int i5 = (i + 4) % values.Length;
                testv.Add(new QState<decimal>(new decimal[] { values[i1], values[i2], values[i3], values[i4], values[i5] }));
            }
            Assert.AreEqual(n_tests, testv.Count, "Unexpected count in the testv list.");
            List<Tuple<QState<decimal>, uint>> indices = new List<Tuple<QState<decimal>, uint>>();
            Dictionary<string, uint> expectedByState = new Dictionary<string, uint>(n_tests);
            Dictionary<uint, string> firstStateByIndex = new Dictionary<uint, string>(n_tests);
            for (int i = 0; i < testv.Count; i++)
            {
                uint idx = f[testv[i].Enumerator];
                indices.Add(new Tuple<QState<decimal>, uint>(testv[i], idx));
                Assert.AreEqual(idx, f[testv[i].Enumerator]);
                string key = StateKey(testv[i]);
                if (expectedByState.TryGetValue(key, out uint expectedIdx))
                {
                    Assert.AreEqual(expectedIdx, idx, $"State remapped unexpectedly at i={i}");
                    continue;
                }
                expectedByState[key] = idx;
                if (firstStateByIndex.TryGetValue(idx, out string otherKey))
                {
                    Assert.AreEqual(otherKey, key, $"Index reuse detected, idx={idx} at i={i}");
                }
                else
                {
                    firstStateByIndex[idx] = key;
                }
                if(i % 100 == 0)
                {
                    Console.WriteLine($"Progress: {i} / {testv.Count}");
                }
            }
            Assert.AreEqual(testv.Count, indices.Count, "Expected indices and testv lists to be the same size.");
            for (int i = 0; i < indices.Count; i++)
            {
                Assert.AreEqual(indices[i].Item2, f[testv[i].Enumerator]);
            }
            f.Dump();
        }

        [TestMethod]
        public void MBTestRemovePathPreservesSiblingBranch()
        {
            MemoryBackedHyperMapper<decimal> f = new MemoryBackedHyperMapper<decimal>();
            QState<decimal> a = new QState<decimal>(new decimal[] { 1M, 2M, 3M });
            QState<decimal> b = new QState<decimal>(new decimal[] { 1M, 2M, 4M });

            uint ia = f[a.Enumerator];
            uint ib = f[b.Enumerator];
            Assert.AreNotEqual(ia, ib, "Expected distinct leaf paths to map distinctly.");

            Assert.IsTrue(f.RemoveKey(a.Enumerator), "Expected leaf path removal to succeed.");
            Assert.IsFalse(f.Known(a.Enumerator), "Expected removed path to be unknown.");
            Assert.IsTrue(f.Known(b.Enumerator), "Expected sibling path to remain known.");
            Assert.AreEqual(ib, f[b.Enumerator], "Expected sibling path mapping to remain stable.");
        }

        [TestMethod]
        public void FBTestRemovePathPreservesSiblingBranch()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "FBRemovePathPreservesSiblingBranch" + Guid.NewGuid().ToString());
            FileBackedHyperMapper<decimal> f = new FileBackedHyperMapper<decimal>(memoryFileName: baseDir);
            QState<decimal> a = new QState<decimal>(new decimal[] { 1M, 2M, 3M });
            QState<decimal> b = new QState<decimal>(new decimal[] { 1M, 2M, 4M });

            uint ia = f[a.Enumerator];
            uint ib = f[b.Enumerator];
            Assert.AreNotEqual(ia, ib, "Expected distinct leaf paths to map distinctly.");

            Assert.IsTrue(f.RemoveKey(a.Enumerator), "Expected leaf path removal to succeed.");
            Assert.IsFalse(f.Known(a.Enumerator), "Expected removed path to be unknown.");
            Assert.IsTrue(f.Known(b.Enumerator), "Expected sibling path to remain known.");
            Assert.AreEqual(ib, f[b.Enumerator], "Expected sibling path mapping to remain stable.");
        }
    }
}
