using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;

namespace HyperQ.Learners
{
    [Serializable]
    public class MappedQQ<T> : BaseQ<T>
    {
        private QRandom _random;
        private MappedQ<T> _Q1;
        private MappedQ<T> _Q2;
        private IndexMapper<T> _StateMap = new IndexMapper<T>(0);
        private Dictionary<T, QArg<double>> _StateArgs = new Dictionary<T, QArg<double>>();
        private int _WhichQ = 0;

        public MappedQQ(QActionSpace<int> actionSpace, Func<double> defaultValueAction = null, QRandom ran = null) : base(actionSpace)
        {
            _Q1 = new MappedQ<T>(actionSpace, _StateMap, defaultValueAction);
            _Q2 = new MappedQ<T>(actionSpace, _StateMap, defaultValueAction);
            if (defaultValueAction != null)
            {
                DefaultValueFunc = defaultValueAction;
            }
            _random = ran ?? QRandom.Instance;
        }
        public override double[,] AsMatrix
        {
            get
            {
                return (makeQ());
            }
        }

        private double[,] makeQ()
        {
            double[,] q = new double[_StateMap.MappingCount, ActionSpace.MaximumNumberOfActions];
            for (uint j = 0; j < _StateMap.MappingCount; j++)
            {
                double[] r = GetActionArray(_StateMap.From(j));
                for (int i = 0; i < r.Length; i++)
                {
                    q[j, i] = r[i];
                }
            }
            return (q);
        }

        #region Q Interface
        public override uint MapState(T stateKey)
        {
            return _StateMap[stateKey];
        }
        public override Tuple<uint, uint> Shape { get { return _Q1.Shape; } }

        public override bool RemoveState(T stateKey)
        {
            // The state map is shared between the Q constituents, so only need to add it once.
            return (_Q1.RemoveState(stateKey));
        }
        public override uint AddState(T stateKey)
        {
            uint i1 = _Q1.AddState(stateKey);
            uint i2 = _Q2.AddState(stateKey);
            if (i1 != i2)
            {
                // OOps, error
                throw (new Exception("State index for " + stateKey + " should be the same, but it was not consistent: i1=" + i1 + ",i2=" + i2));
            }
            return (i1);
        }

        public override double[] GetActionArray(T stateKey)
        {
            double[] r = new double[ActionSpace.MaximumNumberOfActions];
            QMappedActionSpace<int> mapped = ActionSpace as QMappedActionSpace<int>;
            for (uint i = 0; i < r.Length; i++)
            {
                if (mapped.IsValidIndex(i))
                {
                    r[i] = GetValue(stateKey, mapped.FromIndex(i)); // 0.5 * (_Q1.GetValue(stateKey, i) + _Q2.GetValue(stateKey, i));
                }
                else
                    r[i] = DefaultValueFunc();
            }
            return (r);
        }
        /// <summary>
        /// Returns the action index and Q value for that action that is the maximum average value from all
        /// of the Q learners in this ensemble.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>Tuple(action,Q)</returns>
        public override QAction ArgMax(T stateKey)
        {
            AddState(stateKey);
            if (_StateArgs.ContainsKey(stateKey))
            {
                QArg<double> arg = _StateArgs[stateKey];
                return new QAction(arg.MaxIndex, arg.Max, ActionSpace.ToIndex(arg.MaxIndex));
            }
            double[] r = GetActionArray(stateKey);
            double dmax = r[0];
            uint imax = 0;
            for (uint i = 1; i < r.Length; i++)
            {
                if (r[i] > dmax)
                {
                    dmax = r[i];
                    imax = i;
                }
            }
            QMappedActionSpace<int> mapped = ActionSpace as QMappedActionSpace<int>;
            if (!mapped.IsValidIndex(imax))
                return null;
            int amax = ActionSpace.FromIndex(imax);
            _StateArgs[stateKey] = new QArg<double>(dmax, amax);
            return new QAction(amax, dmax, imax);
        }

