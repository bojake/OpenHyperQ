using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using HyperQ.Util.Licensing;

namespace HyperQ.Learners
{
    [Serializable]
    public class ClassicQQ : BaseQ<decimal>, ICheckpointable
    {
        private Q<decimal> _Q1;
        private Q<decimal> _Q2;
        private int _WhichQ = 0;
        private Random _random = QRandom.Instance.Ran;
        private Func<double, double, double> _BlendingFunction = null;

        public ClassicQQ(QActionSpace<int> actionSpace, Func<double> defaultValueAction = null, Func<double, double, double> blendingFunction = null, Random ran = null) : base(actionSpace)
        {
            FeatureGate.Require(HyperQFeatures.LearnerDoubleQ);
            _Q1 = new ClassicQ(actionSpace, defaultValueAction);
            _Q2 = new ClassicQ(actionSpace, defaultValueAction);
            if (defaultValueAction != null)
            {
                DefaultValueFunc = defaultValueAction;
            }
            if (ran != null)
            {
                _random = ran;
            }
            if (blendingFunction != null)
            {
                _BlendingFunction = blendingFunction;
            }
            else
            {
                _BlendingFunction = __default_blender;
            }
        }

        private double __default_blender(double q1, double q2)
        {
            return 0.5 * (q1 + q2);
        }
        /// <summary>
        /// Returns the shape of the Q using the result from Q1
        /// </summary>
        public override Tuple<uint, uint> Shape { get { return _Q1.Shape; } }
        /// <summary>
        /// Returns the averaged Q matrix in 2D format of the two Q's in this QQ.
        /// </summary>
        public override double[,] AsMatrix
        {
            get
            {
                // Start with Q1 as the master
                Q<decimal> c = _Q1.Clone();
                _Q2.MergeInto(c, EvalMethodType.Avg);
                double[,] q1 = c.AsMatrix;
                return (q1);
            }
        }

        #region Q Interface
        public override bool RemoveState(decimal stateKey)
        {
            return (_Q1.RemoveState(stateKey) && _Q2.RemoveState(stateKey));
        }
        public override uint AddState(decimal stateKey)
        {
            uint i1 = _Q1.AddState(stateKey);
            uint i2 = _Q2.AddState(stateKey);
            if (i1 != i2)
            {
                // OOps, error
                throw (new Exception($"State index for {stateKey} should be the same, but it was not consistent: i1={i1},i2={i2}"));
            }
            return (i1);
        }

        /// <summary>
        /// Returns the action index and Q value for that action that is the maximum average value from all
        /// of the Q learners in this ensemble.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>Tuple(action,Q)</returns>
        public override QAction ArgMax(decimal stateKey)
        {
            AddState(stateKey);
            double[] r1 = _Q1.GetActionArray(stateKey);
            double[] r2 = _Q2.GetActionArray(stateKey);
            double dmax = _BlendingFunction(r1[0],r2[0]);
            int imax = 0;
            for (int i = 1; i < r1.Length; i++)
            {
                double d = _BlendingFunction(r1[i],r2[i]);
                if (d > dmax)
                {
                    dmax = d;
                    imax = i;
                }
            }
            return new QAction(imax, dmax,(uint)imax);
        }

        /// <summary>
        /// Returns the action index and Q value for that action that is the minimum average value from all
        /// of the Q learners in this ensemble.
        /// </summary>
        /// <param name="stateKey"></param>
        /// <returns>Tuple(action,Q)</returns>
        public override QAction ArgMin(decimal stateKey)
        {
            AddState(stateKey);
            double[] r1 = _Q1.GetActionArray(stateKey);
            double[] r2 = _Q2.GetActionArray(stateKey);
            double dmin = _BlendingFunction(r1[0],r2[0]);
            int imin = 0;
            for (int i = 1; i < r1.Length; i++)
            {
                double d = _BlendingFunction(r1[i],r2[i]);
                if (d < dmin)
                {
                    dmin = d;
                    imin = i;
                }
            }
            return new QAction(imin, dmin, (uint)imin);
        }

        public override Q<decimal> Clone()
        {
            ClassicQQ c = new ClassicQQ(ActionSpace, DefaultValueFunc);
            c._Q1 = _Q1.Clone();
            c._Q2 = _Q2.Clone();
            return (c);
        }

        public override void Dump(string label = "Double Q", int level = 0, bool dumpData = false)
        {
            Console.WriteLine("=~{0}:Q1~=",label);
            _Q1.Dump(level: level, dumpData: dumpData);
            Console.WriteLine("=~{0}:Q2~=",label);
            _Q2.Dump(level: level, dumpData: dumpData);
        }

