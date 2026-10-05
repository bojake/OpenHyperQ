using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Util;

namespace HuntTheWumpus
{
    public class WumpusGameEnv : WumpusBaseGameEnv, IMACEPvEEnv<decimal, ScalarReward>
    {
        public WumpusGameEnv(QRandom ran = null, bool quiet = false, int staticSeed = DefaultStaticSeed) : base(ran, quiet, staticSeed: staticSeed)
        {
        }
        public decimal Discretize()
        {
            WumpusGameStatus rpt = Report;
            decimal ival = 0;
            int shift = 0;
            /*
            ival += (long)rpt.Location << shift;
            shift++;
            */
            // Encode the look into a 32 bit value. This assumes full observability
            for (int i = 0; i < rpt.Adjacent.Length; i++)
            {
                ulong lv = (ulong)rpt.Adjacent[i] << shift;
                // Console.WriteLine("{0}:{1} << {2} = 0x{3:X24}", i, rpt.Adjacent[i], shift, lv);
                ival += lv;
                shift += 3;
            }
            // Add the gold indicator
            if (rpt.Gold > 0)
            {
                ival += 1L << shift;
            }
            shift++;
            if (IncludeFood)
            {
                ival += (ulong)rpt.FoodLevel << shift; // Up to value 64, so 6 bits
            }
            shift += 6;
            // Future - add distance from entrace
            ival += (ulong)rpt.Location << shift;
            return (ival);
        }
    }
}
