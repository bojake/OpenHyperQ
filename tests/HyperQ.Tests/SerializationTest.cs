using HyperQ.Learners;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace HyperQ.Test
{
    /// <summary>
    /// Round trips a learner through the checkpoint format that replaced BinaryFormatter serialization.
    /// </summary>
    [TestClass]
    public class SerializationTest
    {
        private Q<decimal> MakeQ()
        {
            Q<decimal> q1 = new ClassicQ(new QOrdinalActionSpace(new QRandom(0), 10));
            q1.SetValue(10, 1, 5.0);
            Q<decimal> q2 = new ClassicQ(new QOrdinalActionSpace(new QRandom(0), 10));
            q2.SetValue(10, 1, 7.0);
            q2.SetValue(7, 2, 9.0);
            // Test Merge MAX
            q1.MergeInto(q2, EvalMethodType.Max);
            q1 = q1.Clone();
            q2 = new ClassicQ(new QOrdinalActionSpace(new QRandom(0), 10));
            q2.SetValue(125, 5, 17.0);
            q2.SetValue(8, 5, 29.0);
            q2.SetValue(122, 5, 17.0);
            q2.SetValue(122, 4, 18.0);
            q2.SetValue(122, 3, 27.0);
            q2.SetValue(11, 5, 13.0);
            q2.SetValue(10, 5, 11.0);
            q2.SetValue(10, 4, 15.0);
            q2.SetValue(10, 3, 3.0);
            q2.SetValue(10, 2, 18.0);
            q2.SetValue(10, 1, 19.0);
            q2.SetValue(12, 5, 89.0);
            q2.SetValue(12, 5, 26.0);
            q2.SetValue(7, 5, 21.0);
            q2.SetValue(7, 4, 19.0);
            q2.SetValue(7, 3, 26.0);
            q2.SetValue(7, 2, 13.0);
            // Test Merge MAX
            q1.MergeInto(q2, EvalMethodType.Max);
            return (q1);
        }

        [TestMethod]
        public void TestSaveLoadQ()
        {
            Q<decimal> q = MakeQ();
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    LearnerCheckpoint.Save(w, (ICheckpointable)q);
                }
                data = ms.ToArray();
            }
            // Now load it into a fresh learner
            q = new ClassicQ(new QOrdinalActionSpace(new QRandom(0), 10));
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader r = new BinaryReader(ms, Encoding.UTF8))
            {
                LearnerCheckpoint.Load(r, (ICheckpointable)q);
            }
            // Run tests
            QAction r1 = q.ArgMax(7);
            Assert.IsNotNull(r1);
            Assert.AreEqual(3, r1.Item1);
            Assert.AreEqual(26.0, r1.Item2);
            r1 = q.ArgMin(7);
            Assert.IsNotNull(r1);
            Assert.AreEqual(0, r1.Item1);
            Assert.AreEqual(0.0, r1.Item2);
            uint ix1 = q.AddState(1M);
            uint ix2 = q.AddState(2M);
            uint ix3 = q.AddState(3M);
            Assert.AreNotEqual(ix2, ix1, "Indices should not map equally (1 v 2).");
            Assert.AreNotEqual(ix3, ix1, "Indices should not map equally (1 v 3).");
            Assert.AreNotEqual(ix2, ix3, "Indices should not map equally (2 v 3).");
        }

        [TestMethod]
        public void TestLoadRejectsAnotherLearnerType()
        {
            Q<decimal> q = MakeQ();
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    LearnerCheckpoint.Save(w, (ICheckpointable)q);
                }
                data = ms.ToArray();
            }
            ClassicQQ other = new ClassicQQ(new QOrdinalActionSpace(new QRandom(0), 10));
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader r = new BinaryReader(ms, Encoding.UTF8))
            {
                Assert.ThrowsException<InvalidDataException>(() => LearnerCheckpoint.Load(r, other));
            }
        }
    }
}
