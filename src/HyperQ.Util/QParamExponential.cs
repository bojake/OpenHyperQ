using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public class QParamExponential : QParam
    {
        public QParamExponential(double v, double decay = 0.99, double min = 0.0) : base(v, decay, min) { }

        public override QParam Decay()
        {
            Value = Math.Max(Value * DecayRate, MinValue);
            return this;
        }
    }
}
