using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace HyperQ.Test
{
    /// <summary>
    /// Which updates <see cref="PvESARSATrainer{T}"/> makes. Its constructor used to drop the evalType argument, so every
    /// trainer ran the on-policy (SARSA) loop. Its Dyna step drew an integer with <c>Random.Next()</c> and compared it
    /// with probabilities, so Dyna never learned a model or planned.
    /// </summary>
    [TestClass]
    public class TrainerUpdatesTest
    {
        /// <summary>A walk on cells 0 to 4: action 0 steps right, any other left (not past 0); each step costs 1 and the
        /// episode ends on cell 4.</summary>
        private sealed class WalkEnv : IPvEEnv<decimal, ScalarReward>
        {
            private int _position;

            public EnvMetrics Metrics { get; } = new EnvMetrics();

            public decimal Discretize()
            {
                return _position;
            }

            public void Render()
            {
            }

            public void Reset()
            {
                _position = 0;
            }

            public EnvResult<ScalarReward> Step(QAction action)
            {
                _position = action.Action == 0 ? _position + 1 : Math.Max(0, _position - 1);
                return new EnvResult<ScalarReward>(-1.0, _position >= 4);
            }
        }

        private sealed class CountingQ : ClassicQ
        {
            public int OnPolicyCalls { get; private set; }
            public int OffPolicyCalls { get; private set; }

            public CountingQ(QActionSpace<int> actionSpace) : base(actionSpace)
            {
            }

            public override double OnPolicyUpdate(decimal s, int a, decimal sprime, int aprime, double r, HyperParams hp)
            {
                OnPolicyCalls++;
                return base.OnPolicyUpdate(s, a, sprime, aprime, r, hp);
            }

            public override double OffPolicyUpdate(decimal s, int a, decimal sprime, double r, HyperParams hp, EvalMethodType evalType = EvalMethodType.Max)
            {
                OffPolicyCalls++;
                return base.OffPolicyUpdate(s, a, sprime, r, hp, evalType);
            }
        }

        private static (CountingQ q, PvESARSATrainer<decimal> trainer, int steps) Train(QEvalType evalType, Action<PvESARSATrainer<decimal>> setUp = null, int episodes = 5)
        {
            QRandom ran = new QRandom(3);
            QActionSpace<int> actions = new QOrdinalActionSpace(ran, 4);
            CountingQ q = new CountingQ(actions);
            PvESARSATrainer<decimal> trainer = new PvESARSATrainer<decimal>(q, evalType, new eGreedyActionSelector<decimal>(actions), ran);
            trainer.MaxIterations = 200;
            setUp?.Invoke(trainer);
            int steps = 0;
            trainer.OnStepEnd += () => steps++;
            WalkEnv world = new WalkEnv();
            HyperParams hp = new HyperParams(0.9, 0.2, 0.5);
            for (int i = 0; i < episodes; i++)
            {
                trainer.Episode(world, hp);
            }
            return (q, trainer, steps);
        }

        [TestMethod]
        public void TheTrainerHonorsTheEvalType()
        {
            var (off, _, offSteps) = Train(QEvalType.OffPolicy);
            Assert.AreEqual(0, off.OnPolicyCalls, "an off-policy trainer makes no on-policy updates");
            Assert.AreEqual(offSteps, off.OffPolicyCalls, "one off-policy update per step");

            var (on, _, onSteps) = Train(QEvalType.OnPolicy);
            Assert.AreEqual(0, on.OffPolicyCalls, "an on-policy trainer makes no off-policy updates");
            Assert.AreEqual(onSteps, on.OnPolicyCalls, "one on-policy update per step");
        }

        [TestMethod]
        public void UniformDynaPlansOnMostSteps()
        {
            // Planning runs on the steps whose draw is below 0.8, which are exactly the steps that update the model.
            var (q, trainer, steps) = Train(QEvalType.OffPolicy, t => t.EnableDyna(new DynaState<decimal>(3, 0), 0.8));
            int planning = q.OffPolicyCalls - steps;
            // Three planning updates on each of about 80% of the steps.
            Assert.IsTrue(planning >= 3 * steps / 2 && planning <= 3 * steps, "planning updates {0} for {1} steps", planning, steps);
            Assert.IsTrue(trainer.Dyna.Count > 0, "the model learned no (s,a)");
        }

        [TestMethod]
        public void PrioritizedDynaPlans()
        {
            var (q, _, steps) = Train(QEvalType.OffPolicy, t => t.EnableDyna(new DynaState<decimal>(3, 0), 0.8, DynaSweepMode.Prioritized));
            Assert.IsTrue(q.OffPolicyCalls > steps, "no planning updates: {0} updates for {1} steps", q.OffPolicyCalls, steps);
        }

        [TestMethod]
        public void PrioritizedSweepingKeepsItsQueueSmall()
        {
            // Each sweep pops one entry and pushes its predecessors; with every insertion queued, the queue grew with
            // every step until long runs ran out of memory.
            var (_, trainer, steps) = Train(QEvalType.OffPolicy, t => t.EnableDyna(new DynaState<decimal>(10, 0), 0.8, DynaSweepMode.Prioritized), episodes: 300);
            Assert.IsTrue(steps > 1000);
            Assert.IsTrue(trainer.Dyna.QueuedCount <= trainer.Dyna.Count, "{0} queued for {1} model entries", trainer.Dyna.QueuedCount, trainer.Dyna.Count);
            Assert.IsTrue(trainer.Dyna.PriorityHeapSize <= 2 * trainer.Dyna.QueuedCount + 65, "heap of {0}", trainer.Dyna.PriorityHeapSize);
        }

        [TestMethod]
        public void PlanningMoreOftenThanTheModelUpdatesDoesNotFailOnAnEmptyModel()
        {
            // At a planning frequency of 1, a step whose draw is at least the model update frequency (0.8) plans without
            // updating the model, and on the first steps the model can still be empty.
            for (int seed = 0; seed < 20; seed++)
            {
                QRandom ran = new QRandom(seed);
                QActionSpace<int> actions = new QOrdinalActionSpace(ran, 4);
                CountingQ q = new CountingQ(actions);
                PvESARSATrainer<decimal> trainer = new PvESARSATrainer<decimal>(q, QEvalType.OffPolicy, new eGreedyActionSelector<decimal>(actions), ran);
                trainer.MaxIterations = 20;
                trainer.EnableDyna(new DynaState<decimal>(2, 0), 1.0);
                trainer.Episode(new WalkEnv(), new HyperParams(0.9, 0.2, 0.5));
            }
        }
    }
}
