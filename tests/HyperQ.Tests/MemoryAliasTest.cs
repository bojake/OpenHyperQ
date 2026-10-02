using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;

namespace HyperQ.Test
{
    /// <summary>
    /// The scalar memory aliases (QMemory&lt;T&gt;, QNegPosMemory&lt;T&gt; and the episodic ones) are siblings with no
    /// conversions between them. QNegPosMemory&lt;T&gt; used to declare an implicit conversion to QMemory&lt;T&gt; that
    /// always threw, so code that mixed the two compiled and then crashed.
    /// </summary>
    [TestClass]
    public class MemoryAliasTest
    {
        private static QState<decimal> S(int v)
        {
            return new QState<decimal>().Push(v);
        }

        [TestMethod]
        public void AConditionalOverTwoMemoryAliasesTakesTheBaseType()
        {
            QRandom random = new QRandom(0);
            bool negpos = true;
            // With the old conversion this conditional's type was QMemory<T>, reached through the operator that
            // threw. Without it the conditional takes the declared base type.
            QMemory<QState<decimal>, ScalarReward> memory = negpos
                ? new QNegPosMemory<QState<decimal>>(10, random)
                : new QMemory<QState<decimal>>(10, random);
            Assert.IsInstanceOfType(memory, typeof(QNegPosMemory<QState<decimal>>));
        }

        [TestMethod]
        public void ANegPosMemoryReplaysThroughTheTrainer()
        {
            QRandom random = new QRandom(0);
            QOrdinalActionSpace space = new QOrdinalActionSpace(random, 2);
            space.ToIndex(1);
            SingleHyperQ<decimal> q = new SingleHyperQ<decimal>(space);
            PvESARSATrainer<QState<decimal>> trainer = new PvESARSATrainer<QState<decimal>>(q, QEvalType.OffPolicy, new eGreedyActionSelector<QState<decimal>>(space), random);
            QNegPosMemory<QState<decimal>> memory = new QNegPosMemory<QState<decimal>>(10, random);
            trainer.EnableMemory(memory);
            memory.Remember(S(1), new QAction(0, 0.0, 0), S(2), new QAction(0, 0.0, 0), -5.0);
            trainer.Reminisce(new HyperParams(g: 0.0, a: 1.0), 4);
            Assert.AreEqual(-5.0, q.GetValue(S(1), 0), 1e-12, "the replayed transition was learned");
        }

        [TestMethod]
        public void TheMemoryAliasesDeclareNoConversions()
        {
            Type[] aliases = { typeof(QMemory<>), typeof(QNegPosMemory<>), typeof(QEpisodicMemory<>), typeof(QEpisodicNegPosMemory<>), typeof(DynaState<>) };
            foreach (Type alias in aliases)
            {
                string[] conversions = alias.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name == "op_Implicit" || m.Name == "op_Explicit")
                    .Select(m => m.ToString())
                    .ToArray();
                Assert.AreEqual(0, conversions.Length, alias.Name + " declares " + string.Join(", ", conversions));
            }
        }
    }
}
