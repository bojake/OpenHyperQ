using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace HyperQ.Test
{
    [TestClass]
    public class IHyperQTest
    {
        private QState<decimal> newQState(params int[] v) {
            QState<decimal> q = new QState<decimal>();
            for (int i = 0; i < v.Length; i++)
            {
                q.Push((decimal)v[i]);
            }
            return (q);
        }
        private IHyperQ<decimal> MakeSingleQ()
        {
            IHyperQ<decimal> q1 = new SingleHyperQ<decimal>(new QOrdinalActionSpace(QRandom.Instance,10));
            q1.SetValue(newQState(10), 1, 5.0);
            q1.SetValue(newQState(10), 1, 7.0);
            q1.SetValue(newQState(7), 2, 9.0);
            q1.SetValue(newQState(125), 5, 17.0);
            q1.SetValue(newQState(8), 5, 29.0);
            q1.SetValue(newQState(122), 5, 17.0);
            q1.SetValue(newQState(122), 3, 27.0);
            q1.SetValue(newQState(122), 4, 18.0);
            q1.SetValue(newQState(11), 5, 13.0);
            q1.SetValue(newQState(10), 5, 11.0);
            q1.SetValue(newQState(10), 4, 15.0);
            q1.SetValue(newQState(10), 3, 3.0);
            q1.SetValue(newQState(10), 2, 18.0);
            q1.SetValue(newQState(10), 1, 19.0);
            q1.SetValue(newQState(12), 5, 89.0);
            q1.SetValue(newQState(12), 5, 26.0);
            q1.SetValue(newQState(7), 5, 21.0);
            q1.SetValue(newQState(7), 4, 19.0);
            q1.SetValue(newQState(7), 2, 13.0);
            q1.SetValue(newQState(7), 3, 26.0);
            return (q1);
        }

        private IHyperQ<decimal> MakeDoubleQ()
        {
            IHyperQ<decimal> q1 = new DoubleHyperQ<decimal>(new QOrdinalActionSpace(QRandom.Instance, 10));
            q1.SetValue(newQState(10), 1, 5.0);
            q1.SetValue(newQState(10), 1, 7.0);
            q1.SetValue(newQState(7), 2, 9.0);
            q1.SetValue(newQState(125), 5, 17.0);
            q1.SetValue(newQState(8), 5, 29.0);
            q1.SetValue(newQState(122), 5, 17.0);
            q1.SetValue(newQState(122), 4, 18.0);
            q1.SetValue(newQState(122), 3, 27.0);
            q1.SetValue(newQState(11), 5, 13.0);
            q1.SetValue(newQState(10), 5, 11.0);
            q1.SetValue(newQState(10), 4, 15.0);
            q1.SetValue(newQState(10), 3, 3.0);
            q1.SetValue(newQState(10), 2, 18.0);
            q1.SetValue(newQState(10), 1, 19.0);
            q1.SetValue(newQState(12), 5, 89.0);
            q1.SetValue(newQState(12), 5, 26.0);
            q1.SetValue(newQState(7), 5, 21.0);
            q1.SetValue(newQState(7), 4, 19.0);
            q1.SetValue(newQState(7), 3, 26.0);
            q1.SetValue(newQState(7), 2, 13.0);
            return (q1);
        }

        [TestMethod]
        public void TestCtor()
        {
            IHyperQ<decimal> q = new SingleHyperQ<decimal>(new QOrdinalActionSpace(QRandom.Instance, 10));
            Assert.IsNotNull(q);
            Assert.AreEqual(10u, q.ActionSpace.MaximumNumberOfActions);
            Assert.AreEqual(0u, q.ActionSpace.NumberOfKnownActions);
            q = new DoubleHyperQ<decimal>(new QOrdinalActionSpace(QRandom.Instance, 10));
            Assert.IsNotNull(q);
            Assert.AreEqual(10u, q.ActionSpace.MaximumNumberOfActions);
            Assert.AreEqual(0u, q.ActionSpace.NumberOfKnownActions);
        }
        [TestMethod]
        public void TestSetGetValue()
        {
            IHyperQ<decimal> q = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            q.SetValue(newQState(10), 1, 5.0);
            Assert.AreEqual(5.0, q.GetValue(newQState(10), 1));
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
            q1.SetValue(newQState(1), 5, 1.0);
            q1.SetValue(newQState(2), 3, 2.0);
            q1.SetValue(newQState(3), 9, 3.0);
            q1.SetValue(newQState(1), 6, 4.0);
            IHyperQ<decimal> c = (IHyperQ<decimal>)q1.Clone();
            Assert.IsNotNull(c);
            Assert.AreEqual(1.0, c.GetValue(newQState(1), 5));
            Assert.AreEqual(2.0, c.GetValue(newQState(2), 3));
            Assert.AreEqual(3.0, c.GetValue(newQState(3), 9));
            Assert.AreEqual(4.0, c.GetValue(newQState(1), 6));
            // harder test
            c = MakeSingleQ();
            Assert.IsNotNull(c);
            Assert.AreEqual(13.0, c.GetValue(newQState(7), 2));
            Assert.AreEqual(26.0, c.GetValue(newQState(12), 5));
            Assert.AreEqual(26.0, c.GetValue(newQState(7), 3));
            Assert.AreEqual(3.0, c.GetValue(newQState(10), 3));
        }
        [TestMethod]
        public void TestArgMax()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            QAction r = c.ArgMax(newQState(7));
            Assert.IsNotNull(r);
            Assert.AreEqual(3, r.Item1);
            Assert.AreEqual(26.0, r.Item2);
            r = c.ArgMax(newQState(55324234)); // unknown
            c = MakeDoubleQ();
            r = c.ArgMax(newQState(7));
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
            QAction r = c.ArgMin(newQState(7));
            Assert.IsNotNull(r);
            Assert.AreEqual(2, r.Item1);
            Assert.AreEqual(13.0, r.Item2);
            r = c.ArgMax(newQState(55324234)); // unknown
            c = MakeDoubleQ();
            r = c.ArgMin(newQState(7));
            Assert.IsNotNull(r);
            // The SetValue calls alternate between the two tables. For state 7 that leaves
            // Q1 = {2: 9, 5: 21, 3: 26} and Q2 = {4: 19, 2: 13}, so the blended values are
            // action 2 = 11, action 3 = 13, action 4 = 9.5 and action 5 = 10.5. The minimum is action 4.
            Assert.AreEqual(4, r.Item1);
            Assert.AreEqual(9.5, r.Item2);
        }

        /// <summary>
        /// A mapped action space whose index order is the reverse of the action order. Mapped spaces
        /// assign indexes in the order actions are first seen, so seeding it backwards makes index i
        /// refer to action (n - 1 - i).
        /// </summary>
        private static StaticMappedActionSpace ReverseMappedSpace(uint n)
        {
            StaticMappedActionSpace space = new StaticMappedActionSpace(QRandom.Instance, n);
            for (int a = (int)n - 1; a >= 0; a--)
            {
                space.ToIndex(a);
            }
            return space;
        }

        [TestMethod]
        public void TestSingleHyperQArgMaxUsesActionIndexesNotRowPositions()
        {
            StaticMappedActionSpace space = ReverseMappedSpace(10);
            // Rows fill in the order actions are first queried: action a receives the value a + 1.
            double next = 0.0;
            SingleHyperQ<decimal> q = new SingleHyperQ<decimal>(space, () => ++next);
            QState<decimal> s = newQState(42);
            // Materializes every action of the state without going through SetValue, so ArgMax has to
            // scan the row rather than use the SetValue cache.
            double[] all = q.GetActionArray(s);
            Assert.AreEqual(10.0, all[9]);
            QAction mx = q.ArgMax(s);
            Assert.IsNotNull(mx);
            Assert.AreEqual(9, mx.Action, "ArgMax must report the action with the largest value, not the row position.");
            Assert.AreEqual(10.0, mx.Reward);
            Assert.AreEqual(space.ToIndex(9), mx.Index);
            QAction mn = q.ArgMin(s);
            Assert.IsNotNull(mn);
            Assert.AreEqual(0, mn.Action, "ArgMin must report the action with the smallest value, not the row position.");
            Assert.AreEqual(1.0, mn.Reward);
            Assert.AreEqual(space.ToIndex(0), mn.Index);
        }

        [TestMethod]
        public void TestDoubleHyperQArgMaxBlendsByActionIndex()
        {
            StaticMappedActionSpace space = ReverseMappedSpace(10);
            DoubleHyperQ<decimal> q = new DoubleHyperQ<decimal>(space);
            QState<decimal> s = newQState(7);
            // Updates alternate between the two tables, so each table sees the actions in a different order.
            q.SetValue(s, 3, 8.0); // Q1
            q.SetValue(s, 6, 2.0); // Q2
            q.SetValue(s, 6, 4.0); // Q1
            q.SetValue(s, 3, 1.0); // Q2
            // Blended: action 3 = 0.5 * (8 + 1) = 4.5, action 6 = 0.5 * (4 + 2) = 3.0
            QAction mx = q.ArgMax(s);
            Assert.IsNotNull(mx);
            Assert.AreEqual(3, mx.Action);
            Assert.AreEqual(4.5, mx.Reward);
            QAction mn = q.ArgMin(s);
            Assert.IsNotNull(mn);
            Assert.AreEqual(6, mn.Action);
            Assert.AreEqual(3.0, mn.Reward);
            // Lowering the best action must be reflected by the next ArgMax; the cache can not be stale.
            q.SetValue(s, 3, 0.0); // Q1 -> action 3 = 0.5 * (0 + 1) = 0.5
            mx = q.ArgMax(s);
            Assert.IsNotNull(mx);
            Assert.AreEqual(6, mx.Action);
            Assert.AreEqual(3.0, mx.Reward);
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
            Assert.AreEqual(13.0, c[newQState(7), 2]);
            Assert.AreEqual(26.0, c[newQState(12), 5]);
            Assert.AreEqual(26.0, c[newQState(7), 3]);
            Assert.AreEqual(3.0, c[newQState(10), 3]);
        }

        [TestMethod]
        public void DoubleHyperQMappingTest()
        {
            IHyperQ<decimal> c = MakeDoubleQ();
            uint ix1 = c.AddState(newQState(1));
            uint ix2 = c.AddState(newQState(2));
            uint ix3 = c.AddState(newQState(3));
            bool b = c.RemoveState(newQState(2));
            Assert.IsTrue(b, "Failed to remove a state.");
            uint ix4 = c.AddState(newQState(4));
            Assert.AreEqual(ix4, ix2, "Removed and added state index should be the same (2 v 4)");
        }
        [TestMethod]
        public void TestActionArray()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            Assert.IsNotNull(c);
            double[] a = c.GetActionArray(newQState(1));
            Assert.AreEqual(10, a.Length, "Expected 10 elements in the full action array.");
            a = c.GetKnownActionArray(newQState(122));
            Assert.AreEqual(3, a.Length, "Expected 3 elements in the known action array for state 122.");
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

        [TestMethod]
        public void TestSingleHyperQArgMaxFindsGlobalMaximumNotFirstIncrease()
        {
            var q = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            var s = newQState(999);

            q.SetValue(s, 1, 5.0);
            q.SetValue(s, 2, 6.0);
            q.SetValue(s, 3, 9.0);

            var mx = q.ArgMax(s);
            Assert.IsNotNull(mx);
            Assert.AreEqual(3, mx.Item1, "ArgMax should pick the global best action.");
            Assert.AreEqual(9.0, mx.Item2, "ArgMax value should match the global best Q.");
        }

        [TestMethod]
        public void TestSingleHyperQArgMinFindsGlobalMinimumNotFirstDecrease()
        {
            var q = new SingleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
            var s = newQState(1000);

            q.SetValue(s, 1, 5.0);
            q.SetValue(s, 2, 4.0);
            q.SetValue(s, 3, 1.0);

            var mn = q.ArgMin(s);
            Assert.IsNotNull(mn);
            Assert.AreEqual(3, mn.Item1, "ArgMin should pick the global minimum action.");
            Assert.AreEqual(1.0, mn.Item2, "ArgMin value should match the global minimum Q.");
        }
        [TestMethod]
        public void TestSingleAsMatrix()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            Assert.IsNotNull(c);
            double[,] m = c.AsMatrix;
            Assert.IsNotNull(m);
            Assert.AreEqual(10, m.GetLength(1));
        }

        [TestMethod]
        public void TestDoubleHyperQNextQExplicitSelection()
        {
            var q = new DoubleHyperQ<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));

            q.NextQ(0);
            var q0 = q.CurrentQ();
            q.NextQ(1);
            var q1 = q.CurrentQ();

            Assert.AreNotSame(q0, q1, "NextQ(1) should switch to the alternate Q learner.");
            q.NextQ(0);
            Assert.AreSame(q0, q.CurrentQ(), "NextQ(0) should switch back to the original Q learner.");
        }

        [TestMethod]
        public void TestDoubleAsMatrix()
        {
            IHyperQ<decimal> c = MakeDoubleQ();
            Assert.IsNotNull(c);
            double[,] m = c.AsMatrix;
            Assert.IsNotNull(m);
            Assert.AreEqual(10, m.GetLength(1));
            c.SetValue(newQState(1), 5, 1.0);
            m = c.AsMatrix;
            Assert.IsNotNull(m);
            Assert.AreEqual(10, m.GetLength(1));
            c.SetValue(newQState(2), 3, 2.0);
            m = c.AsMatrix;
            Assert.IsNotNull(m);
            Assert.AreEqual(10, m.GetLength(1));
        }
    }
}
