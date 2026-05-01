using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public class QParamStatic : QParam
    {
        public QParamStatic(double v) : base(v, 1.0, v) { }

        public override QParam Decay()
        {
            return this;
        }
    }
}