        /// <summary>
        /// Returns the action index and Q value for that action that is the minimum average value from all
        /// of the Q learners in this ensemble.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>Tuple(action,Q)</returns>
        public override QAction ArgMin(T stateKey)
        {
            AddState(stateKey);
            if (_StateArgs.ContainsKey(stateKey))
            {
                QArg<double> arg = _StateArgs[stateKey];
                return new QAction(arg.MinIndex, arg.Min, ActionSpace.ToIndex(arg.MinIndex));
            }
            double[] r = GetActionArray(stateKey);
            double dmin = r[0];
            uint imin = 0;
            for (uint i = 1; i < r.Length; i++)
            {
                if (r[i] < dmin)
                {
                    dmin = r[i];
                    imin = i;
                }
            }
            QMappedActionSpace<int> mapped = ActionSpace as QMappedActionSpace<int>;
            if (!mapped.IsValidIndex(imin))
                return null;
            int amin = ActionSpace.FromIndex(imin);
            _StateArgs[stateKey] = new QArg<double>(dmin, amin);
            return new QAction(amin, dmin, imin);
        }

        public override Q<T> Clone()
        {
            MappedQQ<T> c = new MappedQQ<T>(ActionSpace.Clone(), DefaultValueFunc);
            c._Q1 = _Q1.Clone() as MappedQ<T>;
            c._Q2 = _Q2.Clone() as MappedQ<T>;
            return (c);
        }

        public override void Dump(string label = "Mapped QQ", int level = 0, bool dumpData = false)
        {
            Console.WriteLine("=~Q1~=");
            _Q1.Dump(level: level, dumpData: dumpData);
            Console.WriteLine("=~Q2~=");
            _Q2.Dump(level: level, dumpData: dumpData);
        }

        /// <summary>
        /// Returns half of the sum of the two Q values for the given (s,a)
        /// </summary>
        /// <param name="stateKey">The state, in problem space</param>
        /// <param name="action">The action, in problem space</param>
        /// <returns></returns>
        public override double GetValue(T stateKey, int action)
        {
            double q1 = 0.0;
            if (_Q1.IsKnownState(stateKey) && _Q1.IsKnownAction(action))
            {
                q1 = _Q1[stateKey, action];
            }
            else
            {
                if (DefaultValueFunc != null)
                {
                    q1 = DefaultValueFunc();
                }
            }
            double q2 = 0.0;
            if (_Q2.IsKnownState(stateKey) && _Q2.IsKnownAction(action))
            {
                q2 = _Q2[stateKey, action];
            }
            else
            {
                if (DefaultValueFunc != null)
                {
                    q2 = DefaultValueFunc();
                }
            }
            return 0.5 * (q1 + q2);
        }

        public override void MergeInto(Q<T> from, EvalMethodType methodType = EvalMethodType.Max)
        {
            if (from is MappedQQ<T>)
            {
                MappedQQ<T> fromQ = (MappedQQ<T>)from;
                _Q1.MergeInto(fromQ._Q1, methodType);
                _Q2.MergeInto(fromQ._Q2, methodType);
            }
            else
            {
                _Q1.MergeInto(from, methodType);
                _Q2.MergeInto(from, methodType);
            }
        }

        /// <summary>
        /// Sets the literal value for the given state in the Q, and then stores a min/max value
        /// in the state args that is the midpoint of the two Q values.
        /// </summary>
        /// <param name="stateKey">The state for this value, in problem space</param>
        /// <param name="action">The action, in problem space</param>
        /// <param name="v">The value of the Q at this (s,a)</param>
        public override void SetValue(T stateKey, int action, double v)
        {
            double d = 0.0;
            AddState(stateKey);
            if (_WhichQ == 0)
            {
                _Q1.SetValue(stateKey, action, v);
                d = _Q2.GetValue(stateKey, action);
            }
            else
            {
                _Q2.SetValue(stateKey, action, v);
                d = _Q1.GetValue(stateKey, action);
            }
            if (_StateArgs.ContainsKey(stateKey))
            {
                _StateArgs.Remove(stateKey);
            }
            /*
            if (!_StateArgs.ContainsKey(stateKey))
            {
                _StateArgs[stateKey] = new QArg<double>();
            }
            _StateArgs[stateKey].Set((v+d)*0.5, action);
            */
            NextQ();
        }

