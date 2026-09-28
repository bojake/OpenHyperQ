using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    [Serializable]
    public class RunningLogit : ICheckpointable
    {
        private const double FUDGE = 1e-8;
        private Dictionary<int,double> _logits = new Dictionary<int, double>(); //
        private RunningNormalizer _exps = new RunningNormalizer();
        private double _maxLogit = double.MinValue;
        private int _maxLogitIndex = -1;
        private Func<double> _initializer = null;
        private RunningNormalizer _priors = null;
        private bool _dirty_priors = true;

        public RunningLogit(int initialCapacity = 0, Func<double> initializer = null) 
        {
            if (initialCapacity > 0)
            {
                _initializer = initializer;
                // Zero them out
                for (int i = 0; i < initialCapacity; i++)
                {
                    _logits[i] = 0.0;
                    _exps[i] = 1.0;
                }
                if (initializer != null)
                {
                    for (int i = 0; i < initialCapacity; i++)
                    {
                        this[i] = initializer();
                    }
                }
            }
            _initializer = initializer;
        }

        /// <summary>
        /// Make a copy of the prior probabilities which is a snapshot of the current 
        /// probabilities.
        /// </summary>
        public RunningNormalizer LockPriors()
        {
            if (!_dirty_priors)
                return _priors;
            _priors = new RunningNormalizer(_exps);
            _dirty_priors = false;
            return _priors;
        }
        /// <summary>
        /// Lock all, or update a single, prior for the given index
        /// </summary>
        /// <param name="idx">the index to save</param>
        public void UpdatePrior(int idx)
        {
            if (_priors == null)
                LockPriors();
            else
                _priors[idx] = Probability(idx);
        }
        private double Clamp(double v, double low, double high)
        {
            if (v < low) return low;
            if (v > high) return high;
            return v;
        }
        /// <summary>
        /// Compute the advantage gradient using the priors and the current probability. Make
        /// sure you have called LockPriors before calling this function, otherwise, the
        /// priors will all be the same as the current probability
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        public double Objective(int idx, double weight, double omega)
        {
            double r = 1.0/(1.0+FUDGE);
            double p = Probability(idx); // equal probability by default
            if (_priors != null)
            {
                r = p / (_priors[idx]+FUDGE);
            }
            double unclipped = weight * r;
            double clipped = Clamp(r, 1.0 - omega, 1.0 + omega) * weight;
            double objective = Math.Min(unclipped, clipped);
            return objective;
        }

        /// <summary>
        /// Returns the probability of the given index: the softmax of the logits.
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        public double Probability(int idx)
        {
            return _exps[idx];
        }

        /// <summary>
        /// Gets or sets the logit for the given index (0 when it has never been set). Setting a logit updates
        /// the probabilities, which are read with <see cref="Probability"/>. The indexer reads and writes the
        /// same quantity, so <c>rl[i] += delta</c> is a step on the logit. (It used to read back the
        /// probability, so that step set the logit to the probability plus delta, which kept the policy-gradient
        /// selectors from learning from 2026-02 until 2026-09-27.)
        /// </summary>
        /// <param name="idx">The index to query/set</param>
        /// <returns></returns>
        public double this[int idx]
        {
            get
            {
                double v;
                return _logits.TryGetValue(idx, out v) ? v : 0.0;
            }
            set
            {
                bool isnew = _logits.ContainsKey(idx);
                _logits[idx] = value;
                if (_maxLogit < value || idx == _maxLogitIndex)
                {
                    // Have to recompute here because if the value is overwriting the last maxlogit
                    // value then the new maxLogit must be recomputed
                    //
                    _maxLogit = double.NegativeInfinity;
                    foreach(int i in _logits.Keys)
                    {
                        if (_logits[i] > _maxLogit)
                        {
                            _maxLogit = _logits[i];
                            _maxLogitIndex = i;
                        }
                    }
                    // Plain softmax. Subtracting the maximum logit is all the numerical protection the
                    // exponent needs: it can never overflow. (An earlier version also divided the difference
                    // by |max logit| whenever that exceeded 1, i.e. a temperature equal to the largest logit,
                    // which made the distribution flatter the more a policy learned and capped the best
                    // action at roughly even odds.)
                    foreach (int i in _logits.Keys)
                    {
                        _exps[i] = Math.Exp(_logits[i] - _maxLogit);
                    }
                }
                else
                {
                    // Ok to set the value here because the max is not changing
                    _exps[idx] = Math.Exp(value - _maxLogit);
                }
                _dirty_priors = true;
            }
        }

        // ── ICheckpointable ──

        public int CheckpointVersion => 1;

        public void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            writer.Write(_logits.Count);
            foreach (var kvp in _logits)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        public void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            int count = reader.ReadInt32();
            _logits.Clear();
            _exps = new RunningNormalizer();
            _maxLogit = double.MinValue;
            _maxLogitIndex = -1;
            _dirty_priors = true;
            _priors = null;

            // Restore logits through the indexer to rebuild derived caches
            for (int i = 0; i < count; i++)
            {
                int key = reader.ReadInt32();
                double val = reader.ReadDouble();
                this[key] = val;
            }
        }
    }
}
