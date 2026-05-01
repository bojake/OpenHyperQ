using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace LEM
{
    [Serializable]
    public class LEMGameEnv : LEMBaseGameEnv, IPvEEnv<decimal>
    {

        public LEMGameEnv(double g = 0.001, double netmass = 16500, Random ran = null, bool quiet = false, EnvMetrics metrics = null, int precision = DEFAULT_ACTION_PRECISION, double stepduration = 10.0)
        : base(g, netmass, ran, quiet, precision, stepduration)
        {
            Metrics = metrics ?? new EnvMetrics();
        }

        public Tuple<double, bool> Step(QAction[] action)
        {
            return (Step(action[0]));
        }

        public decimal Discretize()
        {
            // _LEM.Altitude, _LEM.Speed, _LEM.Mass, _ElapsedTime, _LEM.NetFuel, Status
            // Math.Truncate(Speed * 3600.0)
            //[Speed][altitude%][fuel%]
            //   000      000      000
            var rpt = Report;
            decimal fmass = (decimal)Math.Truncate(rpt.Item5 / 16000 * 100);
            decimal falt = (decimal)Math.Truncate(rpt.Item1 / 120 * 100);
            decimal s = Math.Truncate(1000000M * (decimal)rpt.Item2 * 3600M) + 1000M * falt + fmass;
            return (s);
        }
    }
}
