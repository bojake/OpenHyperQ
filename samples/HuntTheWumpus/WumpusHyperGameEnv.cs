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
    public class WumpusHyperGameEnv : WumpusBaseGameEnv, IMACEPvEEnv<QState<decimal>, ScalarReward>
    {
        public WumpusHyperGameEnv(QRandom ran = null, bool quiet = false) : base(ran, quiet)
        {
        }
        public QState<decimal> Discretize()
        {
            QState<decimal> s = new QState<decimal>();
            WumpusGameStatus rpt = Report;
            for (int i = 0; i < rpt.Adjacent.Length; i++)
            {
                s.Push((decimal)rpt.Adjacent[i]);
            }
            s.Push(rpt.Location);
            s.Push(rpt.Gold);
            s.Push(rpt.FoodLevel);
            return s;
        }
    }
}
