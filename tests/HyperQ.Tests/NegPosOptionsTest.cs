using HyperQ.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperQ.Test
{
    /// <summary>
    /// The replay memories' opt-in settings: KeepExtremes and ChooseListPerDraw on the negative/positive memories,
    /// ReplayBackward on every memory, and CountSteps on the episodic memories. All are off by default, and the
    /// default behavior is pinned by fingerprints recorded before the settings existed.
    /// </summary>
    [TestClass]
    public class NegPosOptionsTest
    {
        private static long Fingerprint(List<int> ids)
        {
            long h = 17;
            foreach (int id in ids)
            {
                h = unchecked(h * 31 + id);
            }
            return h;
        }

        /// <summary>
        /// Remembers 30 episodes of 15 steps, ending in -100 or +100 with mild rewards before, and replays after each
        /// episode. Returns the ids of the replayed steps.
        /// </summary>
        private static List<int> Run(QMemory<int, ScalarReward> memory, int replayCount)
        {
            Random stream = new Random(5);
            List<int> replayed = new List<int>();
            for (int episode = 0; episode < 30; episode++)
            {
                memory.StartEpisode();
                for (int t = 0; t < 15; t++)
                {
                    int id = episode * 100 + t;
                    double r = t == 14 ? (stream.Next(2) == 0 ? -100.0 : 100.0) : stream.NextDouble() * 4.0 - 2.5;
                    memory.Remember(id, null, id + 1, null, r);
                }
                memory.EndEpisode();
                memory.Playback(replayCount, null, (cell, hp) => { replayed.Add(cell.s); return true; });
            }
            return replayed;
        }

        private static List<int> Replay(Action<Func<QMemoryCell<int, ScalarReward>, HyperParams, bool>> replay)
        {
            List<int> replayed = new List<int>();
            replay((cell, hp) => { replayed.Add(cell.s); return true; });
            return replayed;
        }

        private static void RememberEpisode(QMemory<int, ScalarReward> memory, int first, int length, double reward)
        {
            memory.StartEpisode();
            for (int s = first; s < first + length; s++)
            {
                memory.Remember(s, null, s + 1, null, reward);
            }
            memory.EndEpisode();
        }

        [TestMethod]
        public void DefaultReplayIsUnchanged()
        {
            Assert.AreEqual(860377165599677765L, Fingerprint(Run(new QMemory<int>(20, new QRandom(9)), 20)), "plain");
            Assert.AreEqual(-4936152496574287835L, Fingerprint(Run(new QNegPosMemory<int>(20, new QRandom(9)), 20)), "negpos");
            Assert.AreEqual(1313493081237498698L, Fingerprint(Run(new QEpisodicMemory<int>(8, new QRandom(9)), 2)), "episodic");
            Assert.AreEqual(-2635007222073032903L, Fingerprint(Run(new QEpisodicNegPosMemory<int>(8, new QRandom(9)), 2)), "episodic negpos");
            Assert.AreEqual(-1929120854399329287L, Fingerprint(Run(new QEpisodicNegPosMemory<int>(8, new QRandom(9)), -2)), "episodic negpos, count -2");
        }

        private static QNegPosMemory<int> CrashesAndLandingsThenMildSteps(bool keepExtremes)
        {
            QNegPosMemory<int> memory = new QNegPosMemory<int>(4, new QRandom(1)) { KeepExtremes = keepExtremes };
            memory.Remember(1, null, 2, null, -100.0);
            memory.Remember(2, null, 3, null, 100.0);
            memory.Remember(3, null, 4, null, -100.0);
            memory.Remember(4, null, 5, null, 100.0);
            for (int i = 0; i < 1000; i++)
            {
                memory.Remember(10 + i, null, 11 + i, null, i % 2 == 0 ? -1.0 : 1.0);
            }
            return memory;
        }

        [TestMethod]
        public void KeepExtremesKeepsBothTails()
        {
            QNegPosMemory<int> memory = CrashesAndLandingsThenMildSteps(true);
            memory.NegativeRecallLikelihood = 1.0;
            CollectionAssert.AreEqual(new[] { 3, 1 }, memory.Slice(2).Select(c => c.s).ToList(), "the crashes, newest first");
            memory.NegativeRecallLikelihood = 0.0;
            memory.PositiveRecallLikelihood = 1.0;
            CollectionAssert.AreEqual(new[] { 4, 2 }, memory.Slice(2).Select(c => c.s).ToList(), "the landings, newest first");
        }

        [TestMethod]
        public void ByDefaultTheExtremesAreDropped()
        {
            QNegPosMemory<int> memory = CrashesAndLandingsThenMildSteps(false);
            memory.NegativeRecallLikelihood = 1.0;
            Assert.IsTrue(memory.Slice(4).All(c => c.s >= 10), "the default culling drops the crashes");
            memory.NegativeRecallLikelihood = 0.0;
            memory.PositiveRecallLikelihood = 1.0;
            Assert.IsTrue(memory.Slice(4).All(c => c.s >= 10), "and the landings");
        }

        [TestMethod]
        public void KeepExtremesDropsTheOldestOfEqualEntries()
        {
            QNegPosMemory<int> memory = new QNegPosMemory<int>(3, new QRandom(1)) { KeepExtremes = true };
            for (int id = 1; id <= 5; id++)
            {
                memory.Remember(id, null, id, null, -100.0);
                memory.Remember(10 + id, null, id, null, 100.0);
            }
            memory.NegativeRecallLikelihood = 1.0;
            CollectionAssert.AreEqual(new[] { 5, 4, 3 }, memory.Slice(3).Select(c => c.s).ToList());
            memory.NegativeRecallLikelihood = 0.0;
            memory.PositiveRecallLikelihood = 1.0;
            CollectionAssert.AreEqual(new[] { 15, 14, 13 }, memory.Slice(3).Select(c => c.s).ToList());
        }

        [TestMethod]
        public void TheEpisodicMemoryKeepsTheExtremeEpisodes()
        {
            QEpisodicNegPosMemory<int> memory = new QEpisodicNegPosMemory<int>(2, new QRandom(1)) { KeepExtremes = true };
            RememberEpisode(memory, 100, 2, -50.0);   // return -100
            RememberEpisode(memory, 200, 2, 50.0);    // return +100
            for (int e = 0; e < 20; e++)
            {
                RememberEpisode(memory, 1000 + 10 * e, 2, e % 2 == 0 ? -1.0 : 1.0);
            }
            memory.NegativeRecallLikelihood = 1.0;
            Assert.AreEqual(100, memory.Slice(1)[0].s, "the worst episode is kept");
            memory.NegativeRecallLikelihood = 0.0;
            memory.PositiveRecallLikelihood = 1.0;
            Assert.AreEqual(200, memory.Slice(1)[0].s, "and the best");
        }

        [TestMethod]
        public void KeepExtremesCannotChangeOnceAnythingIsRemembered()
        {
            QNegPosMemory<int> memory = new QNegPosMemory<int>(3, new QRandom(1));
            memory.Remember(1, null, 2, null, -1.0);
            memory.KeepExtremes = false;
            Assert.ThrowsException<InvalidOperationException>(() => memory.KeepExtremes = true);
            memory.Clear();
            memory.KeepExtremes = true;
            Assert.IsTrue(memory.KeepExtremes);

            QEpisodicNegPosMemory<int> episodic = new QEpisodicNegPosMemory<int>(3, new QRandom(1));
            RememberEpisode(episodic, 1, 2, -1.0);
            Assert.ThrowsException<InvalidOperationException>(() => episodic.KeepExtremes = true);
        }

        [TestMethod]
        public void ChooseListPerDrawMixesTheMemoriesWithinACall()
        {
            foreach (bool perDraw in new[] { false, true })
            {
                QNegPosMemory<int> memory = new QNegPosMemory<int>(50, new QRandom(2))
                {
                    ChooseListPerDraw = perDraw,
                    NegativeRecallLikelihood = 0.5,
                    PositiveRecallLikelihood = 0.5,
                };
                for (int i = 0; i < 40; i++)
                {
                    memory.Remember(i, null, i + 1, null, i % 2 == 0 ? -1.0 : 1.0);
                }
                for (int call = 0; call < 5; call++)
                {
                    List<int> replayed = Replay(cb => memory.Playback(100, null, cb));
                    Assert.AreEqual(100, replayed.Count);
                    int signs = replayed.Select(s => s % 2).Distinct().Count();
                    Assert.AreEqual(perDraw ? 2 : 1, signs, perDraw ? "each draw chooses its memory" : "one memory per call");
                }
            }

            QEpisodicNegPosMemory<int> episodic = new QEpisodicNegPosMemory<int>(10, new QRandom(2))
            {
                ChooseListPerDraw = true,
                NegativeRecallLikelihood = 0.5,
                PositiveRecallLikelihood = 0.5,
            };
            for (int e = 0; e < 6; e++)
            {
                RememberEpisode(episodic, 100 * (e + 1), 3, e % 2 == 0 ? -1.0 : 1.0);
            }
            List<int> episodes = Replay(cb => episodic.Playback(20, null, cb)).Select(s => s / 100).ToList();
            Assert.AreEqual(60, episodes.Count, "20 draws of 3-step episodes");
            Assert.IsTrue(episodes.Any(e => e % 2 == 1) && episodes.Any(e => e % 2 == 0), "negative and positive episodes in one call");
        }

        [TestMethod]
        public void ReplayBackwardReversesEpisodes()
        {
            QMemory<int> plain = new QMemory<int>(10, new QRandom(1)) { ReplayBackward = true };
            RememberEpisode(plain, 1, 5, -1.0);
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, Replay(cb => plain.ReplayEpisode(null, cb)));
            plain.ReplayBackward = false;
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, Replay(cb => plain.ReplayEpisode(null, cb)));

            QNegPosMemory<int> negpos = new QNegPosMemory<int>(10, new QRandom(1)) { ReplayBackward = true };
            RememberEpisode(negpos, 1, 5, -1.0);
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, Replay(cb => negpos.ReplayEpisode(null, cb)));

            QEpisodicMemory<int> episodic = new QEpisodicMemory<int>(10, new QRandom(1)) { ReplayBackward = true };
            RememberEpisode(episodic, 1, 5, -1.0);
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, Replay(cb => episodic.ReplayEpisode(null, cb)));
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, Replay(cb => episodic.Playback(1, null, cb)));

            QEpisodicNegPosMemory<int> episodicNegPos = new QEpisodicNegPosMemory<int>(10, new QRandom(1)) { ReplayBackward = true };
            RememberEpisode(episodicNegPos, 1, 5, -1.0);
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, Replay(cb => episodicNegPos.ReplayEpisode(null, cb)));
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, Replay(cb => episodicNegPos.Playback(1, null, cb)));
        }

        /// <summary>
        /// Splits replayed ids into one segment per replayed episode and checks that each one runs through the
        /// episode in replay order, starting at its last step when backward and its first when forward.
        /// </summary>
        private static void AssertWholeEpisodeSegments(List<int> replayed, bool backward, params (int first, int length)[] episodes)
        {
            int i = 0;
            while (i < replayed.Count)
            {
                var episode = episodes.Single(e => replayed[i] >= e.first && replayed[i] < e.first + e.length);
                int start = backward ? episode.first + episode.length - 1 : episode.first;
                Assert.AreEqual(start, replayed[i], "an episode's replay starts at its {0} step", backward ? "last" : "first");
                for (int k = 0; k < episode.length && i < replayed.Count; k++, i++)
                {
                    Assert.AreEqual(backward ? start - k : start + k, replayed[i]);
                }
            }
        }

        [TestMethod]
        public void CountStepsReplaysExactlyTheBudget()
        {
            QEpisodicNegPosMemory<int> negpos = new QEpisodicNegPosMemory<int>(10, new QRandom(3))
            {
                CountSteps = true,
                ReplayBackward = true,
                ChooseListPerDraw = true,
                KeepExtremes = true,
            };
            RememberEpisode(negpos, 100, 4, -1.0);
            RememberEpisode(negpos, 200, 6, 1.0);
            List<int> replayed = Replay(cb => negpos.Playback(25, null, cb));
            Assert.AreEqual(25, replayed.Count);
            AssertWholeEpisodeSegments(replayed, true, (100, 4), (200, 6));

            QEpisodicMemory<int> episodic = new QEpisodicMemory<int>(10, new QRandom(3)) { CountSteps = true };
            RememberEpisode(episodic, 100, 4, -1.0);
            RememberEpisode(episodic, 200, 6, 1.0);
            replayed = Replay(cb => episodic.Playback(17, null, cb));
            Assert.AreEqual(17, replayed.Count);
            AssertWholeEpisodeSegments(replayed, false, (100, 4), (200, 6));
        }

        [TestMethod]
        public void CountStepsEndsWhenThereIsNothingToReplay()
        {
            QEpisodicMemory<int> episodic = new QEpisodicMemory<int>(5, new QRandom(1)) { CountSteps = true };
            Assert.AreEqual(0, Replay(cb => episodic.Playback(10, null, cb)).Count, "no episodes");
            episodic.StartEpisode();
            episodic.EndEpisode();
            episodic.StartEpisode();
            episodic.EndEpisode();
            Assert.AreEqual(0, Replay(cb => episodic.Playback(10, null, cb)).Count, "only empty episodes");

            QEpisodicNegPosMemory<int> negpos = new QEpisodicNegPosMemory<int>(5, new QRandom(1)) { CountSteps = true, ChooseListPerDraw = true };
            Assert.AreEqual(0, Replay(cb => negpos.Playback(10, null, cb)).Count, "no episodes");
            negpos.StartEpisode();
            negpos.EndEpisode();
            Assert.AreEqual(0, Replay(cb => negpos.Playback(10, null, cb)).Count, "only empty episodes");
        }
    }
}
