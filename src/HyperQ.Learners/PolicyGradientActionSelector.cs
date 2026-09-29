using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    [Serializable]
    public class PolicyGradientActionSelector<T> : UniformActionSelector<T>, IEntropyTracer, ICheckpointable
    {
        /// <summary>
        /// Theta is the logit probabilities per state that define how the actions are selected
        /// </summary>
        private Dictionary<uint,RunningLogit> _Theta = new Dictionary<uint,RunningLogit>();
        private List<double> _ProbabilityTrace = new List<double>();
        private double _LastEpisodeEntropy = double.NegativeInfinity;
        private readonly IStateMapper<T> _stateMapper = null;
        /// <summary>
        /// Set the Mode to either MostProbable or LeastProbable during policy evaluation, otherwise use default for
        /// training mode.
        /// </summary>
        public PolicyGradientActionSelector(IStateMapper<T> mapper, QActionSpace<int> actionSpace) : base(mapper, actionSpace)
        {
            _stateMapper = mapper;
        }

        /// <summary>
        /// Retired: it gave the selector a state mapper of its own, but the trainer hands ApplyAdvantage the learner's
        /// state index, so once the two numberings diverge (a warm-up, a terminal next state, an evaluation episode)
        /// the policy learned for one state is applied to another. Pass the learner as the mapper instead.
        /// </summary>
        [Obsolete("Pass the learner as the state mapper: new PolicyGradientActionSelector<T>(learner, actionSpace). The trainer hands ApplyAdvantage the learner's state index, so a selector with a mapper of its own applies each learned policy to the wrong state.", true)]
        public PolicyGradientActionSelector(QActionSpace<int> actionSpace)
            : this(new MemoryMapperAdapter<T>(), actionSpace) { }


        protected Dictionary<uint, RunningLogit> Theta
        {
            get
            {
                return _Theta;
            }
        }

        public override void EndEpisode(RewardAccumulator reward)
        {
            base.EndEpisode(reward);
            _LastEpisodeEntropy = -_ProbabilityTrace
                .Where(p => p > 0) // Avoid log(0) errors
                .Sum(p => p * Math.Log(p));
            _ProbabilityTrace.Clear();
        }
        /// <summary>
        /// Applies the advantage of the last update for the current s,a event.
        /// </summary>
        /// <param name="s">The state as an index in the index space of the state space</param>
        /// <param name="a">The action</param>
        /// <param name="advantage">The advantage, or change, in the "value" of the update</param>
        /// <param name="hp">Hyper parameters, both Omega and Alpha are used</param>
        public override void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp)
        {
            RunningLogit logit = null;
            if (!_Theta.TryGetValue(s, out logit))
            {
                logit = new RunningLogit((int)_actionSpace.MaximumNumberOfActions);
                _Theta[s] = logit;
                for (int i = 0; i < _actionSpace.MaximumNumberOfActions; i++)
                {
                    logit[i] = -Math.Log(_actionSpace.MaximumNumberOfActions); // Softmax-like uniform prior, from o4 chat
                }
            }
            double objective = logit.Objective((int)a.Index, advantage, hp.Omega);
            // Softmax policy gradient: d log pi(a)/d theta_a = 1 - pi(a) and d log pi(a)/d theta_i = -pi(i).
            // The gradient is taken at the policy before this step, so snapshot the probabilities first.
            // The indexer reads and writes the logit, so each step is a plain += on it.
            RunningNormalizer priors = logit.LockPriors();
            int taken = (int)a.Index;
            double g = (1.0 - priors[taken]) * objective; // This should be cumulative
            logit[taken] += hp.Alpha * g;
            // -p*Objective is the counter update
            foreach(int i in priors.Keys)
            {
                if (i == taken)
                    continue;
                logit[i] -= hp.Alpha * priors[i] * objective * hp.PenaltyAnnealingFactor;
            }
        }
        public virtual double LastActionProbability { 
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
        /// Selects an action for a given state based on the policy defined by the logits in the _Theta dictionary.
        /// </summary>
        /// <param name="state_index">The index of the state in the state space.</param>
        /// <returns>
        /// The index of the selected action. If no action is selected based on the policy, a random action is returned as a fallback.
        /// </returns>
        /// <remarks>
        /// The method uses the logits stored in the RunningLogit instance for the given state to calculate action probabilities.
        /// A random number is generated to simulate a roll, and the cumulative probabilities are used to select an action.
        /// If the roll does not match any action (due to rounding or other issues), a random action is selected as a fallback.
        /// The probability of the selected action is stored in LastActionProbability.
        /// </remarks>
        private uint GetPolicyAction(uint state_index, IArgMinMax<T> q)
        {
            LastActionWasRandom = false;
            RunningLogit rl = _Theta[state_index];
            rl.LockPriors();
            if (ActionMode == MinMaxActionEnum.MostProbable)
            {
                uint a_max = 0;
                double p_max = -1.0;
                for (uint a = 0; a < _actionSpace.NumberOfKnownActions; a++)
                {
                    double p = rl.Probability((int)a);
                    if (p > p_max)
                    {
                        p_max = p;
                        a_max = a;
                    }
                }
                LastActionProbability = p_max;
                return a_max;
            }
            else if (ActionMode == MinMaxActionEnum.LeastProbable)
            {
                uint a_min = 0;
                double p_min = 1.0;
                for (uint a = 0; a < _actionSpace.NumberOfKnownActions; a++)
                {
                    double p = rl.Probability((int)a);
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
                double roll = _actionSpace.Random.Ran.NextDouble();
                double cumulative = 0f;
                for (uint a = 0; a < _actionSpace.NumberOfKnownActions; a++)
                {
                    double p = rl.Probability((int)a);
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
        /// Selects an action for a given state using the policy gradient approach.
        /// </summary>
        /// <param name="q">The Q object representing the state-action value function.</param>
        /// <param name="state">The current state for which an action is to be selected.</param>
        /// <param name="epsilon">
        /// The exploration parameter. Higher values increase the likelihood of selecting a random action.
        /// </param>
        /// <returns>
        /// A QAction object representing the selected action, its reward, and its index in the action space.
        /// </returns>
        /// <remarks>
        /// If the state is not present in the _Theta dictionary (i.e., no policy has been learned for the state),
        /// the method falls back to the base class's uniform random action selection.
        /// Otherwise, it uses the GetPolicyAction method to select an action based on the learned policy.
        /// </remarks>
        public override QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            //
            // Need a layered action selector for the layered hyperQ so that the advantage from previous "generalized" layers
            // can be used when a new state is encountered.
            //
            if (epsilon > 0.0)
            {
                double roll = _actionSpace.Random.Ran.NextDouble();
                if (roll < epsilon)
                    return base.SelectAction(q, state, epsilon);
            }
            uint idx = _stateMapper.MapState(state);
            if(!_Theta.ContainsKey(idx))
            {
                // Not in the theta, so pick a uniform random
                var a = base.SelectAction(q, state, epsilon);
                TelemetryCallback?.Invoke(state, a);
                return a;
            }
            uint ix = GetPolicyAction(idx,q);
            var action = new QAction(_actionSpace.FromIndex(ix), 0.0, ix);
            TelemetryCallback?.Invoke(state, action);
            return action;
        }

        public static double ComputePolicyEntropy(Dictionary<int, double> actionProbabilities)
        {
            return -actionProbabilities.Values
                .Where(p => p > 0) // Avoid log(0) errors
                .Sum(p => p * Math.Log(p));
        }

        // ── ICheckpointable ──

        public int CheckpointVersion => 1;

        public virtual void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            // θ dictionary: state_index → RunningLogit
            writer.Write(_Theta.Count);
            foreach (var kvp in _Theta)
            {
                writer.Write(kvp.Key);   // uint state index
                kvp.Value.SaveCheckpoint(writer);
            }
            writer.Write(RandomActionCount);
        }

        public virtual void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            int count = reader.ReadInt32();
            _Theta.Clear();
            for (int i = 0; i < count; i++)
            {
                uint key = reader.ReadUInt32();
                var logit = new RunningLogit();
                logit.LoadCheckpoint(reader);
                _Theta[key] = logit;
            }
            RandomActionCount = reader.ReadInt64();
        }
    }
}
