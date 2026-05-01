using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    [Serializable]
    public abstract class BaseQ<T> : Q<T>
    {
        public QActionSpace<int> ActionSpace { get; private set; }
        public virtual double this[T state, int action] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        /// <summary>
        /// The action used to get a default value for a (s,a) pair.
        /// </summary>
        public Func<double> DefaultValueFunc { get; set; } = () => 0.0;
        public QAdvantage Advantage { get; private set; } = null;

        public BaseQ(QActionSpace<int> actionSpace)
        {
            Advantage = new QAdvantage();
            ActionSpace = actionSpace;
        }
        public virtual void ResetAdvantageTrace()
        {
            Advantage = new QAdvantage();
        }
        public abstract Tuple<uint, uint> Shape { get ; }
        public abstract double[,] AsMatrix { get; }
        public virtual double[] GetActionArray(T stateKey)
        {
            throw (new NotImplementedException());
        }

        public virtual IDictionary<T, double[]> Snapshot(IEnumerable<T> states)
        {
            Dictionary<T, double[]> snap = new Dictionary<T, double[]>();
            foreach (var s in states)
            {
                snap[s] = GetActionArray(s);
            }
            return snap;
        }

        public virtual void MergeInto(Q<T> from, EvalMethodType methodType)
        {
            throw new NotImplementedException();
        }

        public virtual Q<T> Clone()
        {
            throw new NotImplementedException();
        }

        public virtual uint AddState(T stateKey)
        {
            throw new NotImplementedException();
        }

        public virtual bool RemoveState(T stateKey)
        {
            throw new NotImplementedException();
        }

        public virtual double GetValue(T stateKey, int action)
        {
            throw new NotImplementedException();
        }

        public virtual void SetValue(T stateKey, int action, double v)
        {
            throw new NotImplementedException();
        }

        public abstract QAction ArgMax(T stateKey);

        public abstract QAction ArgMin(T stateKey);

        public abstract double OnPolicyUpdate(T s, int a, T sprime, int aprime, double r, HyperParams hp);

        public abstract double OffPolicyUpdate(T s, int a, T sprime, double r, HyperParams hp, EvalMethodType evalType);

        public virtual void Dump(string label, int level, bool dumpData = false)
        {
            throw new NotImplementedException();
        }

        public virtual uint MapState(T stateKey)
        {
            throw new NotImplementedException();
        }
        public virtual int MapActionFromIndex(uint index)
        {
            throw new NotImplementedException();
        }
    }
}
