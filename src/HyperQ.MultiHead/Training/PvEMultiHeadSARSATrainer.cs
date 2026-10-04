using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.MultiHead.Eval;

namespace HyperQ.MultiHead.Training
{
    public class PvEMultiHeadSARSATrainer<T> : ITrainingEvents
    {
        public event Action OnEpisodeStart;
        public event Action OnEpisodeEnd;
        public event Action OnStepStart;
        public event Action OnStepEnd;
        public event Action OnDynaStart;
        public event Action OnDynaEnd;
        public event Action OnMemoryReplayStart;
        public event Action OnMemoryReplayEnd;
        protected QEvalType _EvalType = QEvalType.OnPolicy;
        private QMemory<T, MultiReward> _Memory = null;
        public DynaState<T,MultiReward> Dyna { get; private set; } = null;
        public bool DynaEnabled { get; set; } = false;
        public double DynaFrequency { get; private set; } = 0.5;
        public double DynaUpdateFrequency { get; private set; } = 0.8;
        public int HysteresisSteps { get; private set; } = 10;
        public bool WarmupEnabled { get; set; } = false;
        public bool InTraining { get; set; } = true;
        private List<Tuple<T, QAction, T, QAction, MultiReward>> _Episode = new List<Tuple<T, QAction, T, QAction, MultiReward>>();

        protected MultiHeadQLearner<T> _Q;
        public IActionSelector<T> ActionSelector { get; set; } = null;
        protected QRandom _random;
        public int MaxIterations { get; set; } = 0;

        /// <param name="ran">The source of the trainer's own draws (Dyna, memory replay); the learner's action
        /// space's when null.</param>
        public PvEMultiHeadSARSATrainer(MultiHeadQLearner<T> q, QEvalType evalType = QEvalType.OnPolicy, IActionSelector<T> actionSelector = null, QRandom ran = null)
        {
            _Q = q ?? throw new ArgumentNullException(nameof(q));
            _EvalType = evalType;
            _random = ran ?? q.ActionSpace.Random;
            ActionSelector = actionSelector;
            if (ActionSelector == null)
            {
                throw new ArgumentNullException("actionSelector", "Action selector can not be null");
            }
        }

        /// <summary>
        /// Returns the Q for this trainer
        /// </summary>
        public MultiHeadQLearner<T> Q { get { return _Q; } }

        /// <summary>
        /// Sets the action selector for this trainer. This is used in warm startup of the trainer when the default action
        /// selector should be overridden as a uniform selector instead of the min/max selector.
        /// </summary>
        /// <param name="selector"></param>

        public void SetSelector(IActionSelector<T> selector)
        {
            ActionSelector = selector;
        }

        /// <summary>
        /// Creates an evaluator instance for this trained model. A reference to the Q state is used in the
        /// returned evaluator.
        /// </summary>
        /// <returns></returns>
        public virtual MultiHeadQEvaluator<T, MultiReward> MaxEvaluator()
        {
            MultiHeadQEvaluator<T, MultiReward> s = new MultiHeadQEvaluator<T, MultiReward>(_Q, new MaxActionSelector<T>(_Q.ActionSpace));
            return (s);
        }
        /// <summary>
        /// Enables the replay memory cache
        /// </summary>
        /// <param name="memory">The memory cache used to draw replay slices</param>
        public virtual void EnableMemory(QMemory<T, MultiReward> memory)
        {
            _Memory = memory;
        }
        /// <summary>
        /// Enables the Dyna replay
        /// </summary>
        /// <param name="dyna">The dyna state engine</param>
        /// <param name="freq">The frequency to apply the dyna replay engine</param>
        public void EnableDyna(DynaState<T,MultiReward> dyna, double freq = 0.5)
        {
            DynaEnabled = true;
            Dyna = dyna;
            DynaFrequency = freq;
        }

        /// <summary>
        /// Sets the maximum number of episode steps to keep in the hysteresis buffer during an episode.
        /// </summary>
        /// <param name="steps"></param>
        public void SetHysteresisSteps(int steps)
        {
            HysteresisSteps = steps;
        }
        /// <summary>
        /// Runs an episode
        /// </summary>
        /// <param name="env">The world environment</param>
        /// <param name="hp">The hyper params for the Q</param>
        public virtual void Episode(IPvEEnv<T, MultiReward> env, HyperParams hp)
        {
            if (_EvalType == QEvalType.OnPolicy)
            {
                OnPolicyEpisode(env, hp);
            }
            else
            {
                OffPolicyEpisode(env, hp);
            }
        }

