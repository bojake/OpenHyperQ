using HyperQ.MACE;
using HyperQ.MACE.Eval;
using HyperQ.Learners;
using HyperQ.Env;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Collections.Specialized.BitVector32;
using HyperQ.Util;
using HyperQ.Util.Licensing;

namespace HyperQ.MACE.Training
{
    /// <summary>
    /// Collective trainer that will train a group of Q learners in the same environment, each 
    /// of which determines an element in a single-step action vector. Each step in the
    /// environment creates a vector of actions that are processed simultaneously in the env.
    /// The result of that processing is applied to all of the Q learners.
    /// Multi-Action Collective Environment
    /// </summary>
    /// <typeparam name="T">The type of the state</typeparam>
    [Serializable]
    public class MACEPvESARSATrainer<T,RT> : ITrainingEvents where RT : IReward
    {
        private List<MACEMind<T>> _Minds = new List<MACEMind<T>>();
        protected QRandom _random;
        public int MaxIterations { get; set; } = 0;
        protected QEvalType _EvalType = QEvalType.OnPolicy;
        public bool WarmupEnabled { get; set; } = false;
        private QMemory<T,RT> _Memory = null;
        public DynaState<T,RT> Dyna { get; private set; } = null;
        public int HysteresisSteps { get; private set; } = 10;
        public bool DynaEnabled { get; set; } = false;
        public double DynaFrequency { get; private set; } = 0.5;
        public double DynaUpdateFrequency { get; private set; } = 0.8;
        public bool InTraining { get; set; } = true;
        private List<Tuple<T, QAction[], T, QAction[], RT>> _Episode = new List<Tuple<T, QAction[], T, QAction[], RT>>();
        public event Action OnEpisodeStart;
        public event Action OnEpisodeEnd;
        public event Action OnStepStart;
        public event Action OnStepEnd;
        public event Action OnDynaStart;
        public event Action OnDynaEnd;
        public event Action OnMemoryReplayStart;
        public event Action OnMemoryReplayEnd;

        /// <param name="ran">The source of the trainer's own draws (Dyna, memory replay). The trainer has no
        /// action space of its own to fall back to, so it must be given one.</param>
        public MACEPvESARSATrainer(QRandom ran, QEvalType evalType = QEvalType.OnPolicy)
        {
            FeatureGate.Require(HyperQFeatures.Mace);
            _random = ran ?? throw new ArgumentNullException(nameof(ran));
            _EvalType = evalType;
        }

        public virtual void Add(Q<T> m, IActionSelector<T> actionSelector)
        {
            _Minds.Add(new MACEMind<T>(m, actionSelector));
        }

        public virtual void Add(MACEMind<T> q)
        {
            _Minds.Add(q);
        }

        /// <summary>
        /// Runs an episode in evaluation mode (no learning).
        /// </summary>
        /// <param name="env">The world environment</param>
        /// <param name="hp">The hyper params for the Q</param>
        public virtual void Episode(IMACEPvEEnv<T,RT> env, HyperParams hp)
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

        #region Dyna
        /// <summary>
        /// Enables the Dyna replay
        /// </summary>
        /// <param name="dyna">The dyna state engine</param>
        /// <param name="freq">The frequency to apply the dyna replay engine</param>
        public virtual void EnableDyna(DynaState<T, RT> dyna, double freq = 0.5)
        {
            DynaEnabled = true;
            Dyna = dyna;
            DynaFrequency = freq;
        }
        protected virtual void UpdateDyna(Tuple<T, QAction[]> last_sa, T sprime, RT r, HyperParams hp)
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
        protected virtual void Hallucinate(HyperParams hp)
        {
            if (Dyna == null || Dyna.DynaIterations == 0)
            {
                return;
            }
            for (int i = 0; i < Dyna.DynaIterations; i++)
            {
                DynaEntry<T> d = (DynaEntry<T>)Dyna.RandomSA(_random);
                Tuple<T, RT> sample = Dyna.Sample(d);
                for (int j = 0; j < _Minds.Count; j++)
                {
                    int aprime = 0;
                    if (_EvalType == QEvalType.OnPolicy)
                    {
                        aprime = _Minds[j].Mind.ActionSpace.RandomAction(true); // _random.Ran.Next(0, (int)_Minds[j].Item1.NumActions);
                        _Minds[j].Mind.OnPolicyUpdate(d.Item1, d.Item2[j].Action, sample.Item1, aprime, sample.Item2[0], hp);
                    }
                    else
                    {
                        _Minds[j].Mind.OffPolicyUpdate(d.Item1, d.Item2[j].Action, sample.Item1, sample.Item2[0], hp, EvalMethodType.Max);
                    }
                    // Do not add the hallucination to the memory buffer.
                }
            }
        }
        #endregion
        /// <summary>
        /// Sets the maximum number of episode steps to keep in the hysteresis buffer during an episode.
        /// </summary>
        /// <param name="steps"></param>
        public void SetHysteresisSteps(int steps)
        {
            HysteresisSteps = steps;
        }

