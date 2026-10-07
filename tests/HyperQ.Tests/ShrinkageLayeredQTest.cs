using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HyperQ.Test
{
    /// <summary>
    /// <see cref="ShrinkageLayeredQ{T}"/> and <see cref="KappaChooser{T}"/>: the blend b_i = (n_i Q_i + κ b_(i-1)) / (n_i + κ)
    /// acts on the layers' values without changing them, κ = 0 is the unscaled layered learner, the chooser scores every
    /// candidate κ by the squared error of its blend against the TD target, and per context it takes the κ that
    /// predicted best.
    /// </summary>
    [TestClass]
    public class ShrinkageLayeredQTest
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

        private static QOrdinalActionSpace Space(QRandom random)
        {
            QOrdinalActionSpace space = new QOrdinalActionSpace(random, 4);
            space.ToIndex(3); // makes the ordinals 0 to 3 known
            return space;
        }

        private static Func<IHyperQ<decimal>> DoubleLayers(QActionSpace<int> space, QRandom random)
        {
            return new DoubleQGenerator<decimal>(space, ran: random).HyperGenerator;
        }

        private static ShrinkageLayeredQ<decimal> Fixed(int seed, double kappa)
        {
            QRandom random = new QRandom(seed);
            QOrdinalActionSpace space = Space(random);
            return new ShrinkageLayeredQ<decimal>(DoubleLayers(space, random), space, kappa);
        }

        private static ShrinkageLayeredQ<decimal> Learned(int seed)
        {
            QRandom random = new QRandom(seed);
            QOrdinalActionSpace space = Space(random);
            return new ShrinkageLayeredQ<decimal>(DoubleLayers(space, random), space, new KappaChooser<decimal>());
        }

        private static QState<decimal> RandomState(Random r)
        {
            return S(r.Next(3), r.Next(3), r.Next(2), r.Next(2), r.Next(3), r.Next(3), r.Next(3));
        }

        [TestMethod]
        public void KappaZeroIsTheUnscaledLearner()
        {
            QRandom r1 = new QRandom(5);
            QOrdinalActionSpace s1 = Space(r1);
            LayeredHyperQ<decimal> unscaled = new LayeredHyperQ<decimal>(DoubleLayers(s1, r1), s1, null, LayerUpdateMode.UnscaledLayerUpdates);
            ShrinkageLayeredQ<decimal> shrink = Fixed(5, 0.0);

            Random data = new Random(11);
            HyperParams hp = new HyperParams(g: 0.9, a: 0.5);
            for (int step = 0; step < 3000; step++)
            {
                QState<decimal> s = RandomState(data);
                int a = data.Next(4);
                QState<decimal> sprime = RandomState(data);
                double reward = data.NextDouble() * 2.0 - 1.0;
                if (step % 2 == 0)
                {
                    Assert.AreEqual(unscaled.OffPolicyUpdate(s, a, sprime, reward, hp, EvalMethodType.Max),
                        shrink.OffPolicyUpdate(s, a, sprime, reward, hp, EvalMethodType.Max), "off-policy update " + step);
                }
                else
                {
                    int aprime = data.Next(4);
                    Assert.AreEqual(unscaled.OnPolicyUpdate(s, a, sprime, aprime, reward, hp),
                        shrink.OnPolicyUpdate(s, a, sprime, aprime, reward, hp), "on-policy update " + step);
                }
                if (step % 100 == 0)
                {
                    for (int p = 0; p < 20; p++)
                    {
                        QState<decimal> probe = RandomState(data);
                        QAction x = unscaled.ArgMax(probe);
                        QAction y = shrink.ArgMax(probe);
                        Assert.AreEqual(x?.Action, y?.Action, "arg max at step " + step);
                        Assert.AreEqual(x?.Reward, y?.Reward, "value at step " + step);
                    }
                }
            }
        }

        /// <summary>
        /// Two fine states under one parent: [.., 1] earns 10 for action 0 and [.., 2] earns 0. The finest layer keeps
        /// them apart, and every coarser layer learns their mix.
        /// </summary>
        private static void TrainTwoChildren(IHyperQ<decimal> q, int visitsToFirst, int visitsToSecond)
        {
            HyperParams hp = new HyperParams(g: 0.0, a: 0.5);
            for (int i = 0; i < Math.Max(visitsToFirst, visitsToSecond); i++)
            {
                if (i < visitsToFirst)
                {
                    q.OffPolicyUpdate(S(1, 1, 1, 1, 1, 1, 1), 0, S(0, 0, 0, 0, 0, 0, 0), 10.0, hp, EvalMethodType.Max);
                }
                if (i < visitsToSecond)
                {
                    q.OffPolicyUpdate(S(1, 1, 1, 1, 1, 1, 2), 0, S(0, 0, 0, 0, 0, 0, 0), 0.0, hp, EvalMethodType.Max);
                }
            }
        }

        [TestMethod]
        public void TheBlendWeighsEachLayerByItsEvidence()
        {
            ShrinkageLayeredQ<decimal> q = Fixed(3, 4.0);
            TrainTwoChildren(q, 3, 12);
            QState<decimal> s = S(1, 1, 1, 1, 1, 1, 1);
            IList<double[]> layers = q.GetLayerActionArrays(s);
            double b = layers[0][0];
            for (int i = 1; i < 7; i++)
            {
                int n = q.Count(s, i + 1, 0);
                Assert.AreEqual(i < 6 ? 15 : 3, n, "evidence at layer " + i);
                b = (n * layers[i][0] + 4.0 * b) / (n + 4.0);
            }
            Assert.AreEqual(b, q.GetValue(s, 0), 1e-12);
            Assert.AreEqual(b, q.GetActionArray(s)[0], 1e-12);
            Assert.IsTrue(q.GetValue(s, 0) < layers[6][0], "the scarce fine evidence is pulled toward the mixed parent");
        }

        [TestMethod]
        public void BlendingChangesTheChoiceButNotTheTables()
        {
            QRandom r1 = new QRandom(4);
            QOrdinalActionSpace s1 = Space(r1);
            LayeredHyperQ<decimal> unscaled = new LayeredHyperQ<decimal>(DoubleLayers(s1, r1), s1, null, LayerUpdateMode.UnscaledLayerUpdates);
            ShrinkageLayeredQ<decimal> shrink = Fixed(4, 4.0);
            HyperParams hp = new HyperParams(g: 0.0, a: 0.5);
            QState<decimal> fresh = S(1, 1, 1, 1, 1, 1, 9);
            foreach (IHyperQ<decimal> q in new IHyperQ<decimal>[] { unscaled, shrink })
            {
                // The parent learns that action 2 pays; then [.., 9] is reached once, after action 0, so the finest
                // layer knows it only through the default it registered for action 0.
                for (int i = 0; i < 30; i++)
                {
                    for (int a = 0; a < 4; a++)
                    {
                        q.OffPolicyUpdate(S(1, 1, 1, 1, 1, 1, i % 5), a, S(0, 0, 0, 0, 0, 0, 0), a == 2 ? 10.0 : 0.0, hp, EvalMethodType.Max);
                    }
                }
                q.OffPolicyUpdate(S(0, 0, 0, 0, 0, 0, 0), 0, fresh, 0.0, hp, EvalMethodType.Max);
            }
            Assert.AreEqual(0, unscaled.ArgMax(fresh).Action, "the unscaled learner repeats the registered action");
            Assert.AreEqual(2, shrink.ArgMax(fresh).Action, "the blend acts on what the parent learned");
            IList<double[]> u = unscaled.GetLayerActionArrays(fresh);
            IList<double[]> v = shrink.GetLayerActionArrays(fresh);
            for (int i = 0; i < 7; i++)
            {
                CollectionAssert.AreEqual(u[i], v[i], "layer " + i + " holds the same values");
            }
        }

        [TestMethod]
        public void TheChooserScoresEachKappaByTheSquaredTdError()
        {
            ShrinkageLayeredQ<decimal> q = Learned(8);
            HyperParams hp = new HyperParams(g: 0.5, a: 0.5);
            // Three updates of ([1, 1], 0) teach both layers. The first finds nothing to score; the others score layer 1
            // at evidence 1 and 2, never 0.
            for (int i = 0; i < 3; i++)
            {
                q.OffPolicyUpdate(S(1, 1), 0, S(1, 1), 10.0, hp, EvalMethodType.Max);
            }
            // The probe reaches the new fine state [1, 3]: its layer-1 evidence is 0, its own value the default 0, and its
            // prior the parent [1]'s value. Its target bootstraps from [1, 1], whose layer-1 blend is its own value: with
            // too few samples anywhere the chooser answers κ = 0.
            IList<double[]> known = q.GetLayerActionArrays(S(1, 1));
            double prior = known[0][0];
            double next = known[1].Max();
            double target = 4.0 + 0.5 * next;
            q.OffPolicyUpdate(S(1, 3), 0, S(1, 1), 4.0, hp, EvalMethodType.Max);

            KappaChoice choice = q.Chooser.Summarize().Single(c => c.Layer == 1 && c.Bucket == 0);
            Assert.AreEqual(1L, choice.Samples);
            for (int k = 0; k < choice.Candidates.Count; k++)
            {
                double kappa = choice.Candidates[k];
                double predicted = kappa > 0.0 ? prior : 0.0;
                Assert.AreEqual((target - predicted) * (target - predicted), choice.MeanSquaredError[k], 1e-9, "κ = " + kappa);
            }
        }

        [TestMethod]
        public void TheChooserTakesTheKappaThatPredictsBestPerContext()
        {
            KappaChooser<decimal> chooser = new KappaChooser<decimal>();
            int candidates = chooser.Candidates.Count;
            double[] parentPredicts = new double[candidates];
            double[] parentMisleads = new double[candidates];
            for (int k = 0; k < candidates; k++)
            {
                parentPredicts[k] = 1.0 / (1.0 + chooser.Candidates[k]);
                parentMisleads[k] = chooser.Candidates[k];
            }
            // Three parents of layer 3.
            QState<decimal> first = S(1, 0, 0), second = S(2, 0, 0), third = S(3, 0, 0);
            for (int i = 0; i < KappaChooser<decimal>.MinSamples - 1; i++)
            {
                chooser.Observe(first, 0, parentPredicts);
            }
            Assert.AreEqual(0.0, chooser.Kappa(first, 0), "too few samples anywhere: no blending");
            chooser.Observe(first, 0, parentPredicts);
            Assert.AreEqual(64.0, chooser.Kappa(first, 0));
            for (int i = 0; i < 3 * KappaChooser<decimal>.MinSamples; i++)
            {
                chooser.Observe(second, 0, parentMisleads);
            }
            Assert.AreEqual(0.0, chooser.Kappa(second, 0));
            Assert.AreEqual(64.0, chooser.Kappa(first, 0), "a parent with enough samples keeps its own choice");
            Assert.AreEqual(0.0, chooser.Kappa(third, 0), "a new parent takes the pooled choice, now dominated by the misleading parent");
            Assert.AreEqual(0.0, chooser.PooledKappa(3, 0));
            Assert.IsTrue(double.IsNaN(chooser.PooledKappa(4, 0)));
            KappaChoice summary = chooser.Summarize().Single();
            Assert.AreEqual(3, summary.Layer);
            Assert.AreEqual("0", summary.Evidence);
            Assert.AreEqual(4L * KappaChooser<decimal>.MinSamples, summary.Samples);
            Assert.AreEqual(1, summary.ParentsChoosing[0], "the misleading parent chose κ = 0");
            Assert.AreEqual(1, summary.ParentsChoosing[candidates - 1], "the predicting parent chose κ = 64");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 3, 3, 4, 4, 5 }, new[] { 0, 1, 2, 3, 4, 7, 8, 15, 16 }.Select(EvidenceBuckets.Of).ToArray());
        }

        [TestMethod]
        public void TheLearnedChooserTrustsAParentWhoseChildrenAgree()
        {
            ShrinkageLayeredQ<decimal> q = Learned(6);
            HyperParams hp = new HyperParams(g: 0.0, a: 0.5);
            // Many children of one parent, each new when first updated, all paying 10 for action 1.
            for (int child = 0; child < 200; child++)
            {
                for (int visit = 0; visit < 3; visit++)
                {
                    q.OffPolicyUpdate(S(2, 2, 1, 1, child % 10, child / 10 % 10, child / 100), 1, S(0, 0, 0, 0, 0, 0, 0), 10.0, hp, EvalMethodType.Max);
                }
            }
            Assert.IsTrue(q.Chooser.PooledKappa(6, 0) > 0.0, "a new child is predicted better by its parent than by the default");
        }

        [TestMethod]
        public void PrefixesOfAnyLengthAndElementValueWork()
        {
            // Prefixes are keyed by themselves, so nothing limits a state's length or its element values.
            QRandom random = new QRandom(10);
            QOrdinalActionSpace space = Space(random);
            ShrinkageLayeredQ<int> q = new ShrinkageLayeredQ<int>(new DoubleQGenerator<int>(space, ran: random).HyperGenerator, space, new KappaChooser<int>());
            QState<int> a = new QState<int>(new[] { 1000, -5, 300, 7, 7, 7, 7, 7, 1 });
            QState<int> b = new QState<int>(new[] { 1000, -5, 300, 7, 7, 7, 7, 7, 2 });
            HyperParams hp = new HyperParams(g: 0.0, a: 0.5);
            for (int i = 0; i < 5; i++)
            {
                q.OffPolicyUpdate(a, 1, b, 10.0, hp, EvalMethodType.Max);
            }
            Assert.AreEqual(5, q.Count(a, 9, 1));
            Assert.AreEqual(5, q.Count(b, 8, 1), "a and b share their first eight elements");
            Assert.AreEqual(0, q.Count(b, 9, 1));
            Assert.AreEqual(1, q.ArgMax(a).Action);
        }

        private static byte[] Save(ICheckpointable learner)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    LearnerCheckpoint.Save(w, learner);
                }
                return ms.ToArray();
            }
        }

        private static void Load(byte[] checkpoint, ICheckpointable learner)
        {
            using (BinaryReader r = new BinaryReader(new MemoryStream(checkpoint), Encoding.UTF8))
            {
                LearnerCheckpoint.Load(r, learner);
            }
        }

        [TestMethod]
        public void ACheckpointRestoresTheEvidenceTheChooserAndTheBlend()
        {
            ShrinkageLayeredQ<decimal> trained = Learned(9);
            Random data = new Random(13);
            HyperParams hp = new HyperParams(g: 0.9, a: 0.5);
            for (int step = 0; step < 4000; step++)
            {
                trained.OffPolicyUpdate(RandomState(data), data.Next(4), RandomState(data), data.NextDouble() * 2.0 - 1.0, hp, EvalMethodType.Max);
            }
            byte[] checkpoint = Save(trained);
            ShrinkageLayeredQ<decimal> restored = Learned(9);
            Load(checkpoint, restored);

            List<KappaChoice> before = trained.Chooser.Summarize().ToList();
            List<KappaChoice> after = restored.Chooser.Summarize().ToList();
            Assert.IsTrue(before.Any(c => c.PooledKappa > 0.0), "the chooser learned to blend somewhere");
            Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].Layer, after[i].Layer);
                Assert.AreEqual(before[i].Bucket, after[i].Bucket);
                Assert.AreEqual(before[i].Samples, after[i].Samples);
                CollectionAssert.AreEqual(before[i].MeanSquaredError.ToArray(), after[i].MeanSquaredError.ToArray());
                CollectionAssert.AreEqual(before[i].ParentsChoosing.ToArray(), after[i].ParentsChoosing.ToArray());
            }
            Random probes = new Random(17);
            for (int p = 0; p < 200; p++)
            {
                QState<decimal> s = RandomState(probes);
                CollectionAssert.AreEqual(trained.GetActionArray(s), restored.GetActionArray(s), "values of probe " + p);
                for (int length = 1; length <= s.Count; length++)
                {
                    Assert.AreEqual(trained.Count(s, length, 2), restored.Count(s, length, 2), "evidence of probe " + p);
                }
            }
            Assert.ThrowsException<InvalidDataException>(() => Load(checkpoint, Fixed(9, 4.0)), "a fixed-κ learner cannot load a learned κ");
            Assert.ThrowsException<InvalidDataException>(() => Load(Save(Fixed(9, 4.0)), Fixed(9, 2.0)), "nor another fixed κ");
        }

        [TestMethod]
        public void ArgumentsAreChecked()
        {
            QRandom random = new QRandom(1);
            QOrdinalActionSpace space = Space(random);
            Func<IHyperQ<decimal>> layers = DoubleLayers(space, random);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ShrinkageLayeredQ<decimal>(layers, space, -1.0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ShrinkageLayeredQ<decimal>(layers, space, double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ShrinkageLayeredQ<decimal>(layers, space, double.PositiveInfinity));
            Assert.ThrowsException<ArgumentNullException>(() => new ShrinkageLayeredQ<decimal>(layers, space, (KappaChooser<decimal>)null));
            Assert.ThrowsException<ArgumentNullException>(() => new ShrinkageLayeredQ<decimal>(null, space, 1.0));
            Assert.ThrowsException<ArgumentNullException>(() => new ShrinkageLayeredQ<decimal>(layers, null, 1.0));
            Assert.ThrowsException<ArgumentException>(() => new KappaChooser<decimal>(new double[0]));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new KappaChooser<decimal>(new[] { -1.0, 2.0 }));
            Assert.ThrowsException<ArgumentException>(() => new KappaChooser<decimal>(new[] { 0.0, 4.0, 2.0 }));
            KappaChooser<decimal> chooser = new KappaChooser<decimal>(new[] { 0.0, 3.0 });
            Assert.ThrowsException<ArgumentException>(() => chooser.Observe(S(1), 0, new[] { 1.0 }));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => chooser.Observe(S(1), EvidenceBuckets.Count, new[] { 1.0, 2.0 }));
            Assert.ThrowsException<ArgumentException>(() => chooser.Observe(S(), 0, new[] { 1.0, 2.0 }));
            Assert.ThrowsException<NotSupportedException>(() => Fixed(1, 1.0).Clone());
        }
    }
}