        private void UpdateDyna(Tuple<T, QAction> last_sa, T sprime, MultiReward r, HyperParams hp)
        {
            if (!DynaEnabled)
                return;
            double x = _random.Ran.NextDouble();
            if (x < DynaUpdateFrequency)
                Dyna.Update(last_sa.Item1, last_sa.Item2, sprime, r, hp);
            if (x < DynaFrequency)
            {
                Hallucinate(hp);
            }
        }

        private void Remember(T s, QAction a, T sprime, QAction aprime, MultiReward r)
        {
            _Memory.Remember(s, a, sprime, aprime, r);
        }

        protected virtual bool ReplayCallback(QMemoryCell<T, MultiReward> cell, HyperParams hp)
        {
            if (_EvalType == QEvalType.OnPolicy)
            {
                _Q.OnPolicyUpdate(cell.s, cell.a.Action, cell.sprime, cell.aprime.Action, cell.r, hp);
            }
            else
            {
                _Q.OffPolicyUpdate(cell.s, cell.a.Action, cell.sprime, cell.r, hp, EvalMethodType.Max);
            }
            return true;
        }

        protected virtual bool NegativeReplayCallback(QMemoryCell<T, MultiReward> cell, HyperParams hp)
        {
            if (cell.r.IsNegative)
                if (_EvalType == QEvalType.OnPolicy)
                {
                    _Q.OnPolicyUpdate(cell.s, cell.a.Action, cell.sprime, cell.aprime.Action, cell.r, hp);
                }
                else
                {
                    _Q.OffPolicyUpdate(cell.s, cell.a.Action, cell.sprime, cell.r, hp, EvalMethodType.Max);
                }
            return true;
        }

        protected virtual bool PositiveReplayCallback(QMemoryCell<T, MultiReward> cell, HyperParams hp)
        {
            if (!cell.r.IsNegative)
                if (_EvalType == QEvalType.OnPolicy)
                {
                    _Q.OnPolicyUpdate(cell.s, cell.a.Action, cell.sprime, cell.aprime.Action, cell.r, hp);
                }
                else
                {
                    _Q.OffPolicyUpdate(cell.s, cell.a.Action, cell.sprime, cell.r, hp, EvalMethodType.Max);
                }
            return true;
        }

        /// <summary>
        /// Replays a slice of the memory
        /// </summary>
        /// <param name="hp">The hyper params</param>
        /// <param name="count">The size of the slice to replay</param>
        public virtual void Reminisce(HyperParams hp, int count = 0, ReminisceType howToRemember = ReminisceType.Any)
        {
            if (_Memory == null)
            {
                return;
            }
            OnMemoryReplayStart?.Invoke();
            switch (howToRemember)
            {
                case ReminisceType.Any:
                    _Memory.Playback(count, hp, ReplayCallback);
                    break;
                case ReminisceType.Positive:
                    _Memory.Playback(count, hp, PositiveReplayCallback);
                    break;
                case ReminisceType.Negative:
                    _Memory.Playback(count, hp, NegativeReplayCallback);
                    break;
            }
            OnMemoryReplayEnd?.Invoke();
        }

        /// <summary>
        /// Replays the last episode of memory
        /// </summary>
        /// <param name="hp"></param>
        public virtual void Obsess(HyperParams hp, int repeats = 1, ReminisceType howToRemember = ReminisceType.Any)
        {
            if (_Memory == null)
                return;
            OnMemoryReplayStart?.Invoke();
            while (repeats > 0)
            {
                switch (howToRemember)
                {
                    case ReminisceType.Any:
                        _Memory.ReplayEpisode(hp, ReplayCallback);
                        break;
                    case ReminisceType.Positive:
                        _Memory.ReplayEpisode(hp, PositiveReplayCallback);
                        break;
                    case ReminisceType.Negative:
                        _Memory.ReplayEpisode(hp, NegativeReplayCallback);
                        break;
                }
                repeats--;
            }
            OnMemoryReplayEnd?.Invoke();
        }

