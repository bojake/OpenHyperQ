using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using HyperQ.Env;
using HyperQ.MACE.Training;
using HyperQ.Learners;
using HyperQ.Util;

namespace HyperQ.Test
{
    [TestClass]
    public class MACETests
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

        /// <summary>
        /// Three-step environment whose state is the step counter and whose reward is the number of
        /// steps taken so far, so every (state, reward) pair in an episode is distinct.
        /// </summary>
        private class ScriptedMaceEnv : IMACEPvEEnv<decimal, ScalarReward>
        {
            public int Steps = 0;
            public EnvMetrics Metrics { get; private set; } = new EnvMetrics();
            public void Render() { }
            public void Reset() { Steps = 0; }
            public decimal Discretize() { return Steps; }
            public EnvResult<ScalarReward> Step(QAction[] action)
            {
                Steps++;
                return new EnvResult<ScalarReward>(new ScalarReward(Steps), Steps >= 3);
            }
        }

        /// <summary>
        /// Returns a fixed sequence of actions regardless of the Q values.
        /// </summary>
        private class ScriptedSelector : IActionSelector<decimal>
        {
            private readonly int[] _script;
            private int _next = 0;
            public ScriptedSelector(params int[] script) { _script = script; }
            public QAction SelectAction(IArgMinMax<decimal> q, decimal state, double epsilon)
            {
                int a = _script[_next++ % _script.Length];
                return new QAction(a, 0.0, (uint)a);
            }
            public bool LastActionWasRandom { get { return false; } }
            public long RandomActionCount { get { return 0; } }
            public void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp) { }
            public MinMaxActionEnum ActionMode { get; set; }
            public Action<decimal, QAction> TelemetryCallback { get; set; }
            public void StartEpisode() { }
            public void EndEpisode(RewardAccumulator reward) { }
        }

        [TestMethod]
        public void TestOnPolicyEpisodeUpdatesTheActionThatWasTaken()
        {
            // alpha = 1 and gamma = 0 make every SARSA update Q(s,a) = r, so the tables show exactly
            // which (s,a) pair was credited with each reward.
            HyperParams hp = new HyperParams(g: 0.0, e: 0.0, a: 1.0);
            MappedQ<decimal> angle = new MappedQ<decimal>(new StaticMappedActionSpace(new QRandom(0), 10));
            MappedQ<decimal> power = new MappedQ<decimal>(new StaticMappedActionSpace(new QRandom(0), 10));
            MACEPvESARSATrainer<decimal, ScalarReward> trainer = new MACEPvESARSATrainer<decimal, ScalarReward>(new QRandom(0), QEvalType.OnPolicy);
            trainer.Add(angle, new ScriptedSelector(3, 5, 7, 9));
            trainer.Add(power, new ScriptedSelector(2, 4, 6, 8));
            ScriptedMaceEnv env = new ScriptedMaceEnv();
            trainer.Episode(env, hp);
            // Step k starts in state k, takes the k-th scripted action and earns reward k + 1.
            Assert.AreEqual(1.0, angle.GetValue(0M, 3));
            Assert.AreEqual(1.0, power.GetValue(0M, 2));
            Assert.AreEqual(2.0, angle.GetValue(1M, 5));
            Assert.AreEqual(2.0, power.GetValue(1M, 4));
            Assert.AreEqual(3.0, angle.GetValue(2M, 7));
            Assert.AreEqual(3.0, power.GetValue(2M, 6));
            // The action selected for the next state must not be credited with the current reward.
            Assert.AreEqual(0.0, angle.GetValue(1M, 7));
            Assert.AreEqual(0.0, power.GetValue(1M, 6));
            Assert.AreEqual(0.0, angle.GetValue(2M, 9));
        }
        [TestMethod]
        public void TestCtor()
        {
            DynaState<decimal,ScalarReward> ds1 = new DynaState<decimal, ScalarReward>(100, 200);
            Assert.AreEqual(100, ds1.DynaIterations);
            Assert.AreEqual(200, ds1.HistoryCapacity);
        }

        [TestMethod]
        public void TestHistory()
        {
            QAction[] a = new QAction[3] { new QAction(0,0.0,0), new QAction(1,0.0,1), new QAction(2, 0.0, 2) };

            DynaState<decimal, ScalarReward> ds1 = new DynaState<decimal, ScalarReward>(100, 10);
            Assert.AreEqual(10, ds1.HistoryCapacity);
            HyperParams hp = new HyperParams();
            ds1.Update(1M, a, 2M, 1.0, hp);
            ds1.Update(2M, a, 3M, 1.0, hp);
            ds1.Update(3M, a, 4M, 1.0, hp);
            ds1.Update(4M, a, 5M, 1.0, hp);
            ds1.Update(5M, a, 6M, 1.0, hp);
            ds1.Update(6M, a, 7M, 1.0, hp);
            ds1.Update(7M, a, 8M, 1.0, hp);
            ds1.Update(8M, a, 9M, 1.0, hp);
            ds1.Update(9M, a, 10M, 1.0, hp);
            ds1.Update(10M, a, 11M, 1.0, hp);
            Assert.AreEqual(10, ds1.HistoryCapacity);
            ds1.Update(5M, a, 6M, 1.0, hp);
            ds1.Update(6M, a, 7M, 1.0, hp);
            ds1.Update(7M, a, 8M, 1.0, hp);
            Assert.AreEqual(10, ds1.HistoryCapacity);
        }
        [TestMethod]
        public void TestHistoryWithQState()
        {
            QAction[] a = new QAction[3] { new QAction(0, 0.0, 0), new QAction(1, 0.0, 1), new QAction(2, 0.0, 2) };

            DynaState<QState<decimal>, ScalarReward> ds1 = new DynaState<QState<decimal>, ScalarReward>(100, 10);
            Assert.AreEqual(10, ds1.HistoryCapacity);
            HyperParams hp = new HyperParams();
            ds1.Update(newQState(1), a, newQState(2), 1.0, hp);
            ds1.Update(newQState(2), a, newQState(3), 2.0, hp);
            ds1.Update(newQState(3), a, newQState(4), 3.0, hp);
            ds1.Update(newQState(4), a, newQState(5), 4.0, hp);
            Assert.AreEqual(10, ds1.HistoryCapacity);
            ds1.Update(newQState(5), a, newQState(6), 1.0, hp);
            ds1.Update(newQState(6), a, newQState(7), 2.0, hp);
            ds1.Update(newQState(7), a, newQState(8), 3.0, hp);
            ds1.Update(newQState(8), a, newQState(9), 4.0, hp);
            Assert.AreEqual(10, ds1.HistoryCapacity);
            ds1.Update(newQState(9), a, newQState(10), 1.0, hp);
            ds1.Update(newQState(10), a, newQState(11), 2.0, hp);
            ds1.Update(newQState(11), a, newQState(12), 3.0, hp);
            ds1.Update(newQState(12), a, newQState(13), 4.0, hp);
            Assert.AreEqual(10, ds1.HistoryCapacity);
            ds1.Update(newQState(1), a, newQState(2), 1.0, hp);
            ds1.Update(newQState(2), a, newQState(3), 2.0, hp);
            ds1.Update(newQState(3), a, newQState(4), 3.0, hp);
            ds1.Update(newQState(4), a, newQState(5), 4.0, hp);
            Assert.AreEqual(10, ds1.HistoryCapacity);
        }
        [TestMethod]
        public void TestSamplingWithQState()
        {
            QAction[] a = new QAction[3] { new QAction(0, 0.0, 0), new QAction(1, 0.0, 1), new QAction(2, 0.0, 2) };
            DynaState<QState<decimal>, ScalarReward> ds1 = new DynaState<QState<decimal>, ScalarReward>(100, 10);
            Assert.AreEqual(10, ds1.HistoryCapacity);
            HyperParams hp = new HyperParams();
            ds1.Update(newQState(1), a, newQState(2), 1.0, hp);
            ds1.Update(newQState(2), a, newQState(3), 2.0, hp);
            ds1.Update(newQState(3), a, newQState(4), 3.0, hp);
            ds1.Update(newQState(4), a, newQState(5), 4.0, hp);
            QState<decimal> s;
            double r;
            (s, r) = ds1.Sample(new DynaEntry<QState<decimal>>(newQState(1), a));
            Assert.AreEqual(1.0, r);
            // Do the same (s,a) with r=1
            ds1.Update(newQState(1), a, newQState(2), 1.0, hp);
            // with a=1, the dyna R should be 1.0
            Assert.AreEqual(1.0, r);
        }
        [TestMethod]
        public void TestSamplingWithQStateWithAlpha()
        {
            QAction[] a = new QAction[3] { new QAction(0, 0.0, 0), new QAction(1, 0.0, 1), new QAction(2, 0.0, 2) };
            DynaState<QState<decimal>, ScalarReward> ds1 = new DynaState<QState<decimal>, ScalarReward>(100, 10);
            Assert.AreEqual(10, ds1.HistoryCapacity);
            // alpha=0.5
            HyperParams hp = new HyperParams(1.0, 1.0, 0.5, 1.0, 1.0, 0.5, 1.0, 1.0);
            ds1.Update(newQState(1), a, newQState(2), 1.0, hp);
            ds1.Update(newQState(2), a, newQState(3), 2.0, hp);
            ds1.Update(newQState(3), a, newQState(4), 3.0, hp);
            ds1.Update(newQState(4), a, newQState(5), 4.0, hp);
            QState<decimal> s;
            double r;
            (s, r) = ds1.Sample(new DynaEntry<QState<decimal>>(newQState(1), a));
            Assert.AreEqual(0.5, r); // a * r = 0.5 * 1.0
            // Do the same (s,a) with r=3
            ds1.Update(newQState(1), a, newQState(2), 3.0, hp);
            (s, r) = ds1.Sample(new DynaEntry<QState<decimal>>(newQState(1), a));
            // with a=.5, the dyna R should be 0.5*.5 + 0.5*3 = .25 + 1.5 = 1.75 
            Assert.AreEqual(1.75, r);
        }
        [TestMethod]
        public void TestHallucinate()
        {
            QAction[] a = new QAction[3] { new QAction(0, 0.0, 0), new QAction(1, 0.0, 1), new QAction(2, 0.0, 2) };
            QRandom ran = new QRandom(0);
            DynaState<decimal, ScalarReward> ds1 = new DynaState<decimal, ScalarReward>(100, 3);
            Assert.AreEqual(3, ds1.HistoryCapacity);
            HyperParams hp = new HyperParams(1.0, 1.0, 0.5, 1.0, 1.0, 0.5, 1.0, 1.0);
            ds1.Update(1M, a, 2M, 1.0, hp);
            ds1.Update(2M, a, 3M, 2.0, hp);
            ds1.Update(3M, a, 4M, 3.0, hp);
            ds1.Update(4M, a, 5M, 4.0, hp);
            Assert.AreEqual(3, ds1.HistoryCapacity);
            for (int i = 0; i < 50; i++)
            {
                DynaEntry<decimal> d = (DynaEntry<decimal>)ds1.RandomSA(ran);
                Tuple<decimal, ScalarReward> sample = ds1.Sample(d);
                Assert.IsTrue(sample.Item1 == 3M || sample.Item1 == 4M || sample.Item1 == 5M, $"{sample.Item1} is expected to be [3,4,5]");
            }
        }
        [TestMethod]
        public void TestHallucinateWithQState()
        {
            QAction[] a = new QAction[3] { new QAction(0, 0.0, 0), new QAction(1, 0.0, 1), new QAction(2, 0.0, 2) };
            QRandom ran = new QRandom(0);
            HyperParams hp = new HyperParams(1.0, 1.0, 0.5, 1.0, 1.0, 0.5, 1.0, 1.0);
            DynaState<QState<decimal>,ScalarReward> ds1 = new DynaState<QState<decimal>, ScalarReward>(100, 3);
            Assert.AreEqual(3, ds1.HistoryCapacity);
            ds1.Update(newQState(1), a, newQState(2), 1.0, hp);
            ds1.Update(newQState(2), a, newQState(3), 2.0, hp);
            ds1.Update(newQState(3), a, newQState(4), 3.0, hp);
            ds1.Update(newQState(4), a, newQState(5), 4.0, hp);
            Assert.AreEqual(3, ds1.HistoryCapacity);
            for (int i = 0; i < 50; i++)
            {
                DynaEntry<QState<decimal>> d = (DynaEntry<QState<decimal>>)ds1.RandomSA(ran);
                Tuple<QState<decimal>, ScalarReward> sample = ds1.Sample(d);
            }
        }
    }
}