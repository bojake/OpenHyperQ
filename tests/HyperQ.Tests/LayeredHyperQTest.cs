using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace HyperQ.Test
{
    [TestClass]
    public class LayeredHyperQTest
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
        private SingleQGenerator<decimal> MakeSingleGenerator()
        {
            return new SingleQGenerator<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
        }
        private DoubleQGenerator<decimal> MakeDoubleGenerator()
        {
            return new DoubleQGenerator<decimal>(new StaticMappedActionSpace(QRandom.Instance, 10));
        }
        private IHyperQ<decimal> MakeSingleQ()
        {
            SingleQGenerator<decimal> sg = MakeSingleGenerator();
            IHyperQ<decimal> q1 = new LayeredHyperQ<decimal>(sg.LayeredQ,sg.ActionSpace);
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
            q1.SetValue(newQState(5), 2, 1.0);
            q1.SetValue(newQState(5), 3, 7.0);
            q1.SetValue(newQState(5), 4, 5.0);
            q1.SetValue(newQState(5), 3, 2.0); // Important test, Max(5) = 5 now, Min(5) = 1
            /*        1  |  2  |  3  |  4  |  5  |
             * 7   : --- | 13  | 26  | 19  | 21  |
             * 8   : --- | --- | --- | --- | 29  |
             * 10  : 19  | 18  |  3  | 15  | 11  |
             * 11  : --- | --- | --- | --- | 13  |
             * 12  : --- | --- | --- | --- | 26  |
             * 122 : --- | --- | 27  | 18  | 17  |
             * 125 : --- | --- | --- | --- | 17  |
             */
            return (q1);
        }

        private IHyperQ<decimal> MakeDoubleQ()
        {
            DoubleQGenerator<decimal> dg = MakeDoubleGenerator();
            IHyperQ<decimal> q1 = new LayeredHyperQ<decimal>(dg.Layered, dg.ActionSpace);
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
            IHyperQ<decimal> q = MakeSingleQ();
            Assert.IsNotNull(q);
            Assert.AreEqual(10u, q.ActionSpace.MaximumNumberOfActions);
            Assert.AreEqual(5u, q.ActionSpace.NumberOfKnownActions);
            q = MakeDoubleQ();
            Assert.IsNotNull(q);
            Assert.AreEqual(10u, q.ActionSpace.MaximumNumberOfActions);
            Assert.AreEqual(5u, q.ActionSpace.NumberOfKnownActions);
        }
        [TestMethod]
        public void TestSetGetValue()
        {
            IHyperQ<decimal> q = MakeSingleQ();
            q.SetValue(newQState(10), 1, 5.0);
            Assert.AreEqual(5.0, q.GetValue(newQState(10), 1));
            DoubleQGenerator<decimal> dg = MakeDoubleGenerator();
            IHyperQ<decimal> q2 = new LayeredHyperQ<decimal>(dg.Layered, dg.ActionSpace);
            q2.SetValue(newQState(10), 1, 5.0);
            // 10,1: 5,7,19,5 = 
            // Alt-Q has zero for this state, so 0.5 * (0 + 5) = 2.5, expected value
            Assert.AreEqual(2.5, q2.GetValue(newQState(10), 1));
        }
        [TestMethod]
        public void TestMergeMax()
        {
            IHyperQ<decimal> q1 = MakeSingleQ();
            q1.SetValue(newQState(10), 1, 5.0);
            IHyperQ<decimal> q2 = MakeSingleQ();
            q2.SetValue(newQState(10), 1, 7.0);
            q2.SetValue(newQState(7), 2, 9.0);
            // Test Merge MAX
            try
            {
                q1.MergeInto(q2, EvalMethodType.Max);
                Assert.Fail("Merge is not implemented in the HyperQ");
            }
            catch (Exception)
            {
                // Expected, not implemented
            }
        }
        [TestMethod]
        public void TestClone()
        {
            SingleQGenerator<decimal> sg = MakeSingleGenerator();
            IHyperQ<decimal> q1 = new LayeredHyperQ<decimal>(sg.LayeredQ, sg.ActionSpace);
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
            r = c.ArgMax(newQState(5));
            Assert.IsNotNull(r);
            Assert.AreEqual(4, r.Item1);
            Assert.AreEqual(5.0, r.Item2);
            c = MakeDoubleQ();
            r = c.ArgMax(newQState(7));
            Assert.IsNotNull(r);
            Assert.AreEqual(3, r.Item1);
            // Expected 13 = 0.5 * (0 + 26)
            Assert.AreEqual(13.0, r.Item2);
        }
        [TestMethod]
        public void TestArgMin()
        {
            IHyperQ<decimal> c = MakeSingleQ();
            QAction r = c.ArgMin(newQState(7));
            Assert.IsNotNull(r);
            Assert.AreEqual(2, r.Item1);
            Assert.AreEqual(13.0, r.Item2);
            c = MakeDoubleQ();
            r = c.ArgMin(newQState(7));
            Assert.IsNotNull(r);
            // The SetValue calls alternate between the two tables. For state 7 that leaves
            // Q1 = {2: 9, 5: 21, 3: 26} and Q2 = {4: 19, 2: 13}, so the blended values are
            // action 2 = 11, action 3 = 13, action 4 = 9.5 and action 5 = 10.5. The minimum is action 4.
            Assert.AreEqual(4, r.Item1);
            Assert.AreEqual(9.5, r.Item2);
        }

        [TestMethod]
        public void TestAsMatrixListsCompleteStatesInIndexOrder()
        {
            LayeredHyperQ<decimal> c = (LayeredHyperQ<decimal>)MakeSingleQ();
            // MakeSingleQ touches states 10, 7, 125, 8, 122, 11, 12 and 5, in that order.
            System.Collections.Generic.List<QState<decimal>> states = c.KnownStates();
            Assert.AreEqual(8, states.Count);
            Assert.AreEqual(8u, c.Shape.Item1, "Shape counts the complete states.");
            Assert.AreEqual(10M, states[0][0]);
            Assert.AreEqual(7M, states[1][0]);
            Assert.AreEqual(5M, states[7][0]);
            double[,] m = c.AsMatrix;
            Assert.AreEqual(8, m.GetLength(0), "One row per complete state.");
            Assert.AreEqual(10, m.GetLength(1), "One column per action in the action space.");
            QActionSpace<int> space = c.ActionSpace;
            // Row 1 is state 7: {2: 13, 3: 26, 4: 19, 5: 21}; columns are action-space indexes.
            Assert.AreEqual(13.0, m[1, space.ToIndex(2)]);
            Assert.AreEqual(26.0, m[1, space.ToIndex(3)]);
            Assert.AreEqual(19.0, m[1, space.ToIndex(4)]);
            Assert.AreEqual(21.0, m[1, space.ToIndex(5)]);
            // Row 7 is state 5: {2: 1, 3: 2, 4: 5}; an action never tried there reads the default value 0.
            Assert.AreEqual(1.0, m[7, space.ToIndex(2)]);
            Assert.AreEqual(2.0, m[7, space.ToIndex(3)]);
            Assert.AreEqual(5.0, m[7, space.ToIndex(4)]);
            Assert.AreEqual(0.0, m[7, space.ToIndex(5)]);
            // Reading the matrix must not teach the layers anything.
            Assert.AreEqual(4, c.KnownActionValues(newQState(7)).Count());
            Assert.AreEqual(3, c.KnownActionValues(newQState(5)).Count());

            // The double-Q layers blend the two tables per action index (Q2 holds nothing for state 7, action 3).
            // MakeDoubleQ touches 7 distinct states (it has no state 5 entries).
            LayeredHyperQ<decimal> d = (LayeredHyperQ<decimal>)MakeDoubleQ();
            Assert.AreEqual(7u, d.Shape.Item1);
            double[,] md = d.AsMatrix;
            Assert.AreEqual(7, md.GetLength(0));
            Assert.AreEqual(10, md.GetLength(1));
            Assert.AreEqual(13.0, md[1, d.ActionSpace.ToIndex(3)], "0.5 * (26 + 0)");
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
            // Double Q
            c = MakeDoubleQ();
            ix1 = c.AddState(newQState(1));
            ix2 = c.AddState(newQState(2));
            ix3 = c.AddState(newQState(3));
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
            Assert.IsNotNull(c.ActionSpace);
            Assert.AreEqual(13.0, c[newQState(7), 2]);
            Assert.AreEqual(26.0, c[newQState(12), 5]);
            Assert.AreEqual(26.0, c[newQState(7), 3]);
            Assert.AreEqual(3.0, c[newQState(10), 3]);
        }

    }
}
