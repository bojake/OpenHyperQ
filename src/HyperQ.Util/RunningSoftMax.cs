using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public class RunningSoftMax : RunningNormalizer
    {
        private double _max = double.NegativeInfinity;

        public RunningSoftMax()
        {
        }

        /// <summary>
        /// Gets or sets the logit for the given index (0 when it has never been set). Setting a logit updates
        /// the softmax, whose probabilities are read with <see cref="Probability"/>. The indexer reads and
        /// writes the same quantity, so <c>sm[i] += delta</c> is a step on the logit. (It used to read back the
        /// probability, which turned that step into "the probability plus delta".)
        /// </summary>
        public override double this[int idx]
        {
            get
            {
                return __raw_value(idx);
            }
            set
            {
                base[idx] = value;
            }
        }

        /// <summary>
        /// Returns the softmax probability of the given index.
        /// </summary>
        public double Probability(int idx)
        {
            return base[idx];
        }

        /// <summary>
        /// Returns the exp(value - max) for the softmax of logits calculation.
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        protected override double __get_value(int idx)
        {
            if (!base.__has_value(idx))
            {
                return 1.0;
            }
            return Math.Exp(base.__get_value(idx) - _max);
        }

        /// <summary>
        /// Intercepts the setting to update the max value of the original values
        /// </summary>
        /// <param name="idx"></param>
        /// <param name="v"></param>
        protected override bool __set_value(int idx, double v)
        {
            base.__set_value(idx, v);
            if (v > _max)
            {
                _max = v;
                return true;
            }
            return false;
        }
    }
}
