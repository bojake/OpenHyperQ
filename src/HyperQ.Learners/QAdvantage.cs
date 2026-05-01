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
        /// Returns the advantage computed for the given iteration/step in the episode,
        /// normalized using running mean and standard deviation for numerical stability.
        /// </summary>
        /// <param name="iteration">The iteration or step in the episode trace</param>
        /// <returns></returns>
        public double this[int iteration]
        {
            get
            {
                int n = _adv.Count;
                if (n < 2)
                    return _adv[iteration];
                double mean = _adv_sum / n;
                double variance = (_adv_sum_sq / n) - (mean * mean);
                double std = Math.Sqrt(Math.Max(variance, 0.0));
                if (std < EPSILON)
                    return _adv[iteration] - mean;
                return (_adv[iteration] - mean) / (std + EPSILON);
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
        /// Returns the raw (unnormalized) advantage for the given step.
        /// Use this for eligibility trace accumulation where normalization would corrupt the running sum.
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

        // ── GAE support ──

        /// <summary>
        /// Running eligibility trace accumulator for online GAE (TD(λ)).
        /// Updated each step: e_t = δ_t + γλ · e_{t-1}
        /// </summary>
        private double _eligibilityTrace = 0.0;

        /// <summary>
        /// Returns the current eligibility trace value (online GAE approximation).
        /// </summary>
        public double EligibilityTrace => _eligibilityTrace;

        /// <summary>
        /// Updates the eligibility trace with a new TD error and returns the current trace value.
        /// This is the online (streaming) form of GAE: e_t = δ_t + γλ · e_{t-1}
        /// </summary>
        /// <param name="delta">The one-step TD error δ_t</param>
        /// <param name="gamma">Discount factor γ</param>
        /// <param name="lambda">GAE tradeoff parameter λ</param>
        /// <returns>The updated eligibility trace value</returns>
        public double UpdateEligibilityTrace(double delta, double gamma, double lambda)
        {
            _eligibilityTrace = delta + gamma * lambda * _eligibilityTrace;
            return _eligibilityTrace;
        }

        /// <summary>
        /// Resets the eligibility trace to zero (call at episode start).
        /// </summary>
        public void ResetEligibilityTrace()
        {
            _eligibilityTrace = 0.0;
        }

        /// <summary>
        /// Computes Generalized Advantage Estimation (Schulman 2016) over the stored
        /// one-step TD errors via a backward pass.
        /// A_{T-1} = δ_{T-1}; A_t = δ_t + γλ · A_{t+1}
        /// </summary>
        /// <param name="gamma">Discount factor γ</param>
        /// <param name="lambda">GAE tradeoff λ ∈ [0,1]</param>
        /// <returns>Array of GAE advantages, one per stored step</returns>
        public double[] ComputeGAE(double gamma, double lambda)
        {
            int n = _adv.Count;
            if (n == 0) return Array.Empty<double>();

            double[] gae = new double[n];
            double running = 0.0;
            for (int t = n - 1; t >= 0; t--)
            {
                running = _adv[t] + gamma * lambda * running;
                gae[t] = running;
            }
            return gae;
        }

        /// <summary>
        /// Normalizes a GAE array to zero mean and unit variance for stable policy updates.
        /// </summary>
        public static double[] NormalizeGAE(double[] gae)
        {
            if (gae.Length < 2) return gae;

            double sum = 0.0, sumSq = 0.0;
            for (int i = 0; i < gae.Length; i++)
            {
                sum += gae[i];
                sumSq += gae[i] * gae[i];
            }
            double mean = sum / gae.Length;
            double variance = (sumSq / gae.Length) - (mean * mean);
            double std = Math.Sqrt(Math.Max(variance, 0.0));

            double[] normalized = new double[gae.Length];
            if (std < EPSILON)
            {
                for (int i = 0; i < gae.Length; i++)
                    normalized[i] = gae[i] - mean;
            }
            else
            {
                for (int i = 0; i < gae.Length; i++)
                    normalized[i] = (gae[i] - mean) / (std + EPSILON);
            }
            return normalized;
        }
    }
}