        public override double[] GetActionArray(decimal stateKey)
        {
            AddState(stateKey);
            double[] r1 = _Q1.GetActionArray(stateKey);
            double[] r2 = _Q2.GetActionArray(stateKey);
            for (int i = 0; i < r1.Length; i++)
            {
                r1[i] = _BlendingFunction(r1[i],r2[i]);
            }
            return (r1);
        }

        public override double GetValue(decimal stateKey, int action)
        {
            double[] r1 = _Q1.GetActionArray(stateKey);
            double[] r2 = _Q2.GetActionArray(stateKey);
            return 0.5 * (r1[action] + r2[action]);
        }

        public override uint MapState(decimal stateKey)
        {
            return AddState(stateKey);
        }

        public override void MergeInto(Q<decimal> from, EvalMethodType methodType = EvalMethodType.Max)
        {
            if (from is ClassicQQ)
            {
                ClassicQQ fromQ = (ClassicQQ)from;
                _Q1.MergeInto(fromQ._Q1, methodType);
                _Q2.MergeInto(fromQ._Q2, methodType);
            }
            else
            {
                _Q1.MergeInto(from, methodType);
                _Q2.MergeInto(from, methodType);
            }
        }

        public override void SetValue(decimal stateKey, int action, double v)
        {
            AddState(stateKey);
            if (_WhichQ == 0)
            {
                _Q1.SetValue(stateKey, action, v);
            }
            else
            {
                _Q2.SetValue(stateKey, action, v);
            }
            NextQ();
        }

        /// <summary>
        /// Performs the on-policy update calculation
        /// </summary>
        /// <param name="s">Current state</param>
        /// <param name="a">Current action</param>
        /// <param name="sprime">Derived state from S(a)</param>
        /// <param name="aprime">Derived action for S(a)</param>
        /// <param name="r">The reward for this action</param>
        /// <param name="gamma">Decay parameter</param>
        /// <param name="alpha">Learning parameter</param>
        /// <returns></returns>
        public override double OnPolicyUpdate(decimal s, int a, decimal sprime, int aprime, double r, HyperParams hp)
        {
            AddState(s);
            AddState(sprime);
            // Canonical Double-Q (Hasselt 2010): randomly select which Q to update,
            // use the other Q for value evaluation to reduce maximization bias.
            NextQ(-1, true);
            Q<decimal>[] qq = { _Q1, _Q2 };
            Q<decimal> q1 = qq[_WhichQ];
            Q<decimal> q2 = qq[(_WhichQ + 1) % 2];
            double prime_v = q2.GetValue(sprime, aprime);
            double curr_v = q1.GetValue(s, a);
            double baseline = QUpdateCore.Baseline(q1.GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            q1.SetValue(s, a, new_v);
            return new_v;
        }
        public override double OffPolicyUpdate(decimal s, int a, decimal sprime, double r, HyperParams hp,EvalMethodType evalType=EvalMethodType.Max)
        {
            AddState(s);
            AddState(sprime);
            // Canonical Double-Q (Hasselt 2010): randomly select which Q to update,
            // use q1 for action selection, q2 for value evaluation.
            NextQ(-1, true);
            Q<decimal>[] qq = { _Q1, _Q2 };
            Q<decimal> q1 = qq[_WhichQ];
            Q<decimal> q2 = qq[(_WhichQ + 1) % 2];
            int aprime = QUpdateCore.SelectGreedyAction(sprime, a, evalType, q1.ArgMax, q1.ArgMin);
            double prime_v = q2.GetValue(sprime, aprime);
            double curr_v = q1.GetValue(s, a);
            double baseline = QUpdateCore.Baseline(q1.GetActionArray(s));
            double new_v = QUpdateCore.UpdateAndTrack(curr_v, prime_v, r, hp, Advantage, baseline);
            q1.SetValue(s, a, new_v);
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
                _WhichQ = _random.Next(2);
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
        public virtual Q<decimal> CurrentQ()
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
        public virtual Q<decimal> AltQ()
        {
            if (_WhichQ == 0)
            {
                return (_Q2);
            }
            return (_Q1);
        }
        public override double this[decimal state, int action]
        {
            get
            {
                return CurrentQ().GetValue(state, action);
            }
            set
            {
                CurrentQ().SetValue(state, action, value);
            }
        }

        // ── ICheckpointable ──

        public int CheckpointVersion { get { return 1; } }

        public void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            writer.Write(_WhichQ);
            CheckpointIO.Checkpointable(_Q1, "The first Q table").SaveCheckpoint(writer);
            CheckpointIO.Checkpointable(_Q2, "The second Q table").SaveCheckpoint(writer);
        }

        public void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            _WhichQ = reader.ReadInt32();
            CheckpointIO.Checkpointable(_Q1, "The first Q table").LoadCheckpoint(reader);
            CheckpointIO.Checkpointable(_Q2, "The second Q table").LoadCheckpoint(reader);
        }
    }
}