        /// <summary>
        /// Performs the on-policy update calculation
        /// </summary>
        /// <param name="s">Current state in problem space</param>
        /// <param name="a">Current action in problem space</param>
        /// <param name="sprime">Derived state from S(a)</param>
        /// <param name="aprime">Derived action for S(a)</param>
        /// <param name="r">The reward for this action</param>
        /// <param name="hp">The hyper parameters</param>
        /// <returns></returns>
        public override double OnPolicyUpdate(T s, int a, T sprime, int aprime, double r, HyperParams hp)
        {
            AddState(s);
            AddState(sprime);
            // Canonical Double-Q (Hasselt 2010), the same scheme as ClassicQQ and DoubleHyperQ: randomly
            // select which Q to update and evaluate the next state with the other one to reduce the
            // maximization bias. (This learner used to update the blended value of both tables.)
            NextQ(-1, true);
            MappedQ<T>[] qq = { _Q1, _Q2 };
            MappedQ<T> q1 = qq[_WhichQ];
            MappedQ<T> q2 = qq[(_WhichQ + 1) % 2];
            double prime_v = q2.GetValue(sprime, aprime);
            double curr_v = q1.GetValue(s, a);
            double baseline = QUpdateCore.Baseline(q1.GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            q1.SetValue(s, a, new_v);
            _StateArgs.Remove(s); // the cached arg min/max of the blended value is stale now
            return new_v;
        }
        /// <summary>
        /// Performs the off-policy update. The aprime in this update is derived from
        /// the Q values using the evalType method of evaluation.
        /// </summary>
        /// <param name="s">The state, in problem space</param>
        /// <param name="a">The action, in problem space</param>
        /// <param name="sprime">The new state from (s,a)</param>
        /// <param name="r">The reward for this update</param>
        /// <param name="hp">The hyper parameters</param>
        /// <param name="evalType">How the derived action is determined</param>
        /// <returns></returns>
        public override double OffPolicyUpdate(T s, int a, T sprime, double r, HyperParams hp, EvalMethodType evalType = EvalMethodType.Max)
        {
            AddState(s);
            AddState(sprime);
            // Canonical Double-Q (Hasselt 2010): q1 selects the greedy next action, q2 evaluates it.
            NextQ(-1, true);
            MappedQ<T>[] qq = { _Q1, _Q2 };
            MappedQ<T> q1 = qq[_WhichQ];
            MappedQ<T> q2 = qq[(_WhichQ + 1) % 2];
            int aprime = QUpdateCore.SelectGreedyAction(sprime, a, evalType, q1.ArgMax, q1.ArgMin);
            double prime_v = q2.GetValue(sprime, aprime);
            double curr_v = q1.GetValue(s, a);
            double baseline = QUpdateCore.Baseline(q1.GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            q1.SetValue(s, a, new_v);
            _StateArgs.Remove(s); // the cached arg min/max of the blended value is stale now
            return new_v;
        }
        #endregion
        /// <summary>
        /// Schedule the next Q to use, or set it explicitly.
        /// </summary>
        /// <param name="which">optionally specify which Q to use, either 0 or 1. This value is mod 2 so any value is safe</param>
        public virtual void NextQ(int which = -1, bool random = false)
        {
            if (random)
            {
                _WhichQ = _random.Ran.Next(2);
            }
            else
            {
                if (which == -1)
                {
                    _WhichQ = (_WhichQ + 1) % 2;
                }
                else
                {
                    _WhichQ = ((which % 2) + 2) % 2;
                }
            }
        }
        /// <summary>
        /// Returns the current selected Q
        /// </summary>
        /// <returns></returns>
        public virtual Q<T> CurrentQ()
        {
            if (_WhichQ == 0)
            {
                return (_Q1);
            }
            return (_Q2);
        }
        /// <summary>
        /// Returns the other Q instead of the current selected Q
        /// </summary>
        /// <returns></returns>
        public virtual Q<T> AltQ()
        {
            if (_WhichQ == 0)
            {
                return (_Q2);
            }
            return (_Q1);
        }
        public override double this[T state, int action]
        {
            get
            {
                return GetValue(state, action);
            }
            set
            {
                SetValue(state, action, value);
            }
        }
    }
}
