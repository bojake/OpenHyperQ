using HyperQ.Learners;
using HyperQ.MultiHead;
using HyperQ.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace HyperQ.Test
{
    /// <summary>
    /// QState equality: equal elements in the same order. It used to compare hash codes that XOR-ed the elements'
    /// hashes, so [1, 2] equaled [2, 1] and [3, 3] equaled [5, 5], and every dictionary keyed by states merged them.
    /// </summary>
    [TestClass]
    public class QStateEqualityTest
    {
        private static QState<decimal> S(params int[] v)
        {
            QState<decimal> q = new QState<decimal>();
            foreach (int x in v)
            {
                q.Push(x);
            }
            return q;
        }

        [TestMethod]
        public void StatesAreEqualWhenTheirElementsAreEqualInOrder()
        {
            Assert.AreEqual(S(1, 2), S(1, 2));
            Assert.AreEqual(S(1, 2).GetHashCode(), S(1, 2).GetHashCode());
            Assert.AreNotEqual(S(1, 2), S(2, 1), "order matters");
            Assert.AreNotEqual(S(3, 3), S(5, 5));
            Assert.AreNotEqual(S(3, 3), S(), "x ^ x used to cancel out");
            Assert.AreNotEqual(S(0, 0, 7), S(7));
            Assert.AreNotEqual(S(1), S(1, 0));
            Assert.IsFalse(S(1).Equals((QState<decimal>)null));
            Assert.IsFalse(S(1).Equals((object)null), "this used to throw");
            Assert.IsFalse(S(1).Equals("1"));

            QState<decimal>.EqualityComparer comparer = new QState<decimal>.EqualityComparer();
            Assert.IsTrue(comparer.Equals(S(4, 2), S(4, 2)), "the nested comparer compares values, not references");
            Assert.IsFalse(comparer.Equals(S(4, 2), S(2, 4)));
            Assert.IsTrue(comparer.Equals(null, null));
            Assert.IsFalse(comparer.Equals(S(4), null));
        }

        [TestMethod]
        public void ADictionaryKeepsApartTheStatesTheOldEqualityMerged()
        {
            QState<decimal>[] states = { S(1, 2), S(2, 1), S(3, 3), S(5, 5), S(), S(1, 2, 3), S(3, 2, 1), S(0, 0, 7), S(7) };
            Dictionary<QState<decimal>, int> index = new Dictionary<QState<decimal>, int>();
            for (int i = 0; i < states.Length; i++)
            {
                index[states[i]] = i;
            }
            Assert.AreEqual(states.Length, index.Count);
            for (int i = 0; i < states.Length; i++)
            {
                Assert.AreEqual(i, index[states[i].Clone()], "an equal state finds its entry");
            }
        }

        [TestMethod]
        public void DynaKeepsApartTheModelsOfStatesTheOldEqualityMerged()
        {
            DynaState<QState<decimal>, ScalarReward> dyna = new DynaState<QState<decimal>, ScalarReward>(100, 10);
            HyperParams hp = new HyperParams(a: 1.0); // the reward model then holds the latest reward
            QAction a = new QAction(0, 0.0, 0);
            dyna.Update(S(1, 2), a, S(5), 1.0, hp);
            dyna.Update(S(2, 1), a, S(6), -1.0, hp);
            Tuple<QState<decimal>, ScalarReward> x = dyna.Sample(new DynaEntry<QState<decimal>>(S(1, 2), a));
            Tuple<QState<decimal>, ScalarReward> y = dyna.Sample(new DynaEntry<QState<decimal>>(S(2, 1), a));
            Assert.AreEqual(S(5), x.Item1);
            Assert.AreEqual(1.0, (double)x.Item2, 1e-12);
            Assert.AreEqual(S(6), y.Item1);
            Assert.AreEqual(-1.0, (double)y.Item2, 1e-12);
        }

        [TestMethod]
        public void MultiHeadArgMaxKeepsApartTheStatesTheOldEqualityMerged()
        {
            QOrdinalActionSpace space = new QOrdinalActionSpace(new QRandom(0), 2);
            space.ToIndex(1); // makes the ordinals 0 and 1 known
            SingleHyperQ<decimal> head = new SingleHyperQ<decimal>(space);
            head.SetValue(S(1, 2), 0, 5.0);
            head.SetValue(S(1, 2), 1, 1.0);
            head.SetValue(S(2, 1), 0, 1.0);
            head.SetValue(S(2, 1), 1, 5.0);
            MultiHeadHyperQLearner<decimal> q = new MultiHeadHyperQLearner<decimal>(new IHyperQ<decimal>[] { head });
            Assert.AreEqual(0, q.ArgMax(S(1, 2)).Action);
            Assert.AreEqual(1, q.ArgMax(S(2, 1)).Action, "the arg max cached for [1, 2] used to answer for [2, 1]");
        }
    }
}