        protected virtual void Hallucinate(HyperParams hp)
        {
            // The model can still be empty on the first steps: a planning frequency above the model update
            // frequency plans on steps that did not update the model.
            if (Dyna == null || Dyna.DynaIterations == 0 || Dyna.Count == 0)
            {
                return;
            }
            for (int i = 0; i < Dyna.DynaIterations; i++)
            {
                DynaEntry<T> d = (DynaEntry<T>)Dyna.RandomSA(_random);
                Tuple<T, MultiReward> sample = Dyna.Sample(d);
                if (_EvalType == QEvalType.OnPolicy)
                {
                    int ax = _Q.ActionSpace.RandomAction(false);
                    uint ix = _Q.ActionSpace.ToIndex(ax);
                    _Q.OnPolicyUpdate(d.Item1, d.Item2.Action, sample.Item1, ax, sample.Item2, hp);
                }
                else
                {
                    _Q.OffPolicyUpdate(d.Item1, d.Item2.Action, sample.Item1, sample.Item2, hp, EvalMethodType.Max);
                }
                // Do not add the hallucination to the memory buffer.
            }
        }

        #region Warmups
        public virtual void Warmup(IPvEEnv<T, MultiReward> env, HyperParams hp, int n_episodes = 1000, IActionSelector<T> warmupSelector = null)
        {
            WarmupEnabled = true;
            IActionSelector<T> hold_selector = ActionSelector;
            IActionSelector<T> selector = warmupSelector ?? new UniformActionSelector<T>(_Q,_Q.ActionSpace);
            ActionSelector = selector;
            for (int i = 0; i < n_episodes; i++)
            {
                Episode(env, hp);
            }
            WarmupEnabled = false;
            ActionSelector = hold_selector;
        }
        #endregion

        #region OnPolicy
        protected virtual void OnPolicyEpisode(IPvEEnv<T, MultiReward> env, HyperParams hp)
        {
            bool do_hysteresis = (hp.Hysteresis > 0.0);
            bool done = false;
            env.Reset();
            OnEpisodeStart?.Invoke();
            if (!WarmupEnabled)
                ActionSelector.StartEpisode();
            // TODO: _Q.ResetAdvantageTrace();
            Tuple<T, QAction> last_sa = null;
            if (!WarmupEnabled)
            {
                env.Metrics.StartEpisode();
                if (_Memory != null)
                {
                    _Memory.StartEpisode();
                }
            }
            T s = env.Discretize();
            QAction a = ActionSelector.SelectAction(_Q, s, hp.Epsilon);
            int iter = 0;
            env.Render();
            while (!done)
            {
                if (!WarmupEnabled)
                    env.Metrics.StartAction();
                OnStepStart?.Invoke();
                EnvResult<MultiReward> result = env.Step(a);
                if (!WarmupEnabled)
                {
                    env.Metrics.EndAction(a, ActionSelector.LastActionWasRandom);
                    env.Metrics.NumberOfStatesVisited = _Q.Shape.Item1;
                }
                MultiReward r = result.Reward;
                done = result.Done;
                T sprime = env.Discretize();
                QAction aprime = ActionSelector.SelectAction(_Q, sprime, hp.Epsilon);
                if (!WarmupEnabled)
                    env.Metrics.StartUpdate();
                _Q.OnPolicyUpdate(s, a.Action, sprime, aprime.Action, r, hp);
                // TODO: ActionSelector.ApplyAdvantage(_Q.MapState(s), a, _Q.Advantage[iter], hp);
                if (!WarmupEnabled)
                {
                    env.Metrics.EndUpdate();
                    if (InTraining)
                    {
                        if (_Memory != null)
                            Remember(s, a, sprime, aprime, r); // off policy does not remember the prior action, we are off-policy...
                        if (do_hysteresis)
                        {
                            for (int t = _Episode.Count - 1; t >= 0; t--)
                            {
                                double h_factor = hp.Hysteresis.Value / (_Episode.Count - t);
                                Tuple<T, QAction, T, QAction, MultiReward> hh = _Episode[t];
                                if (h_factor > 0.0)
                                {
                                    _Q.OnPolicyUpdate(hh.Item1, hh.Item2.Action, hh.Item3, hh.Item4.Action, hh.Item5 * h_factor, hp);
                                }
                            }
                            _Episode.Add(new Tuple<T, QAction, T, QAction, MultiReward>(s, a, sprime, aprime, r));
                            if (_Episode.Count > HysteresisSteps)
                                _Episode.RemoveAt(0);
                        }
                    }
                }
                last_sa = new Tuple<T, QAction>(s, a);
                a = aprime;
                s = sprime;
                iter++;
                if (WarmupEnabled)
                {
                    if (iter > 500)
                    {
                        // Warmup stops after 500 steps in the episode
                        break;
                    }
                }
                else
                {
                    env.Metrics.TotalReward.Add(r);
                    if (DynaEnabled && InTraining)
                    {
                        env.Metrics.StartDyna();
                        OnDynaStart?.Invoke();
                        UpdateDyna(last_sa, sprime, r, hp);
                        env.Metrics.EndDyna();
                        OnDynaEnd?.Invoke();
                    }
                }
                env.Render();
                OnStepEnd?.Invoke();
                if (MaxIterations > 0 && MaxIterations < iter)
                {
                    // Bail out of this evaluation
                    break;
                }
            }
            _Episode.Clear();
            if (!WarmupEnabled)
            {
                if (_Memory != null)
                {
                    _Memory.EndEpisode();
                }
                env.Metrics.EndEpisode(null);
                ActionSelector.EndEpisode(env.Metrics.TotalReward);
            }
            OnEpisodeEnd?.Invoke();
        }
        #endregion

