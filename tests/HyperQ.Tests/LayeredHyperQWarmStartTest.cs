using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace HyperQ.Test
{
    /// <summary>
    /// The coarse-to-fine warm start of <see cref="LayeredHyperQ{T}"/>: a state that is new to a layer starts at
    /// the values its parent (the next coarser layer's slice of the state) has learned, whole, for the same
    /// actions, whatever the layer type.
    /// </summary>
    [TestClass]
    public class LayeredHyperQWarmStartTest
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

        private static QOrdinalActionSpace OrdinalSpace()
        {
            QOrdinalActionSpace space = new QOrdinalActionSpace(new QRandom(0), 4);
            space.ToIndex(3); // makes the ordinals 0 to 3 known
            return space;
        }

        /// <summary>
        /// A mapped space whose index order is the reverse of the action order, so index i is action 3 - i.
        /// </summary>
        private static StaticMappedActionSpace ReverseMappedSpace()
        {
            StaticMappedActionSpace space = new StaticMappedActionSpace(new QRandom(0), 4);
            for (int a = 3; a >= 0; a--)
            {
                space.ToIndex(a);
            }
            return space;
        }

        private static Func<IHyperQ<decimal>> DoubleLayers(QActionSpace<int> space)
        {
            return new DoubleQGenerator<decimal>(space).HyperGenerator;
        }

        private static Func<IHyperQ<decimal>> SingleLayers(QActionSpace<int> space)
        {
            return new SingleQGenerator<decimal>(space).HyperQGenerator;
        }

        /// <summary>
        /// Trains a three-layer CoarseToFine learner on transitions out of [1, 5, 7] in which action a earns
        /// 10 (a + 1), with gamma 0, so every layer learns 10, 20, 30 and 40 for its prefix. Then adds [1, 6, 99],
        /// whose slices [1, 6] and [1, 6, 99] are new to layers 1 and 2: layer 1 is warm-started from [1] and then
        /// layer 2 from [1, 6].
        /// </summary>
        private static LayeredHyperQ<decimal> TrainAndAddFreshState(Func<QActionSpace<int>, Func<IHyperQ<decimal>>> layers, QActionSpace<int> space, QState<decimal> fresh)
        {
            LayeredHyperQ<decimal> q = new LayeredHyperQ<decimal>(layers(space), space, updateMode: LayerUpdateMode.CoarseToFineLayerUpdates);
            HyperParams hp = new HyperParams(g: 0.0, a: 0.5);
            for (int i = 0; i < 40; i++)
            {
                for (int a = 0; a < 4; a++)
                {
                    q.OffPolicyUpdate(S(1, 5, 7), a, S(1, 5, 8), 10.0 * (a + 1), hp, EvalMethodType.Max);
                }
            }
            q.AddState(fresh);
            return q;
        }

        private static void AssertEveryLayerStartsAtItsParent(string name, Func<QActionSpace<int>, Func<IHyperQ<decimal>>> layers)
        {
            QState<decimal> fresh = S(1, 6, 99);
            LayeredHyperQ<decimal> q = TrainAndAddFreshState(layers, OrdinalSpace(), fresh);
            IList<double[]> values = q.GetLayerActionArrays(fresh);
            for (int a = 0; a < 4; a++)
            {
                Assert.AreEqual(10.0 * (a + 1), values[0][a], 0.01, name + ": layer 0 learned its targets, action " + a);
                Assert.AreEqual(values[0][a], values[1][a], 1e-9, name + ": layer 1 starts at its parent, action " + a);
                Assert.AreEqual(values[1][a], values[2][a], 1e-9, name + ": the finest layer starts at its parent, action " + a);
            }
        }

        [TestMethod]
        public void DoubleQLayersWarmStartEveryLayerWithTheirParentsValues()
        {
            // Before the fix the finest double-Q layer was never warm-started, because the complete state was
            // registered in the state map it shares before it was asked whether the state was new, and layer 1
            // got half of its parent's values, because each value was written to one of its two tables.
            AssertEveryLayerStartsAtItsParent("double-Q layers", DoubleLayers);
        }

        [TestMethod]
        public void SingleQLayersWarmStartEveryLayerWithTheirParentsValues()
        {
            AssertEveryLayerStartsAtItsParent("single-Q layers", SingleLayers);
        }

        [TestMethod]
        public void TheWarmStartKeepsEachValueWithItsActionInAMappedSpace()
        {
            // Before the fix the parent's values were read by action and written by action-space index, which
            // in this space sent the value of action a to action 3 - a. Every layer is checked: the reversal
            // undoes itself after two warm starts, so the finest layer alone would not show it. A single-Q or
            // double-Q layer's action array is ordered by action.
            var cases = new (string Name, Func<QActionSpace<int>, Func<IHyperQ<decimal>>> Layers)[]
            {
                ("single-Q layers", SingleLayers),
                ("double-Q layers", DoubleLayers),
            };
            foreach (var c in cases)
            {
                QState<decimal> fresh = S(1, 6, 99);
                LayeredHyperQ<decimal> q = TrainAndAddFreshState(c.Layers, ReverseMappedSpace(), fresh);
                IList<double[]> values = q.GetLayerActionArrays(fresh);
                for (int layer = 0; layer < 3; layer++)
                {
                    for (int a = 0; a < 4; a++)
                    {
                        Assert.AreEqual(10.0 * (a + 1), values[layer][a], 0.01, c.Name + ", layer " + layer + ", action " + a);
                    }
                }
                Assert.AreEqual(40.0, q.GetValue(fresh, 3), 0.01, c.Name + ": the learner reports the finest layer's value");
            }
        }

        [TestMethod]
        public void UnscaledUpdatesDoNotWarmStart()
        {
            QOrdinalActionSpace space = OrdinalSpace();
            LayeredHyperQ<decimal> q = new LayeredHyperQ<decimal>(DoubleLayers(space), space, updateMode: LayerUpdateMode.UnscaledLayerUpdates);
            HyperParams hp = new HyperParams(g: 0.0, a: 0.5);
            q.OffPolicyUpdate(S(1, 5), 2, S(1, 6), 10.0, hp, EvalMethodType.Max);
            QState<decimal> fresh = S(1, 99);
            q.AddState(fresh);
            Assert.AreNotEqual(0.0, q.GetLayerActionArrays(fresh)[0][2], "the coarse layer learned");
            CollectionAssert.AreEqual(new double[4], q.GetLayerActionArrays(fresh)[1], "the new fine state starts at the default");
        }

        [TestMethod]
        public void DoubleHyperQInitializeValueWritesBothTables()
        {
            DoubleHyperQ<decimal> q = new DoubleHyperQ<decimal>(OrdinalSpace());
            QState<decimal> s = S(3);
            q.InitializeValue(s, 2, 8.0);
            Assert.AreEqual(8.0, q.GetValue(s, 2));
            q.NextQ(0);
            Assert.AreEqual(8.0, q.CurrentQ().GetValue(s, 2), "the first table");
            q.NextQ(1);
            Assert.AreEqual(8.0, q.CurrentQ().GetValue(s, 2), "the second table");
            QAction mx = q.ArgMax(s);
            Assert.AreEqual(2, mx.Action);
            Assert.AreEqual(8.0, mx.Reward);

            // Lowering the best action must reach the cached arg max.
            q.InitializeValue(s, 2, 1.0);
            q.InitializeValue(s, 0, 3.0);
            mx = q.ArgMax(s);
            Assert.AreEqual(0, mx.Action);
            Assert.AreEqual(3.0, mx.Reward);

            // SetValue still writes the current table only, and moves on to the other one.
            q.NextQ(0);
            q.SetValue(s, 0, 5.0);
            Assert.AreEqual(4.0, q.GetValue(s, 0), "0.5 * (5 + 3)");
            q.SetValue(s, 0, 5.0);
            Assert.AreEqual(5.0, q.GetValue(s, 0), "the second call wrote the other table");
        }

        [TestMethod]
        public void LayeredHyperQInitializeValueReadsBackWhole()
        {
            QOrdinalActionSpace space = OrdinalSpace();
            LayeredHyperQ<decimal> q = new LayeredHyperQ<decimal>(DoubleLayers(space), space);
            q.InitializeValue(S(1, 2), 3, 9.0);
            Assert.AreEqual(9.0, q.GetValue(S(1, 2), 3));
            q.SetValue(S(1, 4), 3, 9.0);
            Assert.AreEqual(4.5, q.GetValue(S(1, 4), 3), "SetValue writes one table of a double-Q layer");
        }

        [TestMethod]
        public void GetActionArrayWorksWithAnOrdinalActionSpace()
        {
            // Before the fix this threw a NullReferenceException: only mapped action spaces were handled.
            QOrdinalActionSpace space = OrdinalSpace();
            LayeredHyperQ<decimal> q = new LayeredHyperQ<decimal>(SingleLayers(space), space);
            q.SetValue(S(1, 2), 1, 5.0);
            double[] row = q.GetActionArray(S(1, 2));
            CollectionAssert.AreEqual(new[] { 0.0, 5.0, 0.0, 0.0 }, row);
        }
    }
}
