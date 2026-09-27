using HyperQ.Util;
using System;
using System.Collections.Generic;

namespace HyperQ.Learners
{
    /// <summary>
    /// A GRPO-based policy selector with an additional KL-divergence penalty to a reference policy.
    /// </summary>
    [Serializable]
    public class GRPOWithKLPenaltyActionSelector<T> : PolicyGradientActionSelector<T>
    {
        /// <summary>
        /// Coefficient scaling the KL penalty term.
        /// </summary>
        public double KlCoef { get; set; }

        /// <summary>
        /// Reference policy probabilities (uniform by default).
        /// </summary>
        private readonly double _uniformRef;

        public GRPOWithKLPenaltyActionSelector(IStateMapper<T> mapper, QActionSpace<int> actionSpace, double klCoef) : base(mapper, actionSpace)
        {
            KlCoef = klCoef;
            _uniformRef = 1.0 / actionSpace.MaximumNumberOfActions;
        }

        /// <summary>Convenience 1-arg ctor — creates internal MemoryBackedHyperMapper&lt;T&gt;.</summary>
        public GRPOWithKLPenaltyActionSelector(QActionSpace<int> actionSpace, double klCoef)
            : this(new MemoryMapperAdapter<T>(), actionSpace, klCoef) { }

        public override void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
        {
            // 1) perform the usual GRPO update
            base.ApplyAdvantage(s, a, advantage, hp);

            // 2) apply a KL(p||p_ref) penalty gradient to keep policy close to reference
            var logits = Theta[s];
            // Snapshot the current probabilities; every write below renormalizes the others.
            var priors = logits.LockPriors();

            foreach (int idx in priors.Keys)
            {
                // current policy probability
                double p = priors[idx];
                // gradient of D_KL(p||p_ref) = log(p/p_ref) + 1
                double klGrad = Math.Log(p / _uniformRef);// + 1.0;
                // double klGrad = p - _uniformRef;
                // subtract a small step in direction of reducing KL, against the logit: the indexer reads
                // back the probability, so "logits[idx] -= step" replaced the logit with p - step.
                logits[idx] = logits.Logit(idx) - hp.Alpha * KlCoef * klGrad;
            }
        }
    }
}
