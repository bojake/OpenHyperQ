using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.MultiHead;
using HyperQ.MultiHead.Training;
using HyperQ.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace HyperQ.Test
{
    [TestClass]
    public class MultiHeadTrainerTest
    {
        private sealed class SingleStepEnv : IPvEEnv<int, MultiReward>
        {
            public EnvMetrics Metrics { get; } = new EnvMetrics();
            private bool _done;

            public int Discretize()
            {
                return 0;
            }

            public void Render()
            {
            }

            public void Reset()
            {
                _done = false;
            }

            public EnvResult<MultiReward> Step(QAction action)
            {
                if (_done)
                {
                    return new EnvResult<MultiReward>(new MultiReward(new double[] { 0.0, 0.0 }), true);
                }
                _done = true;
                return new EnvResult<MultiReward>(new MultiReward(new double[] { 1.0, -1.0 }), true);
            }
        }

        private sealed class FixedSelector : IActionSelector<int>
        {
            public bool LastActionWasRandom => false;
            public long RandomActionCount => 0;
            public MinMaxActionEnum ActionMode { get; set; } = MinMaxActionEnum.MostProbable;
            public Action<int, QAction> TelemetryCallback { get; set; }

            public void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
            {
            }

            public void EndEpisode(RewardAccumulator reward)
            {
            }

            public QAction SelectAction(IArgMinMax<int> q, int state, double epsilon)
            {
                var action = new QAction(0, 0.0, 0);
                TelemetryCallback?.Invoke(state, action);
                return action;
            }

            public void StartEpisode()
            {
            }
        }

        private sealed class SpyMultiHeadLearner : MultiHeadQLearner<int>
        {
            public int OnPolicyCalls { get; private set; }
            public int OffPolicyCalls { get; private set; }

            public SpyMultiHeadLearner(QActionSpace<int> actionSpace)
                : base(() => new MappedQ<int>(actionSpace), headCount: 2, scalarizer: new LinearScalarizer(new double[] { 0.5, 0.5 }))
            {
            }

            public override double OnPolicyUpdate(int s, int a, int sprime, int aprime, IReward reward, HyperParams hp)
            {
                OnPolicyCalls++;
                return 0.0;
            }

            public override double OffPolicyUpdate(int s, int a, int sprime, IReward reward, HyperParams hp, EvalMethodType evalType)
            {
                OffPolicyCalls++;
                return 0.0;
            }
        }

        [TestMethod]
        public void TestTrainerHonorsOffPolicyEvalType()
        {
            var actionSpace = new QOrdinalActionSpace(QRandom.Instance, 2);
            var spyQ = new SpyMultiHeadLearner(actionSpace);
            var selector = new FixedSelector();
            var trainer = new PvEMultiHeadSARSATrainer<int>(spyQ, QEvalType.OffPolicy, selector);
            var env = new SingleStepEnv();

            trainer.Episode(env, new HyperParams());

            Assert.AreEqual(0, spyQ.OnPolicyCalls, "Off-policy trainer should not call OnPolicyUpdate.");
            Assert.AreEqual(1, spyQ.OffPolicyCalls, "Off-policy trainer should call OffPolicyUpdate once in a single-step episode.");
        }
    }
}
