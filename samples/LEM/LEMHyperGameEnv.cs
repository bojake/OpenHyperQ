using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEM
{
    [Serializable]
    public class LEMHyperGameEnv : LEMBaseGameEnv, IPvEEnv<QState<decimal>>
    {

        public LEMHyperGameEnv(double g = 0.001, double netmass = 16500, Random ran = null, bool quiet = false, EnvMetrics metrics = null, int precision = DEFAULT_ACTION_PRECISION, double stepduration = 10.0)
        : base(g, netmass, ran, quiet, precision, stepduration)
        {
            Metrics = metrics ?? new EnvMetrics();
        }

        public Tuple<double, bool> Step(QAction[] action)
        {
            return (Step(action[0]));
        }

        public QState<decimal> Discretize()
        {
            QState<decimal> s = new QState<decimal>();
            var rpt = Report;
            // Quantize each dimension to integer-percentage buckets so that
            // the MemoryBackedHyperMapper produces a finite, reusable state
            // space. Raw float values would cause state-space explosion with
            // no state revisitation, making tabular Q-learning impossible.
            //   Altitude : 0-100 buckets  (1 bucket ≈ 1.2 miles)
            //   Speed    : 0-100 buckets  (1 bucket ≈ 36 MPH re: 3600 MPH max)
            //   Fuel     : 0-100 buckets  (1 bucket ≈ 160 lbs re: 16000 lbs)
            s.Push(Math.Truncate((decimal)(rpt.Item1 / 120.0 * 100.0)));       // altitude %
            s.Push(Math.Truncate((decimal)(rpt.Item2 * 3600.0 / 36.0)));       // speed % of max
            s.Push(Math.Truncate((decimal)(rpt.Item5 / 16000.0 * 100.0)));     // fuel %
            return s;
        }
    }
}
