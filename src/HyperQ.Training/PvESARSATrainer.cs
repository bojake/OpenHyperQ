using HyperQ.Learners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Env;
using HyperQ.Util;

namespace HyperQ.Training
{
    /// <summary>
    /// Controls how the Dyna replay selects (s,a) pairs for planning.
    /// </summary>
    public enum DynaSweepMode
    {
        /// <summary>Uniform random sampling (classic Dyna-Q, Sutton 1991).</summary>
        Uniform,
        /// <summary>Priority queue ranked by TD error magnitude (Moore &amp; Atkeson 1993).</summary>
        Prioritized
    }

    /// <summary>
    /// Implements the SARSA {(S,a)<-R : (S',a')} evaluation method, supporting on-policy and off-policy evaluation.
    /// </summary>
    /// <typeparam name="T">The type for the state</typeparam>
    [Serializable]
    public partial class PvESARSATrainer<T> : ITrainingEvents
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
        private QMemory<T,ScalarReward> _Memory = null;
        public DynaState<T,ScalarReward> Dyna { get; private set; } = null;
        public bool DynaEnabled { get; set; } = false;
        public double DynaFrequency { get; private set; } = 0.5;
        public double DynaUpdateFrequency { get; private set; } = 0.8;
        public DynaSweepMode SweepMode { get; set; } = DynaSweepMode.Uniform;
        public int HysteresisSteps { get; private set; } = 10;
        public bool WarmupEnabled { get; set; } = false;
        public bool InTraining { get; set; } = true;
        private List<Tuple<T, QAction, T, QAction, ScalarReward>> _Episode = new List<Tuple<T, QAction, T, QAction, ScalarReward>>();
        protected Q<T> _Q;
        public IActionSelector<T> ActionSelector { get; set; } = null;
        protected QRandom _random;
        public int MaxIterations { get; set; } = 0;

