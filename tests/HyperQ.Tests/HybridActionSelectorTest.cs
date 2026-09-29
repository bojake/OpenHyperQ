using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace HyperQ.Test
{
    /// <summary>
    /// Checks the rule HybridActionSelector uses to pick a component, with stub selectors whose episodes the test
    /// scores.
    /// </summary>
    [TestClass]
    public class HybridActionSelectorTest
    {
        /// <summary>
        /// A selector that only counts how it is used.
        /// </summary>
        private sealed class CountingSelector : IActionSelector<int>
        {
            public int Episodes { get; private set; }
            public int Advantages { get; private set; }
            public bool LastActionWasRandom { get { return false; } }
            public long RandomActionCount { get { return 0; } }
            public MinMaxActionEnum ActionMode { get; set; } = MinMaxActionEnum.Default;
            public Action<int, QAction> TelemetryCallback { get; set; }

            public QAction SelectAction(IArgMinMax<int> q, int state, double epsilon)
            {
                return new QAction(0, 0.0, 0);
            }

            public void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
            {
                Advantages++;
            }

            public void StartEpisode()
            {
                Episodes++;
            }

            public void EndEpisode(RewardAccumulator reward)
            {
            }
        }

        /// <summary>
        /// Runs one episode of the hybrid, scores it with the score of the selector that played it, and returns that
        /// selector.
        /// </summary>
        private static CountingSelector Play(HybridActionSelector<int> hybrid, CountingSelector[] selectors, Func<int, double> score)
        {
            int[] before = selectors.Select(s => s.Episodes).ToArray();
            hybrid.StartEpisode();
            int current = Enumerable.Range(0, selectors.Length).Single(i => selectors[i].Episodes > before[i]);
            hybrid.EndEpisode(new RewardAccumulator().Add(score(current)));
            return selectors[current];
        }

        [TestMethod]
        public void TriesEverySelectorThenKeepsTheBest()
        {
            // The second selector's episodes score higher. The hybrid used to skip selectors with no reward history,
            // so it never left the first one.
            CountingSelector first = new CountingSelector();
            CountingSelector second = new CountingSelector();
            CountingSelector[] selectors = { first, second };
            HybridActionSelector<int> hybrid = new HybridActionSelector<int>(5, first, second);
            for (int episode = 0; episode < 10; episode++)
            {
                Play(hybrid, selectors, i => i == 1 ? 10.0 : -10.0);
            }
            Assert.AreEqual(1, first.Episodes, "the first selector plays the first episode");
            Assert.AreEqual(9, second.Episodes, "the second selector is tried next, then kept for its better average");
        }

        [TestMethod]
        public void TriesUntriedSelectorsInOrderFirst()
        {
            CountingSelector[] selectors = { new CountingSelector(), new CountingSelector(), new CountingSelector() };
            HybridActionSelector<int> hybrid = new HybridActionSelector<int>(5, selectors);
            // The first selector scores best, so after the first round only it plays.
            CountingSelector[] played = Enumerable.Range(0, 5).Select(_ => Play(hybrid, selectors, i => i == 0 ? 1.0 : -1.0)).ToArray();
            CollectionAssert.AreEqual(new[] { selectors[0], selectors[1], selectors[2], selectors[0], selectors[0] }, played);
        }

        [TestMethod]
        public void OnlyTheCurrentSelectorLearns()
        {
            CountingSelector first = new CountingSelector();
            CountingSelector second = new CountingSelector();
            HybridActionSelector<int> hybrid = new HybridActionSelector<int>(5, first, second);
            hybrid.StartEpisode();
            hybrid.ApplyAdvantage(0, new QAction(0, 0.0, 0), 1.0, new HyperParams());
            Assert.AreEqual(1, first.Advantages);
            Assert.AreEqual(0, second.Advantages);
        }
    }
}
