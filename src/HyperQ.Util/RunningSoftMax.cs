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
