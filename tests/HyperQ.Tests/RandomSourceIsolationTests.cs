using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace HyperQ.Test
{
    /// <summary>
    /// There is no process-wide random source or index repository: a run is reproducible from its seed, and a run
    /// that does not share a learner's source or repository cannot change what that learner does.
    /// </summary>
    [TestClass]
    public class RandomSourceIsolationTests
    {
        private const int Steps = 400;

        /// <summary>
        /// Trains a double-Q learner with epsilon-greedy selection on a five-state chain (move left or right, reward
        /// 1 on reaching the right end, which restarts at the left end). The selector's exploration and the learner's
        /// choice of table both draw from the action space's source. <paramref name="between"/> runs after every
        /// step, so another learner can draw from its own source in between.
        /// </summary>
        private static double[] TrainChain(QRandom ran, Action between = null)
        {
            QOrdinalActionSpace space = new QOrdinalActionSpace(ran, 2);
            DoubleHyperQ<decimal> q = new DoubleHyperQ<decimal>(space);
            eGreedyActionSelector<QState<decimal>> selector = new eGreedyActionSelector<QState<decimal>>(space);
            HyperParams hp = new HyperParams(g: 0.9, e: 0.3, a: 0.5);
            int s = 0;
            for (int i = 0; i < Steps; i++)
            {
                QState<decimal> state = new QState<decimal>(new decimal[] { s });
                QAction a = selector.SelectAction(q, state, hp.Epsilon);
                int next = a.Action == 1 ? Math.Min(4, s + 1) : Math.Max(0, s - 1);
                double r = next == 4 ? 1.0 : 0.0;
                q.OffPolicyUpdate(state, a.Action, new QState<decimal>(new decimal[] { next }), r, hp);
                s = next == 4 ? 0 : next;
                between?.Invoke();
            }
            return q.AsMatrix.Cast<double>().ToArray();
        }

        [TestMethod]
        public void SameSeed_ReproducesTheRun()
        {
            CollectionAssert.AreEqual(TrainChain(new QRandom(11)), TrainChain(new QRandom(11)));
        }

        [TestMethod]
        public void OtherSeed_ChangesTheRun()
        {
            CollectionAssert.AreNotEqual(TrainChain(new QRandom(11)), TrainChain(new QRandom(12)));
        }

        [TestMethod]
        public void DrawsFromAnotherSource_DoNotChangeTheRun()
        {
            double[] alone = TrainChain(new QRandom(11));
            // A second learner with its own source draws between every step of the first.
            QRandom other = new QRandom(99);
            QOrdinalActionSpace otherSpace = new QOrdinalActionSpace(other, 2);
            DoubleHyperQ<decimal> otherQ = new DoubleHyperQ<decimal>(otherSpace);
            eGreedyActionSelector<QState<decimal>> otherSelector = new eGreedyActionSelector<QState<decimal>>(otherSpace);
            HyperParams otherHp = new HyperParams(g: 0.9, e: 1.0, a: 0.5);
            QState<decimal> otherState = new QState<decimal>(new decimal[] { 0 });
            double[] interleaved = TrainChain(new QRandom(11), () =>
            {
                QAction a = otherSelector.SelectAction(otherQ, otherState, otherHp.Epsilon);
                otherQ.OffPolicyUpdate(otherState, a.Action, otherState, 0.0, otherHp);
            });
            CollectionAssert.AreEqual(alone, interleaved);
        }

        [TestMethod]
        public void EachLearner_NumbersItsStatesFromZero()
        {
            SingleHyperQ<decimal> first = new SingleHyperQ<decimal>(new QOrdinalActionSpace(new QRandom(0), 2));
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual((uint)i, first.MapState(new QState<decimal>(new decimal[] { i })));
            }
            SingleHyperQ<decimal> second = new SingleHyperQ<decimal>(new QOrdinalActionSpace(new QRandom(0), 2));
            Assert.AreEqual(0u, second.MapState(new QState<decimal>(new decimal[] { 7 })), "another learner's states do not use up this one's rows");
        }

        [TestMethod]
        public void HyperMapperLevels_ShareOneIndexSpace()
        {
            // States of different lengths are mapped at different levels of the tree; their indexes address rows
            // of one table, so no two may coincide.
            MemoryBackedHyperMapper<decimal> map = new MemoryBackedHyperMapper<decimal>();
            decimal[][] states = { new[] { 1M }, new[] { 1M, 2M }, new[] { 2M }, new[] { 1M, 3M }, new[] { 1M, 2M, 3M }, new[] { 2M, 1M } };
            uint[] indexes = states.Select(k => map[new QState<decimal>(k).Enumerator]).ToArray();
            CollectionAssert.AreEquivalent(Enumerable.Range(0, states.Length).Select(i => (uint)i).ToArray(), indexes);
            MemoryBackedHyperMapper<decimal> clone = map.Clone();
            Assert.AreEqual((uint)states.Length, clone[new QState<decimal>(new[] { 3M, 3M }).Enumerator], "a clone keeps drawing from the same index space");
        }
    }
}
