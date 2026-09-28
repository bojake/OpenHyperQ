using HyperQ.Learners;
using HyperQ.MultiHead;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HyperQ.Test
{
    [TestClass]
    public class MultiHeadHyperQLearnerTest
    {
        private QState<decimal> newQState(params int[] v)
        {
            QState<decimal> q = new QState<decimal>();
            for (int i = 0; i < v.Length; i++)
            {
                q.Push((decimal)v[i]);
            }
            return q;
        }

        private IHyperQ<decimal> MakeSingleQ()
        {
            IHyperQ<decimal> q1 = new SingleHyperQ<decimal>(new QOrdinalActionSpace(new QRandom(0), 10));
            return q1;
        }

        private IHyperQ<decimal> MakeDoubleQ()
        {
            IHyperQ<decimal> q1 = new DoubleHyperQ<decimal>(new QOrdinalActionSpace(new QRandom(0), 10));
            return q1;
        }

        [TestMethod]
        public void TestCtor()
        {
            var scalarizer = new LinearScalarizer(new double[] { 1.0, 0.0 });
            var mh = new MultiHeadHyperQLearner<decimal>(MakeSingleQ, headCount: 2, lambda: 1.0, scalarizer: scalarizer);
            Assert.IsNotNull(mh.Heads);
            Assert.AreEqual(2, mh.Heads.Length);
            mh = new MultiHeadHyperQLearner<decimal>(MakeDoubleQ, headCount: 2, lambda: 1.0, scalarizer: scalarizer);
            Assert.IsNotNull(mh.Heads);
            Assert.AreEqual(2, mh.Heads.Length);
        }

        [TestMethod]
        public void TestSingleHyperQUpdateAndAggregation()
        {
            var hp = new HyperParams(a: 0.5, g: 0.0);
            var s = newQState(1, 2, 3);
            var sp = newQState(4, 5, 6);
            int a = 0;
            int ap = 0;

            var scalarizer = new LinearScalarizer(new double[] { 1.0, 0.0 });
            var q = new MultiHeadHyperQLearner<decimal>(MakeSingleQ, headCount: 2, lambda: 1.0, scalarizer: scalarizer);
            q.AddState(s);
            q.AddState(sp);

            var r = new MultiReward(new double[] { 1.0, -2.0 });
            q.OnPolicyUpdate(s, a, sp, ap, r, hp);

            Assert.AreEqual(0.5, q.GetHead(0).GetValue(s, a), 1e-9);
            Assert.AreEqual(-1.0, q.GetHead(1).GetValue(s, a), 1e-9);
        }

        [TestMethod]
        public void TestArgMaxAndArgMinScanWholeActionSpace()
        {
            var scalarizer = new LinearScalarizer(new double[] { 1.0, 1.0 });
            var q = new MultiHeadHyperQLearner<decimal>(MakeSingleQ, headCount: 2, lambda: 1.0, scalarizer: scalarizer);
            var s = newQState(10);

            // Make action 0 strictly minimal across all 10 actions (including untouched defaults).
            q.GetHead(0).SetValue(s, 0, -3.0); q.GetHead(1).SetValue(s, 0, -2.0);
            q.GetHead(0).SetValue(s, 1, 1.5); q.GetHead(1).SetValue(s, 1, 1.5);
            q.GetHead(0).SetValue(s, 2, 4.0); q.GetHead(1).SetValue(s, 2, 5.0);
            q.GetHead(0).SetValue(s, 3, 2.0); q.GetHead(1).SetValue(s, 3, 2.0);

            var argMax = q.ArgMax(s);
            Assert.AreEqual(2, argMax.Action);

            var argMin = q.ArgMin(s);
            Assert.AreEqual(0, argMin.Action);
        }

        [TestMethod]
        public void TestOnPolicyRejectsRewardDimMismatch()
        {
            var hp = new HyperParams(a: 0.5, g: 0.0);
            var q = new MultiHeadHyperQLearner<decimal>(MakeSingleQ, headCount: 2, lambda: 1.0, scalarizer: new LinearScalarizer(new double[] { 1.0, 1.0 }));
            var s = newQState(1);
            var sp = newQState(2);
            try
            {
                q.OnPolicyUpdate(s, 0, sp, 0, new MultiReward(new double[] { 1.0 }), hp);
                Assert.Fail("Expected ArgumentException for reward/head dimension mismatch.");
            }
            catch (System.ArgumentException)
            {
            }
        }

        [TestMethod]
        public void TestHeadsArrayCtorSetsHeadCountAndSupportsUpdates()
        {
            IHyperQ<decimal>[] heads = new IHyperQ<decimal>[]
            {
                new SingleHyperQ<decimal>(new QOrdinalActionSpace(new QRandom(0), 10)),
                new SingleHyperQ<decimal>(new QOrdinalActionSpace(new QRandom(0), 10))
            };
            var q = new MultiHeadHyperQLearner<decimal>(heads, scalarizer: new LinearScalarizer(new double[] { 1.0, 1.0 }));
            Assert.AreEqual(2, q.HeadCount);

            var hp = new HyperParams(a: 0.5, g: 0.0);
            var s = newQState(1);
            var sp = newQState(2);
            var updated = q.OnPolicyUpdate(s, 0, sp, 0, new MultiReward(new double[] { 2.0, 4.0 }), hp);
            Assert.AreEqual(3.0, updated, 1e-9);
        }

        [TestMethod]
        public void TestDefaultScalarizerMatchesHeadCount()
        {
            var q = new MultiHeadHyperQLearner<decimal>(MakeSingleQ, headCount: 3, lambda: 1.0, scalarizer: null);
            var s = newQState(77);

            // Head0+Head1 are identical across actions, so only Head2 can distinguish.
            q.GetHead(0).SetValue(s, 0, 0.0); q.GetHead(1).SetValue(s, 0, 0.0); q.GetHead(2).SetValue(s, 0, 1.0);
            q.GetHead(0).SetValue(s, 1, 0.0); q.GetHead(1).SetValue(s, 1, 0.0); q.GetHead(2).SetValue(s, 1, 5.0);

            var best = q.ArgMax(s);
            Assert.AreEqual(1, best.Action, "Default scalarizer should include all three heads.");
        }
    }
}
