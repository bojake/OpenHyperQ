using HyperQ.Learners;
using HyperQ.MultiHead;
using HyperQ.MultiHead.Eval;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace HyperQ.Test
{
    [TestClass]
    public class ScalarizerAndEvaluatorTest
    {
        private static TException ExpectThrows<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
                Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            }
            catch (TException ex)
            {
                return ex;
            }

            throw new InvalidOperationException("Unreachable.");
        }

        [TestMethod]
        public void TestLinearScalarizerDefensivelyCopiesWeights()
        {
            var weights = new double[] { 1.0, 0.0 };
            var scalarizer = new LinearScalarizer(weights);
            weights[0] = 0.0;
            weights[1] = 1.0;

            double score = scalarizer.Score(new double[] { 5.0, 1.0 });
            Assert.AreEqual(5.0, score, 1e-9);
        }

        [TestMethod]
        public void TestLinearScalarizerRejectsShortInput()
        {
            var scalarizer = new LinearScalarizer(new double[] { 0.5, 0.5 });
            ExpectThrows<ArgumentException>(() => scalarizer.Score(new double[] { 1.0 }));
        }

        [TestMethod]
        public void TestMultiHeadQEvaluatorRejectsNullArgs()
        {
            var aspace = new QOrdinalActionSpace(new QRandom(0), 2);
            var q = new MultiHeadClassicQ(aspace, headCount: 2, scalarizer: new LinearScalarizer(new double[] { 0.5, 0.5 }));
            var selector = new UniformActionSelector<decimal>(q, aspace);

            ExpectThrows<ArgumentNullException>(() => new MultiHeadQEvaluator<decimal, MultiReward>(null, selector));
            ExpectThrows<ArgumentNullException>(() => new MultiHeadQEvaluator<decimal, MultiReward>(q, null));
        }
    }
}
