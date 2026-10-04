using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace HyperQ.Test
{
    /// <summary>
    /// <see cref="PvESARSATrainer{T}.Obsess"/> replays the last episode through the memory's ReplayEpisode. The plain
    /// and negative/positive memories used to replay nothing, because nothing filled the episode they replay.
    /// </summary>
    [TestClass]
    public class ObsessTest
    {
        /// <summary>A corridor: every action moves one cell on, costs 1, and the episode ends after Length steps.</summary>
        private sealed class ChainEnv : IPvEEnv<decimal, ScalarReward>
        {
            private int _position;

            public int Length { get; set; } = 5;

            public EnvMetrics Metrics { get; } = new EnvMetrics();

            public decimal Discretize()
            {
                return _position;
            }

            public void Render()
            {
            }

            public void Reset()
            {
                _position = 0;
            }

            public EnvResult<ScalarReward> Step(QAction action)
            {
                _position++;
                return new EnvResult<ScalarReward>(-1.0, _position >= Length);
            }
        }

        private sealed class CountingQ : ClassicQ
        {
            public int Updates { get; private set; }

            public CountingQ(QActionSpace<int> actionSpace) : base(actionSpace)
            {
            }

            public override double OnPolicyUpdate(decimal s, int a, decimal sprime, int aprime, double r, HyperParams hp)
            {
                Updates++;
                return base.OnPolicyUpdate(s, a, sprime, aprime, r, hp);
            }

            public override double OffPolicyUpdate(decimal s, int a, decimal sprime, double r, HyperParams hp, EvalMethodType evalType = EvalMethodType.Max)
            {
                Updates++;
                return base.OffPolicyUpdate(s, a, sprime, r, hp, evalType);
            }
        }

        private static void ObsessReplaysTheLastEpisode(QMemory<decimal, ScalarReward> memory)
        {
            QRandom ran = new QRandom(4);
            QActionSpace<int> actions = new QOrdinalActionSpace(ran, 2);
            CountingQ q = new CountingQ(actions);
            PvESARSATrainer<decimal> trainer = new PvESARSATrainer<decimal>(q, QEvalType.OffPolicy, new eGreedyActionSelector<decimal>(actions), ran);
            trainer.EnableMemory(memory);
            ChainEnv env = new ChainEnv();
            HyperParams hp = new HyperParams(0.9, 0.2, 0.5);

            trainer.Episode(env, hp);
            trainer.Obsess(hp, 2);
            Assert.AreEqual(3 * 5, q.Updates, "one live update per step, then the episode replayed twice");

            // A second, longer episode replaces the first.
            env.Length = 7;
            trainer.Episode(env, hp);
            trainer.Obsess(hp);
            Assert.AreEqual(3 * 5 + 2 * 7, q.Updates, "the second episode, replayed once");
        }

        [TestMethod]
        public void ObsessReplaysThePlainMemorysLastEpisode()
        {
            ObsessReplaysTheLastEpisode(new QMemory<decimal>(1000, new QRandom(1)));
        }

        [TestMethod]
        public void ObsessReplaysTheWholeEpisodeEvenWhenItOutgrowsThePlainMemory()
        {
            ObsessReplaysTheLastEpisode(new QMemory<decimal>(3, new QRandom(1)));
        }

        [TestMethod]
        public void ObsessReplaysTheNegPosMemorysLastEpisode()
        {
            ObsessReplaysTheLastEpisode(new QNegPosMemory<decimal>(1000, new QRandom(1)));
        }

        [TestMethod]
        public void ObsessReplaysTheEpisodicMemorysLastEpisode()
        {
            ObsessReplaysTheLastEpisode(new QEpisodicMemory<decimal>(10, new QRandom(1)));
        }

        [TestMethod]
        public void ObsessReplaysTheEpisodicNegPosMemorysLastEpisode()
        {
            ObsessReplaysTheLastEpisode(new QEpisodicNegPosMemory<decimal>(10, new QRandom(1)));
        }

        [TestMethod]
        public void ReplayingFromAnEmptyEpisodicMemoryDoesNothing()
        {
            int calls = 0;
            new QEpisodicMemory<decimal>(10, new QRandom(1)).ReplayEpisode(new HyperParams(), (cell, hp) => { calls++; return true; });
            new QEpisodicNegPosMemory<decimal>(10, new QRandom(1)).ReplayEpisode(new HyperParams(), (cell, hp) => { calls++; return true; });
            Assert.AreEqual(0, calls);
        }

        [TestMethod]
        public void TheMaceMemoryReplaysTheEpisodeInOrder()
        {
            var memory = new HyperQ.MACE.Training.QMemory<int, ScalarReward>(2, new QRandom(1));
            QAction[] a = new QAction[] { new QAction(0, 0.0, 0) };
            memory.StartEpisode();
            for (int s = 1; s <= 3; s++)
            {
                memory.Remember(s, a, s + 1, a, -1.0);
            }
            memory.EndEpisode();
            List<int> replayed = new List<int>();
            memory.ReplayEpisode(new HyperParams(), (cell, hp) => { replayed.Add(cell.s); return true; });
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, replayed);

            memory.StartEpisode();
            replayed.Clear();
            memory.ReplayEpisode(new HyperParams(), (cell, hp) => { replayed.Add(cell.s); return true; });
            Assert.AreEqual(0, replayed.Count, "a new episode starts empty");
        }
    }
}
