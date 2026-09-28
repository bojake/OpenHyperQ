using HyperQ.Learners;
using HyperQ.MACE;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HyperQ.Test
{
    /// <summary>
    /// Every learner round-trips through its checkpoint into a freshly constructed instance with a fresh action
    /// space, and comes back with the same values, arg max and shape. Mapped action spaces number their actions
    /// in the order they were seen, so the tests deliberately visit actions out of order.
    /// </summary>
    [TestClass]
    public class LearnerCheckpointTests
    {
        private static readonly int[] Actions = { 3, 0, 2, 1 };

        private static QState<decimal> S(params int[] v)
        {
            QState<decimal> q = new QState<decimal>();
            foreach (int i in v) q.Push(i);
            return q;
        }

        private static byte[] Save(ICheckpointable c)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    LearnerCheckpoint.Save(w, c);
                }
                return ms.ToArray();
            }
        }

        private static void Load(byte[] data, ICheckpointable c)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader r = new BinaryReader(ms, Encoding.UTF8))
            {
                LearnerCheckpoint.Load(r, c);
            }
        }

        private static void Fill<T>(Q<T> q, IList<T> states)
        {
            double v = 1.0;
            foreach (T s in states)
            {
                foreach (int a in Actions)
                {
                    q.SetValue(s, a, v);
                    v += 1.5;
                }
            }
        }

        private static void AssertSame<T>(Q<T> expected, Q<T> actual, IList<T> states)
        {
            Assert.AreEqual(expected.Shape, actual.Shape, "Shape");
            foreach (T s in states)
            {
                foreach (int a in Actions)
                {
                    Assert.AreEqual(expected.GetValue(s, a), actual.GetValue(s, a), 1e-12, "Q(" + s + "," + a + ")");
                }
                QAction e = expected.ArgMax(s);
                QAction r = actual.ArgMax(s);
                Assert.AreEqual(e.Item1, r.Item1, "ArgMax action of " + s);
                Assert.AreEqual(e.Item2, r.Item2, 1e-12, "ArgMax value of " + s);
            }
        }

        private static readonly decimal[] FlatStates = { 7M, 3M, 11M, 2M };
        private static readonly QState<decimal>[] HyperStates = { S(1, 2), S(1, 3), S(0, 2), S(2, 2) };

        [TestMethod]
        public void MappedQ_RoundTrip_WithMappedActionSpace()
        {
            QRandom ran = new QRandom(3);
            MappedQ<decimal> q = new MappedQ<decimal>(new StaticMappedActionSpace(ran, 4));
            Fill(q, FlatStates);
            byte[] data = Save(q);

            MappedQ<decimal> restored = new MappedQ<decimal>(new StaticMappedActionSpace(ran, 4));
            Load(data, restored);
            AssertSame(q, restored, FlatStates);
            Assert.AreEqual(q.ActionSpace.NumberOfKnownActions, restored.ActionSpace.NumberOfKnownActions, "known actions");
            // The restored space numbers the actions as the original did
            foreach (int a in Actions)
                Assert.AreEqual(q.ActionSpace.ToIndex(a), restored.ActionSpace.ToIndex(a), "index of action " + a);
        }

        [TestMethod]
        public void MappedQ_RoundTrip_ContinuesLearning()
        {
            QRandom ran = new QRandom(4);
            MappedQ<decimal> q = new MappedQ<decimal>(new StaticMappedActionSpace(ran, 4));
            Fill(q, FlatStates);
            MappedQ<decimal> restored = new MappedQ<decimal>(new StaticMappedActionSpace(ran, 4));
            Load(Save(q), restored);
            // New states after a restore must get indices that do not collide with restored ones
            uint before = restored.Shape.Item1;
            restored.SetValue(99M, 1, 42.0);
            Assert.AreEqual(before + 1, restored.Shape.Item1, "one new state");
            Assert.AreEqual(42.0, restored.GetValue(99M, 1), 1e-12);
            foreach (decimal s in FlatStates)
                Assert.AreEqual(q.GetValue(s, 1), restored.GetValue(s, 1), 1e-12, "old state " + s + " kept its values");
        }

        [TestMethod]
        public void MappedQQ_RoundTrip()
        {
            QRandom ran = new QRandom(5);
            MappedQQ<decimal> q = new MappedQQ<decimal>(new StaticMappedActionSpace(ran, 4), ran: ran);
            Fill(q, FlatStates);
            MappedQQ<decimal> restored = new MappedQQ<decimal>(new StaticMappedActionSpace(ran, 4), ran: ran);
            Load(Save(q), restored);
            AssertSame(q, restored, FlatStates);
        }

        [TestMethod]
        public void ClassicQQ_RoundTrip()
        {
            QRandom ran = new QRandom(6);
            ClassicQQ q = new ClassicQQ(new QOrdinalActionSpace(ran, 4));
            Fill(q, FlatStates);
            ClassicQQ restored = new ClassicQQ(new QOrdinalActionSpace(ran, 4));
            Load(Save(q), restored);
            AssertSame(q, restored, FlatStates);
        }

        [TestMethod]
        public void ClassicQ_RoundTrip_RestoresOrdinalKnownActions()
        {
            QRandom ran = new QRandom(7);
            QOrdinalActionSpace space = new QOrdinalActionSpace(ran, 6);
            ClassicQ q = new ClassicQ(space);
            q.SetValue(1M, 4, 2.0); // ToIndex marks action 4 known, so 0..4 are known
            QOrdinalActionSpace space2 = new QOrdinalActionSpace(ran, 6);
            ClassicQ restored = new ClassicQ(space2);
            Load(Save(q), restored);
            Assert.AreEqual(space.NumberOfKnownActions, space2.NumberOfKnownActions);
            Assert.AreEqual(2.0, restored.GetValue(1M, 4), 1e-12);
        }

        [TestMethod]
        public void SingleHyperQ_RoundTrip()
        {
            QRandom ran = new QRandom(8);
            SingleHyperQ<decimal> q = new SingleHyperQ<decimal>(new QOrdinalActionSpace(ran, 4));
            Fill(q, HyperStates);
            SingleHyperQ<decimal> restored = new SingleHyperQ<decimal>(new QOrdinalActionSpace(ran, 4));
            Load(Save(q), restored);
            AssertSame(q, restored, HyperStates);
            Assert.IsTrue(restored.IsKnownState(S(1, 2)));
            Assert.IsFalse(restored.IsKnownState(S(5, 5)));
        }

        [TestMethod]
        public void DoubleHyperQ_RoundTrip()
        {
            QRandom ran = new QRandom(9);
            DoubleHyperQ<decimal> q = new DoubleHyperQ<decimal>(new QOrdinalActionSpace(ran, 4), ran: ran);
            Fill(q, HyperStates);
            DoubleHyperQ<decimal> restored = new DoubleHyperQ<decimal>(new QOrdinalActionSpace(ran, 4), ran: ran);
            Load(Save(q), restored);
            AssertSame(q, restored, HyperStates);
        }

        [TestMethod]
        public void LayeredHyperQ_RoundTrip_SingleLayers()
        {
            QRandom ran = new QRandom(10);
            QOrdinalActionSpace space = new QOrdinalActionSpace(ran, 4);
            LayeredHyperQ<decimal> q = new LayeredHyperQ<decimal>(() => new SingleHyperQ<decimal>(space), space);
            Fill(q, HyperStates);
            QOrdinalActionSpace space2 = new QOrdinalActionSpace(ran, 4);
            LayeredHyperQ<decimal> restored = new LayeredHyperQ<decimal>(() => new SingleHyperQ<decimal>(space2), space2);
            Load(Save(q), restored);
            AssertSame(q, restored, HyperStates);
            CollectionAssert.AreEquivalent(q.KnownStates(), restored.KnownStates(), "known complete states");
        }

        [TestMethod]
        public void LayeredHyperQ_RoundTrip_DoubleLayers()
        {
            QRandom ran = new QRandom(11);
            QOrdinalActionSpace space = new QOrdinalActionSpace(ran, 4);
            LayeredHyperQ<decimal> q = new LayeredHyperQ<decimal>(() => new DoubleHyperQ<decimal>(space, ran: ran), space);
            Fill(q, HyperStates);
            QOrdinalActionSpace space2 = new QOrdinalActionSpace(ran, 4);
            LayeredHyperQ<decimal> restored = new LayeredHyperQ<decimal>(() => new DoubleHyperQ<decimal>(space2, ran: ran), space2);
            Load(Save(q), restored);
            AssertSame(q, restored, HyperStates);
        }

        [TestMethod]
        public void MACEMinds_RoundTrip_ThroughGzipFile()
        {
            QRandom ran = new QRandom(12);
            StaticMappedActionSpace space = new StaticMappedActionSpace(ran, 4);
            MappedQ<decimal> q = new MappedQ<decimal>(space);
            PolicyGradientActionSelector<decimal> selector = new PolicyGradientActionSelector<decimal>(q, space);
            Fill(q, FlatStates);
            HyperParams hp = new HyperParams(0.99, 0.1, 0.5);
            selector.SelectAction(q, 7M, 0.0);
            selector.ApplyAdvantage(q.MapState(7M), new QAction(3, 1.0, space.ToIndex(3)), 2.0, hp);
            MACEMind<decimal>[] minds = { new MACEMind<decimal>(q, selector), new MACEMind<decimal>(new ClassicQ(new QOrdinalActionSpace(ran, 4)), new eGreedyActionSelector<decimal>(new QOrdinalActionSpace(ran, 4))) };
            minds[1].Mind.SetValue(5M, 2, 9.0);

            string path = Path.Combine(Path.GetTempPath(), "hyperq_minds_" + Guid.NewGuid().ToString("N") + ".gz");
            try
            {
                MACECheckpoint.SaveFile(path, minds);

                StaticMappedActionSpace space2 = new StaticMappedActionSpace(ran, 4);
                MappedQ<decimal> q2 = new MappedQ<decimal>(space2);
                PolicyGradientActionSelector<decimal> selector2 = new PolicyGradientActionSelector<decimal>(q2, space2);
                MACEMind<decimal>[] restored = { new MACEMind<decimal>(q2, selector2), new MACEMind<decimal>(new ClassicQ(new QOrdinalActionSpace(ran, 4)), new eGreedyActionSelector<decimal>(new QOrdinalActionSpace(ran, 4))) };
                MACECheckpoint.LoadFile(path, restored);

                AssertSame(q, q2, FlatStates);
                Assert.AreEqual(9.0, restored[1].Mind.GetValue(5M, 2), 1e-12);
                Assert.AreEqual(selector.RandomActionCount, selector2.RandomActionCount, "selector state restored");

                // A different mind count is rejected
                Assert.ThrowsException<InvalidDataException>(() => MACECheckpoint.LoadFile(path, new[] { restored[0] }));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void LearnerCheckpoint_File_UncompressedAndCompressed()
        {
            QRandom ran = new QRandom(13);
            ClassicQ q = new ClassicQ(new QOrdinalActionSpace(ran, 4));
            Fill(q, FlatStates);
            foreach (string ext in new[] { ".hqc", ".hqc.gz" })
            {
                string path = Path.Combine(Path.GetTempPath(), "hyperq_learner_" + Guid.NewGuid().ToString("N") + ext);
                try
                {
                    LearnerCheckpoint.SaveFile(path, q);
                    ClassicQ restored = new ClassicQ(new QOrdinalActionSpace(ran, 4));
                    LearnerCheckpoint.LoadFile(path, restored);
                    AssertSame(q, restored, FlatStates);
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                }
            }
        }
    }
}
