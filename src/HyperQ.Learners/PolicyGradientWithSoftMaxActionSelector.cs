using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyperQ.Learners
{
    /// <summary>
    /// A policy-gradient selector that uses RunningSoftMax for softmax parameterization
    /// instead of RunningLogit. Implements the classic log-softmax policy gradient update.
    /// </summary>
    [Serializable]
    public class PolicyGradientWithSoftmaxActionSelector<T> : UniformActionSelector<T>, IEntropyTracer
    {
        // Per-state softmax parameters
        private readonly Dictionary<uint, RunningSoftMax> _Theta = new Dictionary<uint, RunningSoftMax>();
        private List<double> _ProbabilityTrace = new List<double>();
        private double _LastEpisodeEntropy = double.NegativeInfinity;
        private readonly IStateMapper<T> _mapper;

        public PolicyGradientWithSoftmaxActionSelector(IStateMapper<T> mapper, QActionSpace<int> actionSpace) : base(mapper, actionSpace) {
            _mapper = mapper;
        }
        /// <summary>Convenience 1-arg ctor — creates internal MemoryBackedHyperMapper&lt;T&gt;.</summary>
        public PolicyGradientWithSoftmaxActionSelector(QActionSpace<int> actionSpace)
            : this(new MemoryMapperAdapter<T>(), actionSpace) { }

        public MinMaxActionEnum Mode { get; set; } = MinMaxActionEnum.Default;

        protected Dictionary<uint, RunningSoftMax> Theta
        {
            get
            {
                return _Theta;
            }
        }
        public virtual double LastActionProbability
        {
            get
            {
                return _ProbabilityTrace.Last();
            }
            protected set
            {
                _ProbabilityTrace.Add(value);
            }
        }

        public virtual double EntropyForLastEpisode
        {
            get
            {
                return _LastEpisodeEntropy;
            }
        }

        public virtual double CurrentEntropy
        {
            get
            {
                double ent = -_ProbabilityTrace
                .Where(p => p > 0) // Avoid log(0) errors
                .Sum(p => p * Math.Log(p));
                return ent;
            }
        }

        public virtual double[] Probabilities
        {
            get
            {
                return _ProbabilityTrace.ToArray();
            }
        }


        /// <summary>
        /// Apply advantage-based softmax policy gradient:
        ///   Δθ[a] = α * advantage * (1 - p[a])
        ///   Δθ[i≠a] = -α * advantage * p[i]
        /// </summary>
        public override void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
        {
            if (!_Theta.ContainsKey(s))
            {
                // initialize uniform logits = 0 => uniform softmax
                var sm = new RunningSoftMax();
                for (int i = 0; i < _actionSpace.MaximumNumberOfActions; i++)
                    sm[i] = 0.0;
                _Theta[s] = sm;
            }
            var softmax = _Theta[s];
            // Snapshot the probabilities: the gradient is taken at the policy before the step, and every
            // write renormalizes the others.
            int n = (int)_actionSpace.MaximumNumberOfActions;
            double[] p = new double[n];
            for (int i = 0; i < n; i++)
                p[i] = softmax[i];
            // update all actions
            // NOTE: the RunningSoftMax indexer reads back the probability but sets the logit, so the step has
            // to be written against Logit(i); "softmax[i] += delta" replaced the logit with p(i) + delta.
            for (int i = 0; i < n; i++)
            {
                if (i == a.Index)
                {
                    softmax[i] = softmax.Logit(i) + hp.Alpha * advantage * (1.0 - p[i]);
                }
                else
                {
                    softmax[i] = softmax.Logit(i) - hp.Alpha * advantage * p[i];
                }
            }
        }

        private uint GetPolicyAction(uint state_index, IArgMinMax<T> q)
        {
            LastActionWasRandom = false;
            RunningSoftMax rl = _Theta[state_index];
            if (Mode == MinMaxActionEnum.MostProbable)
            {
                uint a_max = 0;
                double p_max = -1.0;
                for (uint a = 0; a < _actionSpace.NumberOfKnownActions; a++)
                {
                    double p = rl[(int)a];
                    if (p > p_max)
                    {
                        p_max = p;
                        a_max = a;
                    }
                }
                LastActionProbability = p_max;
                return a_max;
            }
            else if (Mode == MinMaxActionEnum.LeastProbable)
            {
                uint a_min = 0;
                double p_min = 1.0;
                for (uint a = 0; a < _actionSpace.NumberOfKnownActions; a++)
                {
                    double p = rl[(int)a];
                    if (p < p_min)
                    {
                        p_min = p;
                        a_min = a;
                    }
                }
                LastActionProbability = p_min;
                return a_min;
            }
            else
            {
                RunningSoftMax softmax = _Theta[state_index];
                double roll = _actionSpace.Random.Ran.NextDouble();
                double cumulative = 0.0;
                for (uint a = 0; a < _actionSpace.NumberOfKnownActions; a++)
                {
                    double p = softmax[(int)a];
                    cumulative += p;
                    if (roll < cumulative)
                    {
                        LastActionProbability = p;
                        return a;
                    }
                }
            }
            // Fall through default behavior is to return a uniform random
            return base.randomAction(state_index,q);
            /*
            LastActionWasRandom = true;
            RandomActionCount++;
            return ((uint)_actionSpace.Random.Ran.Next((int)_actionSpace.MaximumNumberOfActions)); // fallback is the last action
            */
        }

        /// <summary>
        /// Sample action according to current softmax probabilities, fallback to uniform if not yet initialized.
        /// </summary>
        public override QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            // epsilon‐greedy fallback
            if (epsilon > 0.0)
            {
                if(_actionSpace.Random.Ran.NextDouble() < epsilon)
                {
                    var a = base.SelectAction(q, state, epsilon);
                    TelemetryCallback?.Invoke(state, a);
                    return a;
                }
            }
            uint s = _mapper.MapState(state);
            if (!_Theta.ContainsKey(s))
            {
                var a = base.SelectAction(q, state, epsilon);
                TelemetryCallback?.Invoke(state, a);
                return a;
            }
            uint ix = GetPolicyAction(s,q);
            var action = new QAction(_actionSpace.FromIndex(ix), 0.0, ix);
            TelemetryCallback?.Invoke(state, action);
            return action;
        }
    }
}
