using HyperQ.MultiHead;
using HyperQ.MultiHead.Eval;
using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace HyperQ.Test
{
    [TestClass]
    public class MultiHeadQLearnerTest
    {
        [TestMethod]
        public void TestClassicQ()
        {
            var hp = new HyperParams(a: 0.5, g: 0.0); // make math easy
            var s = 123;
            var sp = 456;
            int a = 0;
            int ap = 0;

            var scalarizer = new LinearScalarizer(new double[] { 1.0, 0.0 }); // only head0
            QOrdinalActionSpace aspace = new QOrdinalActionSpace(QRandom.Instance, 10);
            var mh = new MultiHeadClassicQ(aspace, headCount: 2, lambda: 1.0, scalarizer: scalarizer);

            mh.AddState(s);
            mh.AddState(sp);

            var r = new MultiReward(new double[] { 1.0, -2.0 });

            mh.OnPolicyUpdate(s, a, sp, ap, r, hp);

            // With gamma=0, target=r, starting Q=0 => Q := alpha*r
            Assert.AreEqual(0.5, mh.GetHead(0).GetValue(s, a), 1e-9);
            Assert.AreEqual(-1.0, mh.GetHead(1).GetValue(s, a), 1e-9);

            // Now make scalarizer focus head1; best action should change accordingly in a crafted setup
            mh.Scalarizer = new LinearScalarizer(new double[] { 0.0, 1.0 });

            // Optionally: set head1 Q(s,1) > head1 Q(s,0) and confirm ArgMax chooses action 1
            mh.GetHead(1).SetValue(s, 1, 10.0);
            var best = mh.ArgMax(s);
            Assert.AreEqual(1, best.Action);
        }

        [TestMethod]
        public void TestClassicQQ()
        {
            var hp = new HyperParams(a: 0.5, g: 0.0); // make math easy
            var s = 123;
            var sp = 456;
            int a = 0;
            int ap = 0;

            var scalarizer = new LinearScalarizer(new double[] { 1.0, 0.0 }); // only head0
            QOrdinalActionSpace aspace = new QOrdinalActionSpace(QRandom.Instance, 10);
            var mh = new MultiHeadClassicQ(aspace, headCount: 2, lambda: 1.0, scalarizer: scalarizer);

            mh.AddState(s);
            mh.AddState(sp);

            var r = new MultiReward(new double[] { 1.0, -2.0 });

            mh.OnPolicyUpdate(s, a, sp, ap, r, hp);

            // With gamma=0, target=r, starting Q=0 => Q := alpha*r
            Assert.AreEqual(0.5, mh.GetHead(0).GetValue(s, a), 1e-9);
            Assert.AreEqual(-1.0, mh.GetHead(1).GetValue(s, a), 1e-9);

            // Now make scalarizer focus head1; best action should change accordingly in a crafted setup
            mh.Scalarizer = new LinearScalarizer(new double[] { 0.0, 1.0 });

            // Optionally: set head1 Q(s,1) > head1 Q(s,0) and confirm ArgMax chooses action 1
            mh.GetHead(1).SetValue(s, 1, 10.0);
            var best = mh.ArgMax(s);
            Assert.AreEqual(1, best.Action);
        }

        [TestMethod]
        public void TestGetActionArrayUsesPerActionAcrossHeads()
        {
            var scalarizer = new LinearScalarizer(new double[] { 1.0, 1.0 });
            QOrdinalActionSpace aspace = new QOrdinalActionSpace(QRandom.Instance, 3);
            var mh = new MultiHeadClassicQ(aspace, headCount: 2, lambda: 1.0, scalarizer: scalarizer);
            int s = 7;

            mh.GetHead(0).SetValue(s, 0, 1.0);
            mh.GetHead(0).SetValue(s, 1, 2.0);
            mh.GetHead(0).SetValue(s, 2, 3.0);
            mh.GetHead(1).SetValue(s, 0, 10.0);
            mh.GetHead(1).SetValue(s, 1, 20.0);
            mh.GetHead(1).SetValue(s, 2, 30.0);

            var scores = mh.GetActionArray(s);
            Assert.AreEqual(11.0, scores[0], 1e-9);
            Assert.AreEqual(22.0, scores[1], 1e-9);
            Assert.AreEqual(33.0, scores[2], 1e-9);
        }

        [TestMethod]
        public void TestArgMaxAndArgMinScanWholeActionSpace()
        {
            var scalarizer = new LinearScalarizer(new double[] { 1.0, 1.0 });
            QOrdinalActionSpace aspace = new QOrdinalActionSpace(QRandom.Instance, 4);
            var mh = new MultiHeadClassicQ(aspace, headCount: 2, lambda: 1.0, scalarizer: scalarizer);
            int s = 42;

            // Scores by action: [2, 3, 9, 4], so global max=2 and global min=0
            mh.GetHead(0).SetValue(s, 0, 1.0); mh.GetHead(1).SetValue(s, 0, 1.0);
            mh.GetHead(0).SetValue(s, 1, 1.5); mh.GetHead(1).SetValue(s, 1, 1.5);
            mh.GetHead(0).SetValue(s, 2, 4.0); mh.GetHead(1).SetValue(s, 2, 5.0);
            mh.GetHead(0).SetValue(s, 3, 2.0); mh.GetHead(1).SetValue(s, 3, 2.0);

            var argMax = mh.ArgMax(s);
            Assert.AreEqual(2, argMax.Action);

            var argMin = mh.ArgMin(s);
            Assert.AreEqual(0, argMin.Action);
        }

        [TestMethod]
        public void TestOnPolicyRejectsRewardDimMismatch()
        {
            var hp = new HyperParams(a: 0.5, g: 0.0);
            var scalarizer = new LinearScalarizer(new double[] { 1.0, 1.0 });
            QOrdinalActionSpace aspace = new QOrdinalActionSpace(QRandom.Instance, 3);
            var mh = new MultiHeadClassicQ(aspace, headCount: 2, lambda: 1.0, scalarizer: scalarizer);
            try
            {
                mh.OnPolicyUpdate(1, 0, 2, 0, new MultiReward(new double[] { 1.0 }), hp);
                Assert.Fail("Expected ArgumentException for reward/head dimension mismatch.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void TestEvaluatorAssignsSelector()
        {
            QOrdinalActionSpace aspace = new QOrdinalActionSpace(QRandom.Instance, 3);
            var mh = new MultiHeadClassicQ(aspace, headCount: 2, lambda: 1.0, scalarizer: new LinearScalarizer(new double[] { 1.0, 1.0 }));
            IActionSelector<decimal> selector = new UniformActionSelector<decimal>(mh, aspace);
            var evaluator = new MultiHeadQEvaluator<decimal, MultiReward>(mh, selector);
            Assert.AreSame(selector, evaluator.ActionSelector);
        }

        [TestMethod]
        public void TestHeadsArrayCtorSetsHeadCountAndSupportsUpdates()
        {
            QOrdinalActionSpace aspace = new QOrdinalActionSpace(QRandom.Instance, 3);
            Q<decimal>[] heads = new Q<decimal>[]
            {
                new ClassicQ(aspace),
                new ClassicQ(aspace)
            };
            var mh = new MultiHeadQLearner<decimal>(heads, scalarizer: new LinearScalarizer(new double[] { 1.0, 1.0 }));
            Assert.AreEqual(2, mh.HeadCount);

            var hp = new HyperParams(a: 0.5, g: 0.0);
            var updated = mh.OnPolicyUpdate(1M, 0, 2M, 0, new MultiReward(new double[] { 2.0, 4.0 }), hp);
            Assert.AreEqual(3.0, updated, 1e-9);
        }

        [TestMethod]
        public void TestDefaultScalarizerMatchesHeadCount()
        {
            QOrdinalActionSpace aspace = new QOrdinalActionSpace(QRandom.Instance, 3);
            var mh = new MultiHeadClassicQ(aspace, headCount: 3, lambda: 1.0, scalarizer: null);
            int s = 11;

            // Head0+Head1 are identical across actions, so only Head2 can distinguish.
            mh.GetHead(0).SetValue(s, 0, 0.0); mh.GetHead(1).SetValue(s, 0, 0.0); mh.GetHead(2).SetValue(s, 0, 1.0);
            mh.GetHead(0).SetValue(s, 1, 0.0); mh.GetHead(1).SetValue(s, 1, 0.0); mh.GetHead(2).SetValue(s, 1, 5.0);

            var best = mh.ArgMax(s);
            Assert.AreEqual(1, best.Action, "Default scalarizer should include all three heads.");
        }
    }
}
