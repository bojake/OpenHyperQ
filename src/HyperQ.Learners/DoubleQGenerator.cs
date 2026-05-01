using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    /// <summary>
    /// A generator class that generates the different double-Q learners for use with the layered Q learner.
    /// </summary>
    [Serializable]
    public class DoubleQGenerator<T>
    {
        public QActionSpace<int> ActionSpace { get; private set; } = null;
        private Func<double> _DefaultValueFunc;
        private Func<double,double,double> _DefaultBlendingFunc;
        private QRandom _ran;
        public DoubleQGenerator(QActionSpace<int> actionSpace, Func<double> defaultValueFunc = null, Func<double,double,double> defaultBlendingFunc=null, QRandom ran = null)
        {
            ActionSpace = actionSpace;
            _DefaultValueFunc = defaultValueFunc;
            _DefaultBlendingFunc = defaultBlendingFunc;
            _ran = ran ?? QRandom.Instance;
        }

        public Q<T> Mapped()
        {
            return new MappedQQ<T>(ActionSpace, _DefaultValueFunc);
        }
        public Q<QState<T>> Hyper()
        {
            return new DoubleHyperQ<T>(ActionSpace, _DefaultValueFunc, _ran, _DefaultBlendingFunc);
        }

        public IHyperQ<T> HyperGenerator()
        {
            return new DoubleHyperQ<T>(ActionSpace, _DefaultValueFunc, _ran, _DefaultBlendingFunc);
        }

        public IHyperQ<T> Layered()
        {
            return new LayeredHyperQ<T>(HyperGenerator, ActionSpace, _DefaultValueFunc);
        }
    }
}