        #region Memory
        /// <summary>
        /// Enables the replay memory cache
        /// </summary>
        /// <param name="memory">The memory cache used to draw replay slices</param>
        public virtual void EnableMemory(QMemory<T, RT> memory)
        {
            _Memory = memory;
        }
        protected virtual void Remember(T s, QAction[] a, T sprime, QAction[] aprime, RT r)
        {
            _Memory.Remember(s, a, sprime, aprime, r);
        }
        protected virtual bool ReplayCallback(QMemoryCell<T, RT> cell, HyperParams hp)
        {
            for (int j = 0; j < _Minds.Count; j++)
            {
                if (_EvalType == QEvalType.OnPolicy)
                {
                    _Minds[j].Mind.OnPolicyUpdate(cell.s, cell.a[j].Action, cell.sprime, cell.aprime[j].Action, cell.r[0], hp);
                }
                else
                {
                    _Minds[j].Mind.OffPolicyUpdate(cell.s, cell.a[j].Action, cell.sprime, cell.r[0], hp, EvalMethodType.Max);
                }
            }
            return true;
        }
        protected virtual bool PositiveReplayCallback(QMemoryCell<T,RT> cell, HyperParams hp)
        {
            for (int j = 0; j < _Minds.Count; j++)
            {
                if (cell.r.IsNegative)
                    continue;
                if (_EvalType == QEvalType.OnPolicy)
                {
                    _Minds[j].Mind.OnPolicyUpdate(cell.s, cell.a[j].Action, cell.sprime, cell.aprime[j].Action, cell.r[0], hp);
                }
                else
                {
                    _Minds[j].Mind.OffPolicyUpdate(cell.s, cell.a[j].Action, cell.sprime, cell.r[0], hp, EvalMethodType.Max);
                }
            }
            return true;
        }

        protected virtual bool NegativeReplayCallback(QMemoryCell<T,RT> cell, HyperParams hp)
        {
            for (int j = 0; j < _Minds.Count; j++)
            {
                if (!cell.r.IsNegative)
                    continue;
                if (_EvalType == QEvalType.OnPolicy)
                {
                    _Minds[j].Mind.OnPolicyUpdate(cell.s, cell.a[j].Action, cell.sprime, cell.aprime[j].Action, cell.r[0], hp);
                }
                else
                {
                    _Minds[j].Mind.OffPolicyUpdate(cell.s, cell.a[j].Action, cell.sprime, cell.r[0], hp, EvalMethodType.Max);
                }
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
                case ReminisceType.Negative:
                    _Memory.Playback(count, hp, NegativeReplayCallback);
                    break;
                case ReminisceType.Positive:
                    _Memory.Playback(count, hp, PositiveReplayCallback);
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
                    case ReminisceType.Negative:
                        _Memory.ReplayEpisode(hp, NegativeReplayCallback);
                        break;
                    case ReminisceType.Positive:
                        _Memory.ReplayEpisode(hp, PositiveReplayCallback);
                        break;
                }
                repeats--;
            }
            OnMemoryReplayEnd?.Invoke();
        }

        #endregion

