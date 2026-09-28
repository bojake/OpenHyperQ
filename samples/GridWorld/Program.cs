using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;
using System;

namespace HyperQ.Samples.GridWorld
{
    internal static class Program
    {
        private static void Main()
        {
            QRandom random = new QRandom(0);
            GridWorld world = new GridWorld(6, 6, random);
            GridWorldMappedActionSpace actions = new GridWorldMappedActionSpace(random);
            ClassicQ q = new ClassicQ(actions);
            eGreedyActionSelector<decimal> selector = new eGreedyActionSelector<decimal>(actions);
            PvESARSATrainer<decimal> trainer = new PvESARSATrainer<decimal>(q, QEvalType.OnPolicy, selector, random)
            {
                MaxIterations = 100
            };
            HyperParams hp = new HyperParams(g: 0.98, e: 0.4, a: 0.6, edecay: 0.995, adecay: 0.999, min_epsilon: 0.05, min_alpha: 0.1);

            for (int i = 0; i < 250; i++)
            {
                trainer.Episode(world, hp);
                hp.DecayEpsilon();
                hp.DecayAlpha();
            }

            Console.WriteLine($"GridWorld trained for 250 episodes. Average reward: {world.Metrics.AverageReward:F3}");
        }
    }
}
