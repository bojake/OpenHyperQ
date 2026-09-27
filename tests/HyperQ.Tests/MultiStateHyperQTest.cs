using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace HyperQ.Test
{
    [TestClass]
    public class MultiStateHyperQTest
    {
        private QState<decimal> newQState(params int[] v)
        {
            QState<decimal> q = new QState<decimal>();
            for (int i = 0; i < v.Length; i++)
            {
                q.Push((decimal)v[i]);
            }
            return (q);
        }
        private IHyperQ<decimal> MakeSingleQ()
        {
            IHyperQ<decimal> q1 = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            q1.SetValue(newQState(10,1), 1, 5.0);
            q1.SetValue(newQState(10,2), 1, 7.0);
            q1.SetValue(newQState(7,1), 2, 9.0);
            q1.SetValue(newQState(125,1), 5, 17.0);
            q1.SetValue(newQState(8,2), 5, 29.0);
            q1.SetValue(newQState(122,1), 5, 17.0);
            q1.SetValue(newQState(122,2), 4, 18.0);
            q1.SetValue(newQState(122,3), 3, 27.0);
            q1.SetValue(newQState(11,1), 5, 13.0);
            q1.SetValue(newQState(10,2), 5, 11.0);
            q1.SetValue(newQState(10,1), 4, 15.0);
            q1.SetValue(newQState(10,1), 3, 3.0);
            q1.SetValue(newQState(10,2), 2, 18.0);
            q1.SetValue(newQState(10,3), 1, 19.0);
            q1.SetValue(newQState(12,1), 5, 89.0);
            q1.SetValue(newQState(12,2), 5, 26.0);
            q1.SetValue(newQState(7,2), 5, 21.0);
            q1.SetValue(newQState(7,3), 4, 19.0);
            q1.SetValue(newQState(7,1), 3, 26.0);
            q1.SetValue(newQState(7,2), 2, 13.0);
            return (q1);
        }

        private IHyperQ<decimal> MakeDoubleQ()
        {
            IHyperQ<decimal> q1 = new DoubleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            q1.SetValue(newQState(10, 1), 1, 5.0);
            q1.SetValue(newQState(10, 2), 1, 7.0);
            q1.SetValue(newQState(7, 1), 2, 9.0);
            q1.SetValue(newQState(125, 1), 5, 17.0);
            q1.SetValue(newQState(8, 2), 5, 29.0);
            q1.SetValue(newQState(122, 1), 5, 17.0);
            q1.SetValue(newQState(122, 2), 4, 18.0);
            q1.SetValue(newQState(122, 3), 3, 27.0);
            q1.SetValue(newQState(11, 1), 5, 13.0);
            q1.SetValue(newQState(10, 2), 5, 11.0);
            q1.SetValue(newQState(10, 1), 4, 15.0);
            q1.SetValue(newQState(10, 1), 3, 3.0);
            q1.SetValue(newQState(10, 2), 2, 18.0);
            q1.SetValue(newQState(10, 3), 1, 19.0);
            q1.SetValue(newQState(12, 1), 5, 89.0);
            q1.SetValue(newQState(12, 2), 5, 26.0);
            q1.SetValue(newQState(7, 2), 5, 21.0);
            q1.SetValue(newQState(7, 3), 4, 19.0);
            q1.SetValue(newQState(7, 1), 3, 26.0);
            q1.SetValue(newQState(7, 2), 2, 13.0);
            return (q1);
        }

        [TestMethod]
        public void TestCtor()
        {
            IHyperQ<decimal> q = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            Assert.IsNotNull(q);
            Assert.AreEqual(10u, q.ActionSpace.MaximumNumberOfActions);
            Assert.AreEqual(0u, q.ActionSpace.NumberOfKnownActions);
            q = new DoubleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            Assert.IsNotNull(q);
            Assert.AreEqual(10u, q.ActionSpace.MaximumNumberOfActions);
            Assert.AreEqual(0u, q.ActionSpace.NumberOfKnownActions);
        }
        [TestMethod]
        public void TestSetGetValue()
        {
            IHyperQ<decimal> q = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            q.SetValue(newQState(10), 1, 5.0);
            q.SetValue(newQState(10,1), 1, 7.0);
            q.SetValue(newQState(10, 1,2), 1, 9.0);
            Assert.AreEqual(5.0, q.GetValue(newQState(10), 1));
            Assert.AreEqual(7.0, q.GetValue(newQState(10, 1), 1));
            Assert.AreEqual(9.0, q.GetValue(newQState(10, 1,2), 1));
            q = new DoubleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            q.SetValue(newQState(10), 1, 5.0);
            // Alt-Q has zero for this state, so 0.5 * (0 + 5) = 2.5, expected value
            Assert.AreEqual(2.5, q.GetValue(newQState(10), 1));
        }
        [TestMethod]
        public void TestMergeMax()
        {
            IHyperQ<decimal> q1 = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            q1.SetValue(newQState(10), 1, 5.0);
            IHyperQ<decimal> q2 = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            q2.SetValue(newQState(10), 1, 7.0);
            q2.SetValue(newQState(7), 2, 9.0);
            // Test Merge MAX
            try
            {
                q1.MergeInto(q2, EvalMethodType.Max);
                Assert.Fail("Merge is not implemented in the IHyperQ");
            }
            catch (Exception)
            {
                // Expected, not implemented
            }
        }
        [TestMethod]
        public void TestClone()
        {
            IHyperQ<decimal> q1 = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            q1.SetValue(newQState(1,1), 5, 1.0);
            q1.SetValue(newQState(2,1), 3, 2.0);
            q1.SetValue(newQState(3,2), 9, 3.0);
            q1.SetValue(newQState(1,2), 6, 4.0);
            IHyperQ<decimal> c = (IHyperQ<decimal>)q1.Clone();
            Assert.IsNotNull(c);
            Assert.AreEqual(1.0, c.GetValue(newQState(1,1), 5));
            Assert.AreEqual(2.0, c.GetValue(newQState(2,1), 3));
            Assert.AreEqual(3.0, c.GetValue(newQState(3,2), 9));
            Assert.AreEqual(4.0, c.GetValue(newQState(1,2), 6));
            // harder test
            c = MakeSingleQ();
            Assert.IsNotNull(c);
            Assert.AreEqual(9.0, c.GetValue(newQState(7,1), 2));
            Assert.AreEqual(89.0, c.GetValue(newQState(12,1), 5));
            Assert.AreEqual(26.0, c.GetValue(newQState(7,1), 3));
            Assert.AreEqual(3.0, c.GetValue(newQState(10,1), 3));
        }
        [TestMethod]
        public void TestArgMax()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            QAction r = c.ArgMax(newQState(7,1));
            Assert.IsNotNull(r);
            Assert.AreEqual(3, r.Item1);
            Assert.AreEqual(26.0, r.Item2);
            r = c.ArgMax(newQState(55324234)); // unknown
            c = MakeDoubleQ();
            r = c.ArgMax(newQState(7,1));
            Assert.IsNotNull(r);
            Assert.AreEqual(3, r.Item1);
            // Expected 13 = 0.5 * (0 + 26)
            Assert.AreEqual(13.0, r.Item2);
            r = c.ArgMax(newQState(555)); // unknown
        }
        [TestMethod]
        public void TestArgMin()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            QAction r = c.ArgMin(newQState(7,1));
            Assert.IsNotNull(r);
            Assert.AreEqual(2, r.Item1);
            Assert.AreEqual(9.0, r.Item2);
            r = c.ArgMax(newQState(55324234)); // unknown
            c = MakeDoubleQ();
            r = c.ArgMin(newQState(7,1));
            Assert.IsNotNull(r);
            Assert.AreEqual(2, r.Item1);
            // expected is 4.5 = 0.5 * (0 + 9)
            Assert.AreEqual(4.5, r.Item2);
        }

        [TestMethod]
        public void TestAddState()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            uint ix1 = c.AddState(newQState(1));
            uint ix2 = c.AddState(newQState(2));
            uint ix3 = c.AddState(newQState(3));
            Assert.AreNotEqual(ix2, ix1, "Indices should not map equally (1 v 2).");
            Assert.AreNotEqual(ix3, ix1, "Indices should not map equally (1 v 3).");
            Assert.AreNotEqual(ix2, ix3, "Indices should not map equally (2 v 3).");
        }

        [TestMethod]
        public void TestRemoveState()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            uint ix1 = c.AddState(newQState(1));
            uint ix2 = c.AddState(newQState(2));
            uint ix3 = c.AddState(newQState(3));
            bool b = c.RemoveState(newQState(2));
            Assert.IsTrue(b, "Failed to remove a state.");
            uint ix4 = c.AddState(newQState(4));
            Assert.AreEqual(ix4, ix2, "Removed and added state index should be the same (2 v 4)");
        }

        [TestMethod]
        public void TestDump()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            c.Dump();
        }

        [TestMethod]
        public void TestArrayAccess()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            Assert.IsNotNull(c);
            Assert.AreEqual(9.0, c[newQState(7,1), 2]);
            Assert.AreEqual(89.0, c[newQState(12, 1), 5]);
            Assert.AreEqual(26.0, c[newQState(7, 1), 3]);
            Assert.AreEqual(3.0, c[newQState(10,1), 3]);
        }

        [TestMethod]
        public void DoubleHyperQMappingTest()
        {
            IHyperQ<decimal> q = MakeDoubleQ();
        }
        [TestMethod]
        public void TestActionArray()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            Assert.IsNotNull(c);
            double[] a = c.GetActionArray(newQState(1));
            Assert.AreEqual(10, a.Length, "Expected 10 elements in the full action array.");
            a = c.GetKnownActionArray(newQState(122));
            Assert.IsNull(a, "Expected unknown state to return a null known action array.");
            a = c.GetKnownActionArray(newQState(125,1));
            Assert.IsNotNull(a, "Expected known state to return a non-null known action array.");
            a = c.GetKnownActionArray(newQState(10, 2));
            Assert.IsNotNull(a, "Expected known state to return a non-null known action array.");
            Assert.AreEqual(3, a.Length, "Expected known state to return 3 known actions in the array.");
        }
        [TestMethod]
        public void TestShape()
        {
            IHyperQ<decimal> c = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            Assert.IsNotNull(c);
            c.SetValue(newQState(2), 3, 2.0);
            Assert.AreEqual(1u, c.Shape.Item1);
            c.SetValue(newQState(1), 5, 1.0);
            Assert.AreEqual(2u, c.Shape.Item1);
            c.SetValue(newQState(1), 5, 1.0);
            Assert.AreEqual(2u, c.Shape.Item1);
            Assert.AreEqual(2u, c.Shape.Item2);

            c = new DoubleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            Assert.IsNotNull(c);
            c.SetValue(newQState(2), 3, 2.0);
            Assert.AreEqual(1u, c.Shape.Item1);
            c.SetValue(newQState(1), 5, 1.0);
            Assert.AreEqual(2u, c.Shape.Item1);
            c.SetValue(newQState(1), 5, 1.0);
            Assert.AreEqual(2u, c.Shape.Item1);
            Assert.AreEqual(2u, c.Shape.Item2);
        }
    }
}