        #region Warmups
        /// <summary>
        /// Performs the given number of episodes using only the uniform random action selector
        /// </summary>
        /// <param name="env"></param>
        /// <param name="hp"></param>
        /// <param name="n_episodes"></param>
        public virtual void Warmup(IMACEPvEEnv<T, RT> env, HyperParams hp, int n_episodes = 1000, IActionSelector<T>[] warmupSelectors = null)
        {
            WarmupEnabled = true;
            IActionSelector<T>[] hold = new IActionSelector<T>[_Minds.Count];
            for(int i=0; i < _Minds.Count; i++)
            {
                MACEMind<T> mind = _Minds[i];
                hold[i] = mind.ActionSelector;
                if (warmupSelectors == null || warmupSelectors[i] == null)
                    mind.ActionSelector = new UniformActionSelector<T>(mind.Mind,mind.Mind.ActionSpace);
                else
                    mind.ActionSelector = warmupSelectors[i];
            }
            for (int i = 0; i < n_episodes; i++)
            {
                Episode(env, hp);
            }
            for (int i = 0; i < _Minds.Count; i++)
            {
                MACEMind<T> mind = _Minds[i];
                mind.ActionSelector = hold[i];
            }
            WarmupEnabled = false;
        }
        #endregion

        #region OnPolicy
        /// <summary>
        /// The evaluation of the Q learner.
        /// </summary>
        /// <param name="env">The world environment being evaluated</param>
        /// <param name="hp">The hyper parameters for the evaluation</param>
        protected virtual void OnPolicyEpisode(IMACEPvEEnv<T,RT> env, HyperParams hp)
        {
            bool do_hysteresis = (hp.Hysteresis > 0.0);
            QAction[] action = new QAction[_Minds.Count];
            QAction[] aprime = new QAction[_Minds.Count];
            bool done = false;
            env.Reset();
            OnEpisodeStart?.Invoke();
            if (!WarmupEnabled)
            {
                foreach (var m in _Minds)
                    m.ActionSelector.StartEpisode();
            }
            Tuple<T, QAction[]> last_sa = null;
            if (!WarmupEnabled)
            {
                env.Metrics.StartEpisode();
                if (_Memory != null)
                {
                    _Memory.StartEpisode();
                }
            }
            T s = env.Discretize();
            for (int i = 0; i < _Minds.Count; i++)
            {
                _Minds[i].Mind.ResetAdvantageTrace();
                action[i] = _Minds[i].ActionSelector.SelectAction(_Minds[i].Mind, s, hp.Epsilon);
            }
            int iter = 0;
            env.Render();
            while (!done)
            {
                if (!WarmupEnabled)
                    env.Metrics.StartAction();
                OnStepStart?.Invoke();
               EnvResult<RT> result = env.Step(action);
                if (!WarmupEnabled)
                {
                    for (int i = 0; i < action.Length; i++)
                    {
                        env.Metrics.EndAction(action[i], _Minds[i].ActionSelector.LastActionWasRandom);
                    }
                    env.Metrics.NumberOfStatesVisited = _Minds[0].Mind.Shape.Item1;
                }
                RT r = result.Reward;
                done = result.Done;
                T sprime = env.Discretize();
                for (int i = 0; i < _Minds.Count; i++)
                {
                    aprime[i] = _Minds[i].ActionSelector.SelectAction(_Minds[i].Mind, sprime, hp.Epsilon);
                }
                if (!WarmupEnabled)
                    env.Metrics.StartUpdate();
                for (int i = 0; i < _Minds.Count; i++)
                {
                    _Minds[i].Mind.OnPolicyUpdate(s, action[i].Action, sprime, aprime[i].Action, r[0], hp);
                    _Minds[i].ActionSelector.ApplyAdvantage(_Minds[i].Mind.MapState(s), action[i], _Minds[i].Mind.Advantage[iter], hp);
                }
                if (!WarmupEnabled)
                {
                    // Only do hysteresis during training, but not during warmups
                    env.Metrics.EndUpdate();
                    if (InTraining)
                        if (_Memory != null)
                            Remember(s, action, sprime, aprime, r);
                    if (do_hysteresis)
                    {
                        for (int t = _Episode.Count - 1; t >= 0; t--)
                        {
                            double h_factor = hp.Hysteresis.Value / (_Episode.Count - t);
                            Tuple<T, QAction[], T, QAction[], RT> hh = _Episode[t];
                            if (h_factor > 0.0)
                            {
                                for (int i = 0; i < _Minds.Count; i++)
                                {
                                    IReward r2 = RewardMath<RT>.Scale(hh.Item5, h_factor);
                                    _Minds[i].Mind.OnPolicyUpdate(hh.Item1, hh.Item2[i].Action, hh.Item3, hh.Item4[i].Action, r2[0], hp);
                                }
                            }
                        }
                        _Episode.Add(new Tuple<T, QAction[], T, QAction[], RT>(s, action, sprime, aprime, r));
                        if (_Episode.Count > HysteresisSteps)
                            _Episode.RemoveAt(0);
                    }
                }
                last_sa = new Tuple<T, QAction[]>(s, action);
                action = aprime;
                // Give the next step its own array. Reusing 'aprime' would alias 'action', so the
                // next SelectAction would overwrite the action that was actually taken before the
                // SARSA update runs, and every remembered (s,a) and Dyna entry would share the same
                // mutating array.
                aprime = new QAction[_Minds.Count];
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
                foreach (var m in _Minds)
                    m.ActionSelector.EndEpisode(env.Metrics.TotalReward);
            }
            OnEpisodeEnd?.Invoke();
        }
        #endregion

