using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace HyperQ.Test
{
    /// <summary>
    /// The trainer hands a policy selector's ApplyAdvantage the learner's index of the state (see
    /// PvESARSATrainer.ApplyAdvantageForStep), so the selector has to look states up the same way when it selects.
    /// </summary>
    [TestClass]
    public class PolicySelectorStateMappingTest
    {
        private static readonly Dictionary<string, Func<IStateMapper<QState<decimal>>, QActionSpace<int>, IActionSelector<QState<decimal>>>> Selectors =
            new Dictionary<string, Func<IStateMapper<QState<decimal>>, QActionSpace<int>, IActionSelector<QState<decimal>>>>
            {
                { "PolicyGradient", (learner, actions) => new PolicyGradientActionSelector<QState<decimal>>(learner, actions) },
                { "GRPOWithKLPenalty", (learner, actions) => new GRPOWithKLPenaltyActionSelector<QState<decimal>>(learner, actions, 0.1) },
                { "PolicyGradientWithSoftmax", (learner, actions) => new PolicyGradientWithSoftmaxActionSelector<QState<decimal>>(learner, actions) },
                { "SoftmaxWithKLPenalty", (learner, actions) => new SoftmaxWithKLPenaltyActionSelector<QState<decimal>>(learner, actions, 0.1) },
            };

        [TestMethod]
        public void PolicySelectorsFindThePolicyLearnedForTheLearnersIndex()
        {
            foreach (KeyValuePair<string, Func<IStateMapper<QState<decimal>>, QActionSpace<int>, IActionSelector<QState<decimal>>>> entry in Selectors)
            {
                QRandom random = new QRandom(11);
                QOrdinalActionSpace actions = new QOrdinalActionSpace(random, 4);
                actions.ToIndex(3); // ordinal actions: mapping action 3 makes actions 0 to 3 known
                DoubleHyperQ<decimal> q = new DoubleHyperQ<decimal>(actions, ran: random);
                // The learner numbers two other states first, so a mapper of the selector's own would give the third
                // state a different number from the learner's.
                q.AddState(new QState<decimal>(new decimal[] { 9M }));
                q.AddState(new QState<decimal>(new decimal[] { 8M }));
                QState<decimal> s = new QState<decimal>(new decimal[] { 1M });
                uint index = q.MapState(s);
                Assert.AreNotEqual(0u, index, "the state must not be the learner's first, or the numberings could agree by accident");

                IActionSelector<QState<decimal>> selector = entry.Value(q, actions);
                HyperParams hp = new HyperParams(a: 0.5);
                for (int i = 0; i < 20; i++)
                {
                    selector.ApplyAdvantage(index, new QAction(2, 0.0, 2), 1.0, hp);
                }
                selector.ActionMode = MinMaxActionEnum.MostProbable;
                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual(2, selector.SelectAction(q, s, 0.0).Action, entry.Key);
                }
            }
        }
    }
}
