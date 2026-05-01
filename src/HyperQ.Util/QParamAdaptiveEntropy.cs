using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public class QParamAdaptiveEntropy : QParam
    {
        private Func<double> entropyFunc;

        public QParamAdaptiveEntropy(double v, Func<double> entropyProvider, double min = 0.0) : base(v, 1.0, min)
        {
            entropyFunc = entropyProvider;
        }

        public override QParam Decay()
        {
            double entropy = entropyFunc.Invoke();
            Value = Math.Max(Value * entropy, MinValue);
            return this;
        }
    }
}