        #region OffPolicy
        protected virtual void OffPolicyEpisode(IMACEPvEEnv<T,RT> env, HyperParams hp)
        {
            bool do_hysteresis = (hp.Hysteresis > 0.0);
            bool done = false;
            env.Reset();
            OnEpisodeStart?.Invoke();
            if (!WarmupEnabled)
            {
                foreach (var m in _Minds)
                    m.ActionSelector.StartEpisode();
            }
            Tuple<T, QAction[]> last_sa = null;
            for (int i = 0; i < _Minds.Count; i++)
            {
                _Minds[i].Mind.ResetAdvantageTrace();
            }
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
            while (!done)
            {
                if (!WarmupEnabled)
                    env.Metrics.StartAction();
                OnStepStart?.Invoke();
                // A fresh array per step: the replay memory and the Dyna model keep a reference to it.
                QAction[] a = new QAction[_Minds.Count];
                for (int i = 0; i < _Minds.Count; i++)
                {
                    a[i] = _Minds[i].ActionSelector.SelectAction(_Minds[i].Mind, s, hp.Epsilon);
                }
                EnvResult<RT> result = env.Step(a);
                RT r = result.Reward;
                if (!WarmupEnabled)
                {
                    for (int i = 0; i < a.Length; i++)
                    {
                        env.Metrics.EndAction(a[i], _Minds[i].ActionSelector.LastActionWasRandom);
                    }
                    env.Metrics.NumberOfStatesVisited = _Minds[0].Mind.Shape.Item1;
                }
                done = result.Done;
                T sprime = env.Discretize();
                if (!WarmupEnabled)
                    env.Metrics.StartUpdate();
                for (int i = 0; i < _Minds.Count; i++)
                {
                    _Minds[i].Mind.OffPolicyUpdate(s, a[i].Action, sprime, r[0], hp, EvalMethodType.Max);
                }
                if (!WarmupEnabled)
                {
                    env.Metrics.EndUpdate();
                    if (InTraining)
                    {
                        if (_Memory != null)
                            Remember(s, a, sprime, null, r); // off policy does not remember the prior action, we are off-policy...
                        if (do_hysteresis)
                        {
                            for (int t = _Episode.Count - 1; t >= 0; t--)
                            {
                                double h_factor = hp.Hysteresis.Value / (_Episode.Count - t);
                                Tuple<T, QAction[], T, QAction[], RT> hh = _Episode[t];
                                if (h_factor > 0.0)
                                {
                                    for (int i = 0; i < _Minds.Count; i++)
                                    {
                                        IReward r2 = RewardMath<RT>.Scale(hh.Item5, h_factor);
                                        _Minds[i].Mind.OffPolicyUpdate(hh.Item1, hh.Item2[i].Action, hh.Item3, r2[0], hp, EvalMethodType.Max);
                                    }
                                }
                            }
                            _Episode.Add(new Tuple<T, QAction[], T, QAction[], RT>(s, a, sprime, null, r));
                            if (_Episode.Count > HysteresisSteps)
                                _Episode.RemoveAt(0);
                        }
                    }
                }
                last_sa = new Tuple<T, QAction[]>(s, a);
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
                foreach (var m in _Minds)
                    m.ActionSelector.EndEpisode(env.Metrics.TotalReward);
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
