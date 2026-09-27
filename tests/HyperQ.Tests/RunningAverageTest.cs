using System;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HyperQ.Test
{
    [TestClass]
    public class RunningAverageTest
    {
        [TestMethod]
        public void UpdateThrowsWhenEmpty()
        {
            RunningAverage avg = new RunningAverage();
            Assert.ThrowsException<InvalidOperationException>(() => avg.Update(1m));
        }

        [TestMethod]
        public void AddComputesAverageAndSum()
        {
            RunningAverage avg = new RunningAverage();
            avg.Add(1m);
            avg.Add(2m);
            avg.Add(3m);
            Assert.AreEqual(3, avg.Count);
            Assert.AreEqual(6m, avg.Sum);
            Assert.AreEqual(2m, avg.Value);
            Assert.AreEqual(1m, avg.First);
            Assert.AreEqual(3m, avg.Last);
        }

        [TestMethod]
        public void UpdateAdjustsAverage()
        {
            RunningAverage avg = new RunningAverage();
            avg.Add(1m);
            avg.Add(2m);
            avg.Add(3m);
            avg.Update(2m); // change last value from 3 to 5
            Assert.AreEqual(8m, avg.Sum);
            Assert.AreEqual(2.666666666666666666666666667m, avg.Value, (decimal)1e-10);
        }

        [TestMethod]
        public void AddDifferentTypes()
        {
            RunningAverage avg = new RunningAverage();
            avg.Add(1.0); // double
            avg.Add(2);   // int
            avg.Add(3f);  // float
            avg.Add(4L);  // long
            Assert.AreEqual(4, avg.Count);
            Assert.AreEqual(10m, avg.Sum);
            Assert.AreEqual(2.5m, avg.Value);
        }
    }
}
