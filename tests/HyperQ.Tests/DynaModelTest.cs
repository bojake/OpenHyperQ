using HyperQ.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HyperQ.Test
{
    /// <summary>
    /// The Dyna model's next state for (s,a) is the most frequent one seen. Its cache used to compare a new
    /// probability with the cached one, which never shrank, so the first next state ever seen (probability 1) stayed
    /// the model's answer for good.
    /// </summary>
    [TestClass]
    public class DynaModelTest
    {
        private static readonly QAction A0 = new QAction(0, 0.0, 0);

        private static decimal Next(DynaState<decimal, ScalarReward> ds)
        {
            return ds.Sample(new DynaEntry<decimal>(1M, A0)).Item1;
        }

        [TestMethod]
        public void TheModelPredictsTheMostFrequentNextState()
        {
            var ds = new DynaState<decimal, ScalarReward>(10, 0);
            HyperParams hp = new HyperParams();
            ds.Update(1M, A0, 10M, -1.0, hp);
            Assert.AreEqual(10M, Next(ds));
            ds.Update(1M, A0, 20M, -1.0, hp);
            Assert.AreEqual(10M, Next(ds), "a tie keeps the state that reached the count first");
            ds.Update(1M, A0, 20M, -1.0, hp);
            Assert.AreEqual(20M, Next(ds), "20 now leads 2 to 1");
            ds.Update(1M, A0, 20M, -1.0, hp);
            ds.Update(1M, A0, 10M, -1.0, hp);
            Assert.AreEqual(20M, Next(ds), "3 to 2");
            ds.Update(1M, A0, 10M, -1.0, hp);
            ds.Update(1M, A0, 10M, -1.0, hp);
            Assert.AreEqual(10M, Next(ds), "10 takes the lead back, 4 to 3");
        }

        [TestMethod]
        public void TheModelIsRightWhenFirstSampledLate()
        {
            // Sampling before the counts change must not pin the answer either.
            var ds = new DynaState<decimal, ScalarReward>(10, 0);
            HyperParams hp = new HyperParams();
            ds.Update(1M, A0, 10M, -1.0, hp);
            for (int i = 0; i < 5; i++)
            {
                ds.Update(1M, A0, 30M, -1.0, hp);
            }
            Assert.AreEqual(30M, Next(ds));
        }

        [TestMethod]
        public void TheMaceModelPredictsTheMostFrequentNextState()
        {
            var ds = new HyperQ.MACE.Training.DynaState<decimal, ScalarReward>(10, 0);
            HyperParams hp = new HyperParams();
            QAction[] a = new QAction[] { A0, A0 };
            ds.Update(1M, a, 10M, -1.0, hp);
            ds.Update(1M, a, 20M, -1.0, hp);
            ds.Update(1M, a, 20M, -1.0, hp);
            Assert.AreEqual(20M, ds.Sample(new HyperQ.MACE.Training.DynaEntry<decimal>(1M, a)).Item1);
        }
    }
}
