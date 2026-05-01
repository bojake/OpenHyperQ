using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    /// <summary>
    /// A generator class that generates the different single-Q learners for use with the layered Q learner.
    /// </summary>
    [Serializable]
    public class SingleQGenerator<T>
    {
        public QActionSpace<int> ActionSpace { get; private set; } = null;
        private Func<double> _DefaultValueFunc;
        public SingleQGenerator(QActionSpace<int> actionSpace, Func<double> defaultValueFunc = null)
        {
            _DefaultValueFunc = defaultValueFunc;
            ActionSpace = actionSpace;
        }

        public Q<T> MappedQ()
        {
            return new MappedQ<T>(ActionSpace, _DefaultValueFunc);
        }
        public Q<QState<T>> HyperQ()
        {
            return new SingleHyperQ<T>(ActionSpace, _DefaultValueFunc);
        }

        public IHyperQ<T> HyperQGenerator()
        {
            return new SingleHyperQ<T>(ActionSpace, _DefaultValueFunc);
        }

        public IHyperQ<T> LayeredQ()
        {
            return new LayeredHyperQ<T>(HyperQGenerator, ActionSpace, _DefaultValueFunc);
        }
    }
}
