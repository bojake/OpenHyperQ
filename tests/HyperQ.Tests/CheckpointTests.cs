using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace HyperQ.Test
{
    [TestClass]
    public class CheckpointTests
    {
        // ── QParam Round-Trip ──

        [TestMethod]
        public void QParam_RoundTrip()
        {
            var original = new QParamExponential(0.5, 0.999, 0.01);
            // Simulate some decay
            for (int i = 0; i < 100; i++) original.Decay();
            double decayedValue = original.Value;

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    original.SaveCheckpoint(bw);
                }

                ms.Position = 0;
                var restored = new QParamExponential(0.0); // dummy
                using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    restored.LoadCheckpoint(br);
                }

                Assert.AreEqual(decayedValue, restored.Value, 1e-15, "Current decayed value should be restored");
                Assert.AreEqual(0.5, restored.InitialValue, 1e-15, "InitialValue should be restored");
                Assert.AreEqual(0.999, restored.DecayRate, 1e-15, "DecayRate should be restored");
                Assert.AreEqual(0.01, restored.MinValue, 1e-15, "MinValue should be restored");
            }
        }

        // ── HyperParams Round-Trip ──

        [TestMethod]
        public void HyperParams_RoundTrip()
        {
            var hp = new HyperParams(g: 0.99, e: 0.3, a: 0.1, lambda: 0.95);
            // Decay to move away from initial values
            for (int i = 0; i < 50; i++) hp.Decay();

            double gamma = hp.Gamma;
            double epsilon = hp.Epsilon;
            double alpha = hp.Alpha;
            double lambda = (double)hp.Lambda;

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    hp.SaveCheckpoint(bw);
                }

                ms.Position = 0;
                var restored = new HyperParams(); // defaults
                using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    restored.LoadCheckpoint(br);
                }

                Assert.AreEqual(gamma, restored.Gamma, 1e-15, "Gamma should be restored at decay position");
                Assert.AreEqual(epsilon, restored.Epsilon, 1e-15, "Epsilon should be restored at decay position");
                Assert.AreEqual(alpha, restored.Alpha, 1e-15, "Alpha should be restored at decay position");
                Assert.AreEqual(lambda, (double)restored.Lambda, 1e-15, "Lambda should be restored at decay position");
            }
        }

        // ── ClassicQ Round-Trip ──

        [TestMethod]
        public void ClassicQ_RoundTrip()
        {
            QRandom ran = new QRandom(0);
            QActionSpace<int> qas = new QOrdinalActionSpace(ran, 4);
            ClassicQ q = new ClassicQ(qas);

            // Populate with known values
            q.SetValue(10M, 0, 5.0);
            q.SetValue(10M, 1, 7.0);
            q.SetValue(10M, 2, 3.0);
            q.SetValue(10M, 3, 9.0);
            q.SetValue(20M, 0, 11.0);
            q.SetValue(20M, 1, 13.0);
            q.SetValue(7M, 2, 21.0);

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    q.SaveCheckpoint(bw);
                }

                ms.Position = 0;
                ClassicQ restored = new ClassicQ(qas);
                using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    restored.LoadCheckpoint(br);
                }

                // Verify all values round-tripped
                Assert.AreEqual(5.0, restored.GetValue(10M, 0), 1e-15);
                Assert.AreEqual(7.0, restored.GetValue(10M, 1), 1e-15);
                Assert.AreEqual(3.0, restored.GetValue(10M, 2), 1e-15);
                Assert.AreEqual(9.0, restored.GetValue(10M, 3), 1e-15);
                Assert.AreEqual(11.0, restored.GetValue(20M, 0), 1e-15);
                Assert.AreEqual(13.0, restored.GetValue(20M, 1), 1e-15);
                Assert.AreEqual(21.0, restored.GetValue(7M, 2), 1e-15);

                // Verify ArgMax still works
                QAction argmax = restored.ArgMax(10M);
                Assert.AreEqual(3, argmax.Item1, "ArgMax action for state 10 should be 3");
                Assert.AreEqual(9.0, argmax.Item2, "ArgMax value for state 10 should be 9.0");
            }
        }

        [TestMethod]
        public void ClassicQ_EmptyRoundTrip()
        {
            QRandom ran = new QRandom(0);
            QActionSpace<int> qas = new QOrdinalActionSpace(ran, 4);
            ClassicQ q = new ClassicQ(qas);

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    q.SaveCheckpoint(bw);
                }

                ms.Position = 0;
                ClassicQ restored = new ClassicQ(qas);
                using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    restored.LoadCheckpoint(br);
                }

                Assert.AreEqual(0u, restored.Shape.Item1, "Empty Q should have 0 states");
            }
        }

        // ── RunningLogit Round-Trip ──

        [TestMethod]
        public void RunningLogit_RoundTrip()
        {
            var logit = new RunningLogit(4);
            logit[0] = 1.0;
            logit[1] = 2.5;
            logit[2] = -0.5;
            logit[3] = 3.0;

            double prob0 = logit.Probability(0);
            double prob1 = logit.Probability(1);
            double prob2 = logit.Probability(2);
            double prob3 = logit.Probability(3);

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    logit.SaveCheckpoint(bw);
                }

                ms.Position = 0;
                var restored = new RunningLogit();
                using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    restored.LoadCheckpoint(br);
                }

                Assert.AreEqual(prob0, restored.Probability(0), 1e-10, "Probability[0] should match");
                Assert.AreEqual(prob1, restored.Probability(1), 1e-10, "Probability[1] should match");
                Assert.AreEqual(prob2, restored.Probability(2), 1e-10, "Probability[2] should match");
                Assert.AreEqual(prob3, restored.Probability(3), 1e-10, "Probability[3] should match");
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(logit[i], restored[i], $"Logit[{i}] should match");
            }
        }

        // ── PolicyGradientActionSelector Round-Trip ──

        [TestMethod]
        public void PolicyGradientSelector_RoundTrip()
        {
            QRandom ran = new QRandom(0);
            QActionSpace<int> qas = new QOrdinalActionSpace(ran, 4);
            ClassicQ q = new ClassicQ(qas);

            // Set up some Q-values
            q.SetValue(1M, 0, 5.0);
            q.SetValue(1M, 1, 3.0);
            q.SetValue(2M, 0, 7.0);

            var selector = new PolicyGradientActionSelector<decimal>(q, qas);
            var hp = new HyperParams(g: 0.99, e: 0.3, a: 0.1);

            // Run some selections to build up the policy
            for (int i = 0; i < 20; i++)
            {
                selector.SelectAction(q, 1M, hp.Epsilon);
                selector.ApplyAdvantage(q.MapState(1M), new QAction(0, 5.0, 0), 2.0, hp);
                selector.SelectAction(q, 2M, hp.Epsilon);
                selector.ApplyAdvantage(q.MapState(2M), new QAction(0, 7.0, 0), 1.0, hp);
            }
            long randomCount = selector.RandomActionCount;

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    selector.SaveCheckpoint(bw);
                }

                ms.Position = 0;
                var restored = new PolicyGradientActionSelector<decimal>(q, qas);
                using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    restored.LoadCheckpoint(br);
                }

                Assert.AreEqual(randomCount, restored.RandomActionCount, "Random action count should be restored");
            }
        }

        // ── TrainingCheckpoint Integration ──

        [TestMethod]
        public void TrainingCheckpoint_SaveLoad_Integration()
        {
            QRandom ran = new QRandom(0);
            QActionSpace<int> qas = new QOrdinalActionSpace(ran, 4);
            ClassicQ q = new ClassicQ(qas);

            q.SetValue(1M, 0, 5.0);
            q.SetValue(1M, 1, 7.0);
            q.SetValue(2M, 2, 9.0);

            var hp = new HyperParams(g: 0.99, e: 0.3, a: 0.1, lambda: 0.95);
            for (int i = 0; i < 100; i++) hp.Decay();

            double savedGamma = hp.Gamma;
            double savedEpsilon = hp.Epsilon;

            string path = Path.Combine(Path.GetTempPath(), $"hyperq_test_{Guid.NewGuid()}.hqc");
            try
            {
                TrainingCheckpoint.Save(
                    path, q, hp,
                    episodeCount: 500,
                    elapsedMs: 12345,
                    sweepMode: DynaSweepMode.Prioritized);

                Assert.IsTrue(File.Exists(path), "Checkpoint file should exist");
                Assert.IsTrue(new FileInfo(path).Length > 0, "Checkpoint file should not be empty");

                // Create fresh instances to load into
                ClassicQ restoredQ = new ClassicQ(qas);
                var restoredHp = new HyperParams();

                var meta = TrainingCheckpoint.Load(path, restoredQ, restoredHp);

                // Metadata
                Assert.AreEqual(500, meta.EpisodeCount, "Episode count should be restored");
                Assert.AreEqual(12345L, meta.ElapsedMilliseconds, "Elapsed time should be restored");
                Assert.AreEqual(DynaSweepMode.Prioritized, meta.SweepMode, "SweepMode should be restored");

                // HyperParams
                Assert.AreEqual(savedGamma, restoredHp.Gamma, 1e-15, "Gamma should be at saved decay position");
                Assert.AreEqual(savedEpsilon, restoredHp.Epsilon, 1e-15, "Epsilon should be at saved decay position");

                // Q-Table
                Assert.AreEqual(5.0, restoredQ.GetValue(1M, 0), 1e-15, "Q(1,0) should be restored");
                Assert.AreEqual(7.0, restoredQ.GetValue(1M, 1), 1e-15, "Q(1,1) should be restored");
                Assert.AreEqual(9.0, restoredQ.GetValue(2M, 2), 1e-15, "Q(2,2) should be restored");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void TrainingCheckpoint_LoadsVersion1InlineSelector()
        {
            // Version 1 files stored the selector state inline, without a length prefix. They must still load
            // when the selector is supplied, and load without it (the selector is the last section).
            QRandom ran = new QRandom(0);
            QActionSpace<int> qas = new QOrdinalActionSpace(ran, 4);
            ClassicQ q = new ClassicQ(qas);
            q.SetValue(1M, 0, 5.0);
            var hp = new HyperParams(g: 0.99, e: 0.3, a: 0.1);
            var selector = new PolicyGradientActionSelector<decimal>(q, qas);
            selector.SelectAction(q, 1M, hp.Epsilon);
            selector.ApplyAdvantage(q.MapState(1M), new QAction(0, 5.0, 0), 2.0, hp);
            long savedCount = selector.RandomActionCount;

            string path = Path.Combine(Path.GetTempPath(), $"hyperq_v1_{Guid.NewGuid()}.hqc");
            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(1);      // FORMAT_VERSION 1
                    writer.Write(42);     // episodeCount
                    writer.Write(1000L);  // elapsedMs
                    writer.Write(0);      // reserved slot (the advantage mode of the first format releases)
                    writer.Write((int)DynaSweepMode.Uniform);
                    hp.SaveCheckpoint(writer);
                    q.SaveCheckpoint(writer);
                    writer.Write(true);
                    selector.SaveCheckpoint(writer); // inline, no length prefix
                }

                ClassicQ restoredQ = new ClassicQ(qas);
                var restoredSel = new PolicyGradientActionSelector<decimal>(restoredQ, qas);
                var meta = TrainingCheckpoint.Load(path, restoredQ, new HyperParams(), selector: restoredSel);
                Assert.AreEqual(1, meta.FormatVersion, "The file's own version is reported");
                Assert.AreEqual(42, meta.EpisodeCount);
                Assert.AreEqual(5.0, restoredQ.GetValue(1M, 0), 1e-15);
                Assert.AreEqual(savedCount, restoredSel.RandomActionCount, "Inline selector state should be restored");

                // Without a selector the inline payload is simply left unread.
                var meta2 = TrainingCheckpoint.Load(path, new ClassicQ(qas), new HyperParams());
                Assert.AreEqual(1, meta2.FormatVersion);
                Assert.AreEqual(42, meta2.EpisodeCount);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void TrainingCheckpoint_SaveLoadWithSelector()
        {
            QRandom ran = new QRandom(0);
            QActionSpace<int> qas = new QOrdinalActionSpace(ran, 4);
            ClassicQ q = new ClassicQ(qas);
            q.SetValue(1M, 0, 5.0);

            var hp = new HyperParams(g: 0.99, e: 0.5, a: 0.1);
            var selector = new PolicyGradientActionSelector<decimal>(q, qas);

            // Build policy state
            for (int i = 0; i < 10; i++)
            {
                selector.SelectAction(q, 1M, hp.Epsilon);
                selector.ApplyAdvantage(q.MapState(1M), new QAction(0, 5.0, 0), 2.0, hp);
            }
            long savedCount = selector.RandomActionCount;

            string path = Path.Combine(Path.GetTempPath(), $"hyperq_sel_{Guid.NewGuid()}.hqc");
            try
            {
                TrainingCheckpoint.Save(path, q, hp, 100, selector: selector);

                ClassicQ restoredQ = new ClassicQ(qas);
                var restoredHp = new HyperParams();
                var restoredSel = new PolicyGradientActionSelector<decimal>(restoredQ, qas);

                var meta = TrainingCheckpoint.Load(path, restoredQ, restoredHp, selector: restoredSel);

                Assert.AreEqual(100, meta.EpisodeCount);
                Assert.AreEqual(savedCount, restoredSel.RandomActionCount);
                Assert.AreEqual(5.0, restoredQ.GetValue(1M, 0), 1e-15);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void TrainingCheckpoint_CanSkipSelectorPayload()
        {
            QRandom ran = new QRandom(0);
            QActionSpace<int> qas = new QOrdinalActionSpace(ran, 4);
            ClassicQ q = new ClassicQ(qas);
            q.SetValue(1M, 0, 5.0);

            var hp = new HyperParams(g: 0.99, e: 0.5, a: 0.1);
            var selector = new PolicyGradientActionSelector<decimal>(q, qas);
            selector.SelectAction(q, 1M, hp.Epsilon);

            string path = Path.Combine(Path.GetTempPath(), $"hyperq_skip_sel_{Guid.NewGuid()}.hqc");
            try
            {
                TrainingCheckpoint.Save(path, q, hp, 100, selector: selector);

                ClassicQ restoredQ = new ClassicQ(qas);
                var restoredHp = new HyperParams();
                var meta = TrainingCheckpoint.Load(path, restoredQ, restoredHp);

                Assert.AreEqual(100, meta.EpisodeCount);
                Assert.AreEqual(5.0, restoredQ.GetValue(1M, 0), 1e-15);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void TrainingCheckpoint_VersionCheck()
        {
            // Write a checkpoint with a fake future version
            string path = Path.Combine(Path.GetTempPath(), $"hyperq_ver_{Guid.NewGuid()}.hqc");
            try
            {
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    using (var bw = new BinaryWriter(fs))
                    {
                        bw.Write(999); // fake future version
                    }
                }

                QRandom ran = new QRandom(0);
                QActionSpace<int> qas = new QOrdinalActionSpace(ran, 4);
                ClassicQ q = new ClassicQ(qas);
                var hp = new HyperParams();

                Assert.ThrowsException<InvalidOperationException>(() =>
                    TrainingCheckpoint.Load(path, q, hp),
                    "Should reject checkpoint files from future versions");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