        #region OffPolicy
        protected virtual void OffPolicyEpisode(IPvEEnv<T, MultiReward> env, HyperParams hp)
        {
            bool do_hysteresis = (hp.Hysteresis > 0.0);
            bool done = false;
            env.Reset();
            OnEpisodeStart?.Invoke();
            if (!WarmupEnabled)
                ActionSelector.StartEpisode();
            Tuple<T, QAction> last_sa = null;
            if (!WarmupEnabled)
            {
                env.Metrics.StartEpisode();
                if (_Memory != null)
                {
                    _Memory.StartEpisode();
                }
            }
            T s = env.Discretize();
            int iter = 0;
            env.Render();
            // TODO: _Q.ResetAdvantageTrace();
            while (!done)
            {
                if (!WarmupEnabled)
                    env.Metrics.StartAction();
                OnStepStart?.Invoke();
                QAction a = ActionSelector.SelectAction(_Q, s, hp.Epsilon);
                EnvResult<MultiReward> result = env.Step(a);
                MultiReward r = result.Reward;
                if (!WarmupEnabled)
                {
                    env.Metrics.EndAction(a, ActionSelector.LastActionWasRandom);
                    env.Metrics.NumberOfStatesVisited = _Q.Shape.Item1;
                }
                done = result.Done;
                T sprime = env.Discretize();
                if (!WarmupEnabled)
                    env.Metrics.StartUpdate();
                _Q.OffPolicyUpdate(s, a.Action, sprime, r, hp, EvalMethodType.Max);
                // TODO: ActionSelector.ApplyAdvantage(_Q.MapState(s), a, _Q.Advantage[iter], hp);
                if (!WarmupEnabled)
                {
                    env.Metrics.EndUpdate();
                    if (InTraining)
                    {
                        if (_Memory != null)
                            Remember(s, a, sprime, null, r); // off policy does not remember the prior action, we are off-policy...
                        // Hysteresis
                        if (do_hysteresis)
                        {
                            for (int t = _Episode.Count - 1; t >= 0; t--)
                            {
                                double h_factor = hp.Hysteresis.Value / (_Episode.Count - t);
                                Tuple<T, QAction, T, QAction, MultiReward> hh = _Episode[t];
                                if (h_factor > 0.0)
                                {
                                    double new_v = _Q.OffPolicyUpdate(hh.Item1, hh.Item2.Action, hh.Item3, hh.Item5 * h_factor, hp, EvalMethodType.Max);
                                }
                            }
                            _Episode.Add(new Tuple<T, QAction, T, QAction, MultiReward>(s, a, sprime, null, r));
                            if (_Episode.Count > HysteresisSteps)
                                _Episode.RemoveAt(0);
                        }
                    }
                }
                last_sa = new Tuple<T, QAction>(s, a);
                s = sprime;
                iter++;
                if (WarmupEnabled)
                {
                    if (iter > 500)
                    {
                        // Warmup stops after 500 steps in the episode
                        break;
                    }
                }
                else
                {
                    env.Metrics.TotalReward.Add(r);
                    if (DynaEnabled && InTraining)
                    {
                        env.Metrics.StartDyna();
                        OnDynaStart?.Invoke();
                        UpdateDyna(last_sa, sprime, r, hp);
                        env.Metrics.EndDyna();
                        OnDynaEnd?.Invoke();
                    }
                }
                env.Render();
                OnStepEnd?.Invoke();
                if (MaxIterations > 0 && MaxIterations < iter)
                {
                    // Bail out of this evaluation
                    break;
                }
            }
            _Episode.Clear();
            if (!WarmupEnabled)
            {
                env.Metrics.EndEpisode(null);
                ActionSelector.EndEpisode(env.Metrics.TotalReward);
                if (_Memory != null)
                {
                    _Memory.EndEpisode();
                }
            }
            OnEpisodeEnd?.Invoke();
        }
        #endregion    
    }
}
