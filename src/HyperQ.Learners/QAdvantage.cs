using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    [Serializable]
    public class QAdvantage
    {
        private const double EPSILON = 1e-8;
        /// <summary>
        /// The running return based on the return[n] = return[n-1]*g + R formula
        /// </summary>
        private List<double> _returns = new List<double>();
        private List<double> _adv = new List<double>();
        private double _adv_sum = 0.0;
        private double _adv_sum_sq = 0.0;
        public double[] Returns { get { return _returns.ToArray(); } }
        public double[] Advantages { get { return _adv.ToArray(); } }
        public QAdvantage()
        {
        }

        /// <summary>
        /// Returns the advantage recorded for the given iteration/step in the episode, scaled by the root
        /// mean square of the advantages recorded so far so that the policy selectors see a signal of
        /// unit magnitude whatever the reward scale of the environment.
        /// </summary>
        /// <remarks>
        /// The scaling deliberately does not centre the values. The recorded advantages already carry a
        /// state baseline, so their sign is meaningful: once a policy is good, nearly every step has a
        /// positive advantage, and subtracting the episode mean would turn that into a zero-mean noise
        /// signal that random-walks the policy away from what it learned.
        /// </remarks>
        /// <param name="iteration">The iteration or step in the episode trace</param>
        /// <returns></returns>
        public double this[int iteration]
        {
            get
            {
                int n = _adv.Count;
                if (n < 2)
                    return _adv[iteration];
                double rms = Math.Sqrt(Math.Max(_adv_sum_sq / n, 0.0));
                if (rms < EPSILON)
                    return _adv[iteration];
                return _adv[iteration] / (rms + EPSILON);
            }
        }
        public double AddReturn(double r2, double g)
        {
            if (_returns.Count > 0)
                r2 += g * _returns[_returns.Count - 1];
            _returns.Add(r2);
            return r2;
        }
        public void AddAdvantage(double a)
        {
            _adv_sum += a;
            _adv_sum_sq += a * a;
            _adv.Add(a);
        }

        /// <summary>
        /// Returns the raw (unscaled) advantage recorded for the given step.
        /// </summary>
        public double RawAdvantage(int iteration)
        {
            return _adv[iteration];
        }

        /// <summary>
        /// Add the given return and advantage to the lists
        /// </summary>
        /// <param name="r">The return to add</param>
        /// <param name="a">The advantage</param>
        public void Add(double r, double a)
        {
            _returns.Add(r);
            AddAdvantage(a);
        }

    }
}
