using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    public class SoftmaxWithKLPenaltyActionSelector<T> : PolicyGradientWithSoftmaxActionSelector<T>
    {
        /// <summary>
        /// Coefficient scaling the KL penalty term.
        /// </summary>
        public double KlCoef { get; set; }

        /// <summary>
        /// Reference policy probabilities (uniform by default).
        /// </summary>
        private readonly double _uniformRef;

        public SoftmaxWithKLPenaltyActionSelector(IStateMapper<T> mapper, QActionSpace<int> actionSpace, double klCoef) : base(mapper, actionSpace)
        {
            KlCoef = klCoef;
            _uniformRef = 1.0 / actionSpace.MaximumNumberOfActions;
        }

        /// <summary>
        /// Retired for the reason given on <see cref="PolicyGradientActionSelector{T}"/>'s one-argument constructor:
        /// a mapper of its own numbers states differently from the learner. Pass the learner as the mapper instead.
        /// </summary>
        [Obsolete("Pass the learner as the state mapper: new SoftmaxWithKLPenaltyActionSelector<T>(learner, actionSpace, klCoef). The trainer hands ApplyAdvantage the learner's state index, so a selector with a mapper of its own applies each learned policy to the wrong state.", true)]
        public SoftmaxWithKLPenaltyActionSelector(QActionSpace<int> actionSpace, double klCoef)
            : this(new MemoryMapperAdapter<T>(), actionSpace, klCoef) { }


        public override void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
        {
            // 1) perform the usual GRPO update
            base.ApplyAdvantage(s, a, advantage, hp);

            // 2) apply a KL(p||p_ref) penalty gradient to keep policy close to reference
            var softmax = Theta[s];

            // Snapshot the current probabilities; every write below renormalizes the others.
            ICollection<int> keys = softmax.Keys.ToList();
            Dictionary<int, double> p = keys.ToDictionary(k => k, k => softmax.Probability(k));
            foreach (int idx in keys)
            {
                // gradient of D_KL(p||p_ref) = log(p/p_ref) + 1
                double klGrad = Math.Log(p[idx] / _uniformRef) + 1.0;
                // subtract a small step on the logit in the direction that reduces KL
                softmax[idx] -= hp.Alpha * KlCoef * klGrad;
            }
        }
    }
}
