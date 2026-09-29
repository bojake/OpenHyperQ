using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperQ.Test
{
    /// <summary>
    /// Evaluation switches a selector to its most probable action through IActionSelector.ActionMode (LEM's EvaluateIt,
    /// for one). The softmax selectors used to read a separate Mode property instead, so they kept sampling.
    /// </summary>
    [TestClass]
    public class SoftmaxSelectorActionModeTest
    {
        private static readonly Dictionary<string, Func<IStateMapper<QState<decimal>>, QActionSpace<int>, IActionSelector<QState<decimal>>>> Selectors =
            new Dictionary<string, Func<IStateMapper<QState<decimal>>, QActionSpace<int>, IActionSelector<QState<decimal>>>>
            {
                { "PolicyGradientWithSoftmax", (learner, actions) => new PolicyGradientWithSoftmaxActionSelector<QState<decimal>>(learner, actions) },
                { "SoftmaxWithKLPenalty", (learner, actions) => new SoftmaxWithKLPenaltyActionSelector<QState<decimal>>(learner, actions, 0.1) },
            };

        [TestMethod]
        public void SoftmaxSelectorsTakeTheMostProbableActionWhenActionModeSaysSo()
        {
            foreach (KeyValuePair<string, Func<IStateMapper<QState<decimal>>, QActionSpace<int>, IActionSelector<QState<decimal>>>> entry in Selectors)
            {
                QRandom random = new QRandom(5);
                QOrdinalActionSpace actions = new QOrdinalActionSpace(random, 4);
                actions.ToIndex(3); // ordinal actions: mapping action 3 makes actions 0 to 3 known
                DoubleHyperQ<decimal> q = new DoubleHyperQ<decimal>(actions, ran: random);
                QState<decimal> s = new QState<decimal>(new decimal[] { 1M });
                uint index = q.MapState(s);
                IActionSelector<QState<decimal>> selector = entry.Value(q, actions);
                HyperParams hp = new HyperParams(a: 0.5);
                // A few steps toward action 2 make it the most probable action while the policy stays far from
                // deterministic, so sampling still picks the other actions often.
                for (int i = 0; i < 5; i++)
                {
                    selector.ApplyAdvantage(index, new QAction(2, 0.0, 2), 1.0, hp);
                }

                selector.ActionMode = MinMaxActionEnum.Default;
                Assert.IsTrue(Enumerable.Range(0, 200).Any(_ => selector.SelectAction(q, s, 0.0).Action != 2),
                    entry.Key + ": sampling should still pick the other actions");

                selector.ActionMode = MinMaxActionEnum.MostProbable;
                for (int i = 0; i < 200; i++)
                {
                    Assert.AreEqual(2, selector.SelectAction(q, s, 0.0).Action, entry.Key + ": call " + i);
                }
            }
        }
    }
}
