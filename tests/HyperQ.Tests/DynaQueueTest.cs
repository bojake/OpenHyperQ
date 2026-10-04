using HyperQ.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace HyperQ.Test
{
    /// <summary>
    /// The prioritized sweeping queue keeps each (s,a) entry at most once, with its highest priority. It used to queue
    /// every insertion, duplicates included, and grew without bound in long runs.
    /// </summary>
    [TestClass]
    public class DynaQueueTest
    {
        private static DynaEntry<decimal> Entry(decimal s, int a)
        {
            return new DynaEntry<decimal>(s, new QAction(a, 0.0, (uint)a));
        }

        [TestMethod]
        public void AQueuedEntryWaitsOnceWithItsHighestPriority()
        {
            var ds = new DynaState<decimal, ScalarReward>(10, 0);
            ds.InsertPriority(Entry(1M, 0), 1.0);
            ds.InsertPriority(Entry(1M, 0), 2.0);
            ds.InsertPriority(Entry(1M, 0), 0.5);
            Assert.AreEqual(1, ds.QueuedCount);
            ds.InsertPriority(Entry(2M, 0), 1.5);
            Assert.AreEqual(Entry(1M, 0), ds.PopPriority(), "the raised priority, 2.0, comes first");
            Assert.AreEqual(Entry(2M, 0), ds.PopPriority());
            Assert.IsNull(ds.PopPriority(), "the entry's stale copies are skipped");
            Assert.IsFalse(ds.HasPriority);
        }

        [TestMethod]
        public void EntriesComeOutHighestFirst()
        {
            var ds = new DynaState<decimal, ScalarReward>(10, 0);
            ds.InsertPriority(Entry(1M, 0), 1.0);
            ds.InsertPriority(Entry(2M, 0), 3.0);
            ds.InsertPriority(Entry(3M, 1), 2.0);
            Assert.AreEqual(Entry(2M, 0), ds.PopPriority());
            Assert.AreEqual(Entry(3M, 1), ds.PopPriority());
            Assert.AreEqual(Entry(1M, 0), ds.PopPriority());
            Assert.IsNull(ds.PopPriority());
        }

        [TestMethod]
        public void APoppedEntryCanBeQueuedAgain()
        {
            var ds = new DynaState<decimal, ScalarReward>(10, 0);
            ds.InsertPriority(Entry(1M, 0), 1.0);
            Assert.AreEqual(Entry(1M, 0), ds.PopPriority());
            ds.InsertPriority(Entry(1M, 0), 0.5);
            Assert.IsTrue(ds.HasPriority);
            Assert.AreEqual(Entry(1M, 0), ds.PopPriority());
        }

        [TestMethod]
        public void TheHeapStaysBounded()
        {
            var ds = new DynaState<decimal, ScalarReward>(10, 0);
            Random r = new Random(1);
            for (int i = 0; i < 100_000; i++)
            {
                // Rising priorities keep leaving stale copies behind.
                ds.InsertPriority(Entry(r.Next(10), r.Next(2)), i + r.NextDouble());
                if (i % 7 == 0)
                {
                    ds.PopPriority();
                }
                Assert.IsTrue(ds.QueuedCount <= 20);
                Assert.IsTrue(ds.PriorityHeapSize <= 2 * ds.QueuedCount + 65, "heap {0} for {1} queued", ds.PriorityHeapSize, ds.QueuedCount);
            }
        }
    }
}
