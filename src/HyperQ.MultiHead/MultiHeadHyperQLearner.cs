using HyperQ.Learners;
using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util.Licensing;

namespace HyperQ.MultiHead
{
    [Serializable]
    public class MultiHeadHyperQLearner<Tstate> : IMultiHeadHyperQLearner<Tstate>
    {
        public int HeadCount { get; }
        public double Lambda { get; set; }
        public virtual IHyperQ<Tstate>[] Heads { get; private set; }
        private MemoryBackedHyperMapper<Tstate> _StateMap = new MemoryBackedHyperMapper<Tstate>();
        public QActionSpace<int> ActionSpace { get { return Heads[0].ActionSpace; } }
        public IRewardScalarizer Scalarizer { get; set; }
        private Dictionary<QState<Tstate>, QArg<double>> _StateArgs = new Dictionary<QState<Tstate>, QArg<double>>();
        public string Label { get; set; } = "MultiHeadHyperQ";
        /// <summary>
        /// Constructor for the Q. You must define the actions for the Q and then a function to act as
        /// the lazy initializer of action data when new states are added. The default is to set the 
        /// action Q values to zero.
        /// </summary>
        /// <param name="nActions">The number of actions in the Q</param>
        /// <param name="defaultValueAction">The lazy action initializer</param>
        public MultiHeadHyperQLearner(int headCount, double lambda = 1.0, IRewardScalarizer scalarizer = null)
        {
            FeatureGate.Require(HyperQFeatures.MultiHead);
            if (headCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(headCount), "headCount must be greater than zero.");
            }
            HeadCount = headCount;
            Lambda = lambda;
            Scalarizer = scalarizer ?? CreateDefaultScalarizer(headCount);
            ValidateScalarizerDims(Scalarizer, headCount);
        }
        public MultiHeadHyperQLearner(Func<IHyperQ<Tstate>> qFactory, int headCount, double lambda = 1.0, IRewardScalarizer scalarizer = null)
        {
            FeatureGate.Require(HyperQFeatures.MultiHead);
            if (headCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(headCount), "headCount must be greater than zero.");
            }
            HeadCount = headCount;
            if (qFactory == null)
            {
                throw new ArgumentNullException("qFactory");
            }
            Heads = new IHyperQ<Tstate>[headCount];
            for (int i = 0; i < headCount; i++)
            {
                Heads[i] = qFactory();
            }
            Lambda = lambda;
            Scalarizer = scalarizer ?? CreateDefaultScalarizer(headCount);
            ValidateScalarizerDims(Scalarizer, headCount);
        }

        public MultiHeadHyperQLearner(IHyperQ<Tstate>[] heads, double lambda = 1.0, IRewardScalarizer scalarizer = null)
        {
            FeatureGate.Require(HyperQFeatures.MultiHead);
            if (heads == null)
            {
                throw new ArgumentNullException(nameof(heads));
            }
            if (heads.Length == 0)
            {
                throw new ArgumentException("heads can not be empty.", nameof(heads));
            }
            for (int i = 0; i < heads.Length; i++)
            {
                if (heads[i] == null)
                {
                    throw new ArgumentException("heads can not contain null entries.", nameof(heads));
                }
            }
            HeadCount = heads.Length;
            Heads = heads;
            Lambda = lambda;
            Scalarizer = scalarizer ?? CreateDefaultScalarizer(HeadCount);
            ValidateScalarizerDims(Scalarizer, HeadCount);
        }

        private static IRewardScalarizer CreateDefaultScalarizer(int headCount)
        {
            double[] w = new double[headCount];
            double wi = 1.0 / headCount;
            for (int i = 0; i < headCount; i++)
            {
                w[i] = wi;
            }
            return new LinearScalarizer(w);
        }

        private static void ValidateScalarizerDims(IRewardScalarizer scalarizer, int headCount)
        {
            if (scalarizer == null)
            {
                throw new ArgumentNullException(nameof(scalarizer));
            }
            if (scalarizer.Dims != headCount)
            {
                throw new ArgumentException("Scalarizer dimensions must match head count.", nameof(scalarizer));
            }
        }

        private void EnsureScalarizerDims()
        {
            ValidateScalarizerDims(Scalarizer, HeadCount);
        }
        public Tuple<uint, uint> Shape { get { return Heads[0].Shape; } }

        public virtual IMultiHeadHyperQLearner<Tstate> Clone()
        {
            MultiHeadHyperQLearner<Tstate> c = new MultiHeadHyperQLearner<Tstate>(HeadCount, Lambda, Scalarizer);
            c.Heads = new IHyperQ<Tstate>[HeadCount];
            for (int i = 0; i < HeadCount; i++)
            {
                c.Heads[i] = (IHyperQ<Tstate>)Heads[i].Clone();
            }
            return (c);
        }

        public virtual uint MapState(QState<Tstate> stateKey)
        {
            return _StateMap[stateKey.Enumerator];
        }
        public virtual void Link(MemoryBackedHyperMapper<Tstate> stateMap, QActionSpace<int> actionSpace)
        {
            _StateMap = stateMap;
            foreach(IHyperQ<Tstate> head in Heads)
                head.Link(stateMap, actionSpace);
        }
        /// <summary>
        /// Returns a copy of the actions for the given state key. The ordering of the array is not conserved.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns></returns>
        public virtual double[] GetActionArray(QState<Tstate> s)
        {
            EnsureScalarizerDims();
            Span<double> perHead = HeadCount <= 32
                ? stackalloc double[HeadCount]
                : new double[HeadCount]; // rare fallback
            List<double[]> arrays = new List<double[]>();
            for (int h = 0; h < HeadCount; h++)
            {
                IHyperQ<Tstate> head = Heads[h];
                double[] headActions = head.GetActionArray(s);
                arrays.Add(headActions);
            }
            double[] space = new double[arrays[0].Length];
            for (int i = 0; i < space.Length; i++)
            {
                for (int h = 0; h < HeadCount; h++)
                {
                    perHead[h] = arrays[h][i];
                }
                double score = Scalarizer.Score(perHead);
                space[i] = score;
            }
            return space;
        }
        public IHyperQ<Tstate> GetHead(int head)
        {
            return Heads[head];
        }

        public virtual uint AddState(QState<Tstate> stateKey)
        {
            uint idx = 0;
            foreach (IHyperQ<Tstate> q in Heads)
            {
                idx = q.AddState(stateKey);
                // TODO: we sholuld use a single state mapper for all of the heads.
            }
            return idx;
        }

        public virtual bool RemoveState(QState<Tstate> stateKey)
        {
            bool result = true;
            foreach (IHyperQ<Tstate> q in Heads)
                result &= q.RemoveState(stateKey);
            return result;
        }

        public QAction ArgMax(QState<Tstate> s)
        {
            AddState(s);
            if (_StateArgs.ContainsKey(s))
            {
                QArg<double> arg = _StateArgs[s];
                return new QAction(arg.MaxIndex, arg.Max, ActionSpace.ToIndex(arg.MaxIndex));
            }
            double[] r = GetActionArray(s);
            uint imax = 0;
            int a0 = ActionSpace.FromIndex(0);
            QArg<double> argAll = new QArg<double>(r[0], a0);
            for (uint i = 1; i < r.Length; i++)
            {
                int ai = ActionSpace.FromIndex(i);
                argAll.Set(r[i], ai);
                if (r[i] > r[imax])
                {
                    imax = i;
                }
            }
            int amax = ActionSpace.FromIndex(imax);
            _StateArgs[s] = argAll;
            return new QAction(amax, r[imax], imax);
        }

        public virtual QAction ArgMin(QState<Tstate> s)
        {
            AddState(s);
            if (_StateArgs.ContainsKey(s))
            {
                QArg<double> arg = _StateArgs[s];
                return new QAction(arg.MinIndex, arg.Min, ActionSpace.ToIndex(arg.MinIndex));
            }
            double[] r = GetActionArray(s);
            uint imin = 0;
            int a0 = ActionSpace.FromIndex(0);
            QArg<double> argAll = new QArg<double>(r[0], a0);
            for (uint i = 1; i < r.Length; i++)
            {
                int ai = ActionSpace.FromIndex(i);
                argAll.Set(r[i], ai);
                if (r[i] < r[imin])
                {
                    imin = i;
                }
            }
            int amin = ActionSpace.FromIndex(imin);
            _StateArgs[s] = argAll;
            return new QAction(amin, r[imin], imin);
        }

        public virtual double OnPolicyUpdate(QState<Tstate> s, int a, QState<Tstate> sprime, int aprime, IReward reward, HyperParams hp)
        {
            EnsureScalarizerDims();
            if (reward == null)
            {
                throw new ArgumentNullException(nameof(reward));
            }
            if (reward.Dims != HeadCount)
            {
                throw new ArgumentException("Reward dimensions must match head count.", nameof(reward));
            }
            AddState(s);
            AddState(sprime);
            Span<double> perHead = HeadCount <= 32
                ? stackalloc double[HeadCount]
                : new double[HeadCount]; // rare fallback
            for (int i = 0; i < Heads.Length; i++)
            {
                IHyperQ<Tstate> head = Heads[i];
                perHead[i] = head.OnPolicyUpdate(s, a, sprime, aprime, reward[i], hp);
            }
            double score = Scalarizer.Score(perHead);
            if (_StateArgs.ContainsKey(s))
            {
                QArg<double> arg = _StateArgs[s];
                arg.Set(score, a);
            }
            return score;
        }

        public virtual double OffPolicyUpdate(QState<Tstate> s, int a, QState<Tstate> sprime, IReward reward, HyperParams hp, EvalMethodType evalType)
        {
            EnsureScalarizerDims();
            if (reward == null)
            {
                throw new ArgumentNullException(nameof(reward));
            }
            if (reward.Dims != HeadCount)
            {
                throw new ArgumentException("Reward dimensions must match head count.", nameof(reward));
            }
            AddState(s);
            AddState(sprime);
            Span<double> perHead = HeadCount <= 32
                ? stackalloc double[HeadCount]
                : new double[HeadCount]; // rare fallback
            for (int i = 0; i < Heads.Length; i++)
            {
                IHyperQ<Tstate> head = Heads[i];
                perHead[i] = head.OffPolicyUpdate(s, a, sprime, reward[i], hp, evalType);
            }
            double score = Scalarizer.Score(perHead);
            if (_StateArgs.ContainsKey(s))
            {
                QArg<double> arg = _StateArgs[s];
                arg.Set(score, a);
            }
            return score;
        }
    }
    [Serializable]
    public class MultiHeadHyperQQ<T> : MultiHeadHyperQLearner<T>
    {
        public MultiHeadHyperQQ(QActionSpace<int> actionSpace, Func<double> defaultValueFunc = null, int headCount = 1, double lambda = 1.0, IRewardScalarizer scalarizer = null)
            : base(() => new DoubleHyperQ<T>(actionSpace, defaultValueFunc), headCount, lambda, scalarizer)
        {
        }
    }
    [Serializable]
    public class MultiHeadHyperQ<T> : MultiHeadHyperQLearner<T>
    {
        public MultiHeadHyperQ(QActionSpace<int> actionSpace, Func<double> defaultValueFunc = null, int headCount = 1, double lambda = 1.0, IRewardScalarizer scalarizer = null)
            : base(() => new SingleHyperQ<T>(actionSpace, defaultValueFunc), headCount, lambda, scalarizer)
        {
        }
    }
}