        /// <param name="ran">The source of the trainer's own draws (Dyna, memory replay); the learner's action
        /// space's when null.</param>
        public PvESARSATrainer(Q<T> q, QEvalType evalType = QEvalType.OnPolicy, IActionSelector<T> actionSelector = null, QRandom ran = null)
        {
            _Q = q ?? throw new ArgumentNullException(nameof(q));
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
        public Q<T> Q { get { return _Q; } }

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
        public virtual QEvaluator<T, ScalarReward> MaxEvaluator()
        {
            QEvaluator<T, ScalarReward> s = new QEvaluator<T, ScalarReward>(_Q, new MaxActionSelector<T>(_Q.ActionSpace));
            return (s);
        }
        /// <summary>
        /// Enables the replay memory cache
        /// </summary>
        /// <param name="memory">The memory cache used to draw replay slices</param>
        public virtual void EnableMemory(QMemory<T,ScalarReward> memory)
        {
            _Memory = memory;
        }
        /// <summary>
        /// Enables the Dyna replay
        /// </summary>
        /// <param name="dyna">The dyna state engine</param>
        /// <param name="freq">The frequency to apply the dyna replay engine</param>
        public void EnableDyna(DynaState<T, ScalarReward> dyna, double freq = 0.5, DynaSweepMode mode = DynaSweepMode.Uniform)
        {
            DynaEnabled = true;
            Dyna = dyna;
            DynaFrequency = freq;
            SweepMode = mode;
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
        public virtual void Episode(IPvEEnv<T,ScalarReward> env, HyperParams hp)
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

        private void UpdateDyna(Tuple<T, QAction> last_sa, T sprime, ScalarReward r, HyperParams hp)
        {
            if (!DynaEnabled)
                return;
            double x = _random.Ran.Next();
            if (x < DynaUpdateFrequency)
                Dyna.Update(last_sa.Item1, last_sa.Item2, sprime, r, hp);
            if (x < DynaFrequency)
            {
                if (SweepMode == DynaSweepMode.Prioritized)
                {
                    // Seed the priority queue with the current transition's TD error
                    double tdError = ComputeTDError(last_sa.Item1, last_sa.Item2.Action, sprime, (double)r, hp);
                    var seedEntry = new DynaEntry<T>(last_sa.Item1, last_sa.Item2);
                    Dyna.InsertPriority(seedEntry, tdError);
                    HallucinatePrioritized(hp);
                }
                else
                {
                    Hallucinate(hp);
                }
            }
        }

        /// <summary>
        /// Computes |r + γ·max_a' Q(s',a') − Q(s,a)| for priority queue seeding.
        /// </summary>
        private double ComputeTDError(T s, int a, T sprime, double r, HyperParams hp)
        {
            QAction maxAction = _Q.ArgMax(sprime);
            double maxQSprime = maxAction?.Item2 ?? 0.0;
            double currentQ = _Q.GetValue(s, a);
            return Math.Abs(r + hp.Gamma * maxQSprime - currentQ);
        }

        private void Remember(T s, QAction a, T sprime, QAction aprime, double r)
        {
            _Memory.Remember(s, a, sprime, aprime, r);
        }

        protected virtual bool ReplayCallback(QMemoryCell<T,ScalarReward> cell, HyperParams hp)
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

        protected virtual bool NegativeReplayCallback(QMemoryCell<T,ScalarReward> cell, HyperParams hp)
        {
            if (cell.r < 0.0)
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

        protected virtual bool PositiveReplayCallback(QMemoryCell<T,ScalarReward> cell, HyperParams hp)
        {
            if(cell.r > 0.0)
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
        public virtual void Reminisce(HyperParams hp, int count = 0, ReminisceType howToRemember=ReminisceType.Any)
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
            if (Dyna == null || Dyna.DynaIterations == 0)
            {
                return;
            }
            for (int i = 0; i < Dyna.DynaIterations; i++)
            {
                DynaEntry<T> d = (DynaEntry<T>)Dyna.RandomSA(_random);
                Tuple<T, ScalarReward> sample = Dyna.Sample(d);
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

        /// <summary>
        /// Prioritized sweeping (Moore &amp; Atkeson 1993): processes entries in
        /// TD-error order and propagates value changes backward through the
        /// learned transition model.
        /// </summary>
        protected virtual void HallucinatePrioritized(HyperParams hp)
        {
            if (Dyna == null || Dyna.DynaIterations == 0)
                return;

            for (int i = 0; i < Dyna.DynaIterations && Dyna.HasPriority; i++)
            {
                IDynaEntry entry = Dyna.PopPriority();
                DynaEntry<T> d = entry as DynaEntry<T>;
                if (d == null)
                    continue;

                Tuple<T, ScalarReward> sample;
                try { sample = Dyna.Sample(d); }
                catch (KeyNotFoundException) { continue; } // stale entry, skip

                T updatedState = d.Item1;

                // Update Q using the model
                if (_EvalType == QEvalType.OnPolicy)
                {
                    int ax = _Q.ActionSpace.RandomAction(false);
                    _Q.OnPolicyUpdate(updatedState, d.Item2.Action, sample.Item1, ax, sample.Item2, hp);
                }
                else
                {
                    _Q.OffPolicyUpdate(updatedState, d.Item2.Action, sample.Item1, sample.Item2, hp, EvalMethodType.Max);
                }

                // Backward propagation: for each (s̄, ā) predicted to lead to updatedState,
                // recompute TD error and insert into the priority queue if significant.
                foreach (IDynaEntry pred in Dyna.GetPredecessors(updatedState))
                {
                    DynaEntry<T> p = pred as DynaEntry<T>;
                    if (p == null)
                        continue;

                    if (!Dyna.TryGetReward(pred, out ScalarReward predReward))
                        continue;

                    // TD error for the predecessor: |R(s̄,ā) + γ·max_a' Q(updatedState, a') − Q(s̄, ā)|
                    double predTdError = ComputeTDError(p.Item1, p.Item2.Action, updatedState, (double)predReward, hp);
                    Dyna.InsertPriority(pred, predTdError);
                }
            }
        }

        #region Warmups
        public virtual void Warmup(IPvEEnv<T, ScalarReward> env, HyperParams hp, int n_episodes = 1000, IActionSelector<T> warmupSelector = null)
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
        protected virtual void OnPolicyEpisode(IPvEEnv<T, ScalarReward> env, HyperParams hp)
        {
            bool do_hysteresis = (hp.Hysteresis > 0.0);
            bool done = false;
            env.Reset();
            OnEpisodeStart?.Invoke();
            if (!WarmupEnabled)
                ActionSelector.StartEpisode();
            _Q.ResetAdvantageTrace();
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
                EnvResult<ScalarReward> result = env.Step(a);
                if (!WarmupEnabled)
                {
                    env.Metrics.EndAction(a,ActionSelector.LastActionWasRandom);
                    env.Metrics.NumberOfStatesVisited = _Q.Shape.Item1;
                }
                ScalarReward r = result.Reward;
                done = result.Done;
                T sprime = env.Discretize();
                QAction aprime = ActionSelector.SelectAction(_Q, sprime, hp.Epsilon);
                if (!WarmupEnabled)
                    env.Metrics.StartUpdate();
                _Q.OnPolicyUpdate(s, a.Action, sprime, aprime.Action, r, hp);
                ApplyAdvantageForStep(s, a, iter, hp);
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
                                Tuple<T, QAction, T, QAction, ScalarReward> hh = _Episode[t];
                                if (h_factor > 0.0)
                                {
                                    _Q.OnPolicyUpdate(hh.Item1, hh.Item2.Action, hh.Item3, hh.Item4.Action, hh.Item5 * h_factor, hp);
                                }
                            }
                            _Episode.Add(new Tuple<T, QAction, T, QAction, ScalarReward>(s, a, sprime, aprime, r));
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
                    env.Metrics.TotalReward.Add((IReward)r);
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
        protected virtual void OffPolicyEpisode(IPvEEnv<T, ScalarReward> env, HyperParams hp)
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
            _Q.ResetAdvantageTrace();
            while (!done)
            {
                if (!WarmupEnabled)
                    env.Metrics.StartAction();
                OnStepStart?.Invoke();
                QAction a = ActionSelector.SelectAction(_Q, s, hp.Epsilon);
                EnvResult<ScalarReward> result = env.Step(a);
                ScalarReward r = result.Reward;
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
                ApplyAdvantageForStep(s, a, iter, hp);
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
                                Tuple<T, QAction, T, QAction, ScalarReward> hh = _Episode[t];
                                if (h_factor > 0.0)
                                {
                                    double new_v = _Q.OffPolicyUpdate(hh.Item1, hh.Item2.Action, hh.Item3, hh.Item5*h_factor, hp, EvalMethodType.Max);
                                }
                            }
                            _Episode.Add(new Tuple<T, QAction, T, QAction, ScalarReward>(s, a, sprime, null, r));
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
                    env.Metrics.TotalReward.Add((IReward)r);
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

        /// <summary>
        /// Hands the action selector the advantage of the step just updated. The learner's Advantage trace
        /// holds Q(s,a) minus the state's mean action value, scaled to unit magnitude over the episode.
        /// Called inline during both on-policy and off-policy episodes.
        /// </summary>
        private void ApplyAdvantageForStep(T s, QAction a, int iter, HyperParams hp)
        {
            ActionSelector.ApplyAdvantage(_Q.MapState(s), a, _Q.Advantage[iter], hp);
        }
    }
}
