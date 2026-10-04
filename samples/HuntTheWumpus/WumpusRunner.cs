using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Util;
using HyperQ.MACE.Training;
using HyperQ.MACE.Eval;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.MACE;

namespace HuntTheWumpus
{
    public class QQRunner : WumpusRunner<decimal>
    {
        public QQRunner(int numepisodes, HyperParams h = null) : base(numepisodes, h) { }
        protected override Q<decimal> CreateLearner(QActionSpace<int> actionSpace)
        {
            return new MappedQQ<decimal>(actionSpace, _Random.DefaultRandomAction, ran: _TheRandom);
        }
        protected override WumpusBaseGameEnv CreateWorld()
        {
            WumpusGameEnv world = new WumpusGameEnv(ran: _TheRandom, quiet: Quiet, staticSeed: WorldSeed);
            world.Metrics = new EnvMetrics(noHistory: true);
            return (world);
        }
    }
    public class HyperQQRunner : WumpusRunner<QState<decimal>>
    {
        public HyperQQRunner(int numepisodes, HyperParams h = null) : base(numepisodes, h) { }
        protected override Q<QState<decimal>> CreateLearner(QActionSpace<int> actionSpace)
        {
            return new DoubleHyperQ<decimal>(actionSpace, _Random.DefaultRandomAction, ran: _TheRandom);
        }
        protected override WumpusBaseGameEnv CreateWorld()
        {
            WumpusHyperGameEnv world = new WumpusHyperGameEnv(ran: _TheRandom, quiet: Quiet, staticSeed: WorldSeed);
            world.Metrics = new EnvMetrics(noHistory: true);
            return (world);
        }
    }
    public class QRunner : WumpusRunner<decimal>
    {
        public QRunner(int numepisodes, HyperParams h = null) : base(numepisodes, h) { }
        protected override Q<decimal> CreateLearner(QActionSpace<int> actionSpace)
        {
            return new MappedQ<decimal>(actionSpace, _Random.DefaultRandomAction);
        }
        protected override WumpusBaseGameEnv CreateWorld()
        {
            WumpusGameEnv world = new WumpusGameEnv(ran: _TheRandom, quiet: Quiet, staticSeed: WorldSeed);
            world.Metrics = new EnvMetrics(noHistory: false);
            return (world);
        }
    }
    public class HyperQRunner : WumpusRunner<QState<decimal>>
    {
        public HyperQRunner(int numepisodes, HyperParams h = null) : base(numepisodes, h) { }
        protected override Q<QState<decimal>> CreateLearner(QActionSpace<int> actionSpace)
        {
            return new SingleHyperQ<decimal>(actionSpace, _Random.DefaultRandomAction);
        }
        protected override WumpusBaseGameEnv CreateWorld()
        {
            WumpusHyperGameEnv world = new WumpusHyperGameEnv(ran: _TheRandom, quiet: Quiet, staticSeed: WorldSeed);
            world.Metrics = new EnvMetrics(noHistory: false);
            return (world);
        }
    }
    public class LayeredHyperQRunner : WumpusRunner<QState<decimal>>
    {
        public LayeredHyperQRunner(int numepisodes, HyperParams h = null) : base(numepisodes, h) { }
        protected override Q<QState<decimal>> CreateLearner(QActionSpace<int> actionSpace)
        {
            SingleQGenerator<decimal> g = new SingleQGenerator<decimal>(actionSpace, _Random.DefaultRandomAction);
            return new LayeredHyperQ<decimal>(g.HyperQGenerator, actionSpace);
        }
        protected override WumpusBaseGameEnv CreateWorld()
        {
            WumpusHyperGameEnv world = new WumpusHyperGameEnv(ran: _TheRandom, quiet: Quiet, staticSeed: WorldSeed);
            world.Metrics = new EnvMetrics(noHistory: false);
            return (world);
        }
    }
    public class LayeredHyperQQRunner : WumpusRunner<QState<decimal>>
    {
        public LayeredHyperQQRunner(int numepisodes, HyperParams h = null) : base(numepisodes, h) { }
        protected override Q<QState<decimal>> CreateLearner(QActionSpace<int> actionSpace)
        {
            DoubleQGenerator<decimal> g = new DoubleQGenerator<decimal>(actionSpace, _Random.DefaultRandomAction, ran: _TheRandom);
            return new LayeredHyperQ<decimal>(g.HyperGenerator, actionSpace);
        }
        protected override WumpusBaseGameEnv CreateWorld()
        {
            WumpusHyperGameEnv world = new WumpusHyperGameEnv(ran: _TheRandom, quiet: Quiet, staticSeed: WorldSeed);
            world.Metrics = new EnvMetrics(noHistory: false);
            return (world);
        }
    }

    public interface IWumpusRunner
    {
        event Action OnStepEnd;
        event Action OnEpisodeStart;
        event Action<double[], QAction, bool> OnTelemetry;
        event Action OnRunComplete;
        HyperParams Hypers { get; set; }
        WumpusBaseGameEnv World { get; }

        object TheLearner { get; }
        object TheSelector { get; }
        void Stop();

        void Run(ProgramArgs args);
        void Save(string fname);
        void Load(string fname);
    }

    [Serializable]
    public class VerboseMaxSelector<T> : MaxActionSelector<T>
    {
        public bool Verbose { get; set; } = false;
        public VerboseMaxSelector(QActionSpace<int> actionSpace) : base(actionSpace)
        {
        }

        protected override QAction ProcessSelection(T state, IArgMinMax<T> q, QAction t)
        {
            if(Verbose)
                if(t != null)
                    Console.WriteLine("Process: {0} = {1}", t.Action, t.Reward);
            return base.ProcessSelection(state, q, t);
        }
        public override QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            if (Verbose)
            {
                Q<T> qFull = q as Q<T>;
                if (qFull != null)
                {
                    double[] array = qFull.GetActionArray(state);
                    Console.Write("(max) {0} : ", state);
                    foreach (double d in array)
                    {
                        Console.Write("{0},", d);
                    }
                    Console.Write(" [e={0}]", epsilon);
                }
            }
            QAction action = base.SelectAction(q, state, epsilon);
            if (LastActionWasRandom)
            {
                Console.Write('*');
            }
            Console.WriteLine(" => a={0}", action);
            return (action);
        }
    }

    [Serializable]
    public class VerboseGreedySelector<T> : eGreedyActionSelector<T>
    {
        public bool Verbose { get; set; } = false;
        public VerboseGreedySelector(QActionSpace<int> actionSpace) : base(actionSpace)
        {
        }

        public override QAction SelectAction(IArgMinMax<T> q, T state, double epsilon)
        {
            if (Verbose)
            {
                Q<T> qFull = q as Q<T>;
                if (qFull != null)
                {
                    double[] array = qFull.GetActionArray(state);
                    Console.Write("(greedy) {0} : ", state);
                    foreach (double d in array)
                    {
                        Console.Write("{0},", d);
                    }
                    Console.Write(" [e={0}]", epsilon);
                }
            }
            QAction action = base.SelectAction(q, state, epsilon);
            if (LastActionWasRandom)
            {
                Console.Write('*');
            }
            Console.WriteLine(" => a={0}", action);
            return action;
        }
    }
    [Serializable]
    public class HuntTheWumpusActionSpace : QMappedActionSpace<int>
    {
        private int _maxActions = 0;
        public HuntTheWumpusActionSpace(QRandom ran, int maxActions) : base(ran)
        {
            _maxActions = maxActions;
        }

        public override uint MaximumNumberOfActions
        {
            get
            {
                return (uint)_maxActions;
            }
        }

        public override QActionSpace<int> Clone()
        {
            HuntTheWumpusActionSpace ras = new HuntTheWumpusActionSpace(Random, _maxActions);
            base.CopyTo(ras);
            return ras;
        }

        public override int UnknownRandomAction()
        {
            return Random.Ran.Next(0, _maxActions);
        }

        public override int FromIndex(uint index)
        {
            int last_i = 0;
            while (!base.IsValidIndex(index))
            {
                for (int i = last_i; i < _maxActions; i++)
                {
                    // Actions are ordinals in this space, so start adding them
                    if (!base.IsKnownAction(i))
                    {
                        base.ToIndex(i);
                        last_i = i;
                        break;
                    }
                }
            }
            return base.FromIndex(index);
        }
    }
    [Serializable]
    public abstract class WumpusRunner<T> : IWumpusRunner
    {
        /// <summary>
        /// The world's random source. Null gives each world its own static seed, so it rebuilds the same cave on
        /// every reset; the randomize option hands the world the run's source instead.
        /// </summary>
        protected QRandom _TheRandom = null;
        /// <summary>The static cave's seed (option seed=).</summary>
        protected int WorldSeed = WumpusBaseGameEnv.DefaultStaticSeed;
        /// <summary>One row per greedy evaluation: wumpus-eval.csv.</summary>
        private StreamWriter _EvalCsv = null;
        public int NumEpisodes { get; private set; }
        public WumpusBaseGameEnv World { get; private set; }
        protected MACEMind<T>[] _Q;
        public double StepDuration { get; set; } = 10.0;
        public bool Quiet { get; set; } = true;
        public HyperParams Hypers { get; set; } = new HyperParams(g: 0.997, e: .5, a: 0.7, edecay: 0.999991, adecay: 0.999991, min_epsilon: 0.05, min_alpha: 0.1, tau: 0.95);
        public MACEPvESARSATrainer<T, ScalarReward> Model { get; private set; }

        public event Action OnStepEnd;
        public event Action OnEpisodeStart;
        public event Action OnRunComplete;
        public event Action<double[], QAction, bool> OnTelemetry;

        private bool _Done = false;

        public void Stop()
        {
            lock (this)
            {
                _Done = true;
            }
        }

        public Q<T> Learner
        {
            get { return _Q != null && _Q.Length > 0 ? _Q[0].Mind : null; }
        }

        public IActionSelector<T> Selector
        {
            get { return _Q != null && _Q.Length > 0 ? _Q[0].ActionSelector : null; }
        }

        public object TheLearner { get { return Learner; } }
        public object TheSelector { get { return Selector; } }

        /// <summary>
        /// The run's random source: the action spaces, learners, world, trainer and memory draw from it, so a
        /// run is reproducible from this seed.
        /// </summary>
        protected readonly QRandom _Random = new QRandom(903387237);

        public WumpusRunner(int numepisodes, HyperParams h = null)
        {
            NumEpisodes = numepisodes;
            if (h != null)
            {
                Hypers = h;
            }
        }

        protected abstract Q<T> CreateLearner(QActionSpace<int> actionSpace);
        protected abstract WumpusBaseGameEnv CreateWorld();

        private void EvaluateIt(bool quiet = false, string episode = null)
        {
            MACEEvaluator<T, ScalarReward> eval = null;
            eval = new MACEEvaluator<T, ScalarReward>();
            foreach (MACEMind<T> q in _Q)
            {
                q.ActionSelector.ActionMode = MinMaxActionEnum.MostProbable;
                eval.Add(q.Mind, q.ActionSelector);
            }
            World.Quiet = quiet;
            World.Reset();
            //
            // Evaluate
            //
            Console.WriteLine("==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==");
            World.Quiet = false;
            HyperParams hpz = new HyperParams(g: 0.997, e: 0.0, a: 1, edecay: 1, adecay: 1, min_epsilon: 0.0, min_alpha: 1);
            eval.Episode((IMACEPvEEnv<T, ScalarReward>)World, hpz);
            Console.WriteLine("Ending evaluation with total reward {0} in {1} states and {2} actions.", World.Metrics.TotalReward, World.Metrics.NumberOfStatesVisited, World.Metrics.NumberOfActionsTaken);
            if (_EvalCsv != null && episode != null)
            {
                WumpusGameStatus rpt = World.Report;
                _EvalCsv.WriteLine("{0},{1},{2},{3},{4},{5}", episode, World.Status, World.Metrics.TotalReward, World.Metrics.NumberOfActionsTaken, rpt.Gold, rpt.FoodLevel);
                _EvalCsv.Flush();
            }
            foreach (MACEMind<T> q in _Q)
            {
                q.ActionSelector.ActionMode = MinMaxActionEnum.Default;
            }
        }
        // int epStep, QEvalType evalType = QEvalType.OffPolicy, int warmup_episodes = 0, int memory_size = 500, int dyna_size = 200, int dyna_iters = 500, bool use_negpos_memory = false, double dyna_freq = 0.2, int max_training_steps = 500, bool use_episodic_memory = false
        public void TrainIt(ProgramArgs args)
        {
            QEvalType evalType = QEvalType.OffPolicy;
            if (args.onpolicy)
                evalType = QEvalType.OnPolicy;
            TextWriter console = Console.Out;
            List<string> files = new List<string>();
            using (StreamWriter swout = new StreamWriter($"wumpus-trainit-{NumEpisodes}x{args.epStep}-{evalType}.log"))
            {
                Console.SetOut(swout);
                using (StreamWriter swResult = new StreamWriter($"wumpus-result-{NumEpisodes}-final.csv"))
                {
                    swResult.WriteLine("deaths,escapes,totalreward,avgreward,epsilon,alpha,gold,food,wins");
                    Console.WriteLine("Training {0} episodes", NumEpisodes);
                    Console.WriteLine("  output step = {0}", args.epStep);
                    Console.WriteLine("  warmup = {0}", args.warmup_episodes);
                    Console.WriteLine("  memory = {0} {1}", args.memory_size, args.use_negpos_memory ? "neg/pos" : "all");
                    Console.WriteLine("  dyna = {0} @ {1:F3}", args.dyna_size, args.dyna_freq);
                    if (World == null)
                    {
                        World = CreateWorld();
                        World.RewardModel = args.model;
                        World.Dimensions = args.Dimensions;
                        World.Reset();
                        Console.WriteLine("Created the simulation world.");
                    }
                    IActionSelector<T> selector = null;
                    MACEPvESARSATrainer<T, ScalarReward> s = new MACEPvESARSATrainer<T, ScalarReward>(_Random, evalType);
                    Model = s;
                    s.OnStepEnd += () => OnStepEnd?.Invoke();
                    s.OnEpisodeStart += () => OnEpisodeStart?.Invoke();
                    int[] numActions = World.NumActions;
                    if (_Q == null)
                        _Q = new MACEMind<T>[numActions.Length];
                    for (int i = 0; i < numActions.Length; i++)
                    {
                        HuntTheWumpusActionSpace ras = new HuntTheWumpusActionSpace(_Random, numActions[i]);
                        Q<T> learner = CreateLearner(ras);
                        // selector = new MostProbableMaxActionSelector<T>((int)learner.MaxNumActions, _Random.Ran);
                        if (args.actionModel == ActionSelectionModel.eGreedy)
                            selector = new eGreedyActionSelector<T>(ras);
                        else if (args.actionModel == ActionSelectionModel.PolicyGradient)
                            selector = new PolicyGradientActionSelector<T>(learner, ras);
                        else if (args.actionModel == ActionSelectionModel.GRPO_KL)
                            selector = new GRPOWithKLPenaltyActionSelector<T>(learner, ras, klCoef: 0.1);
                        else if (args.actionModel == ActionSelectionModel.SoftMax)
                            selector = new PolicyGradientWithSoftmaxActionSelector<T>(learner, ras);
                        else if (args.actionModel == ActionSelectionModel.SoftMax_KL)
                            selector = new SoftmaxWithKLPenaltyActionSelector<T>(learner, ras, klCoef: 0.1);
                        else if (args.actionModel == ActionSelectionModel.Min)
                            selector = new MinActionSelector<T>(ras);
                        else if (args.actionModel == ActionSelectionModel.Max)
                            selector = new MaxActionSelector<T>(ras);
                        else if (args.actionModel == ActionSelectionModel.Hybrid)
                        {
                            List<IActionSelector<T>> models = new List<IActionSelector<T>>();
                            foreach (string sm in args.HybridActionModels)
                            {
                                switch (sm)
                                {
                                    case "GRPO":
                                        models.Add(new GRPOWithKLPenaltyActionSelector<T>(learner, ras, 0.1));
                                        break;
                                    case "SoftMax":
                                        models.Add(new PolicyGradientWithSoftmaxActionSelector<T>(learner, ras));
                                        break;
                                    case "Min":
                                        models.Add(new MinActionSelector<T>(ras));
                                        break;
                                    case "Max":
                                        models.Add(new MaxActionSelector<T>(ras));
                                        break;
                                    case "PolicyGradient":
                                        models.Add(new PolicyGradientActionSelector<T>(learner, ras));
                                        break;
                                    case "SoftMax_KL":
                                        models.Add(new SoftmaxWithKLPenaltyActionSelector<T>(learner, ras, 0.1));
                                        break;
                                    case "eGreedy":
                                        models.Add(new eGreedyActionSelector<T>(ras));
                                        break;

                                }
                            }
                            selector = new HybridActionSelector<T>(25, models.ToArray());
                        }
                        //
                        _Q[i] = new MACEMind<T>(learner, selector);
                        selector.TelemetryCallback = (st, act) =>
                        {
                            double[] arr = learner.GetActionArray(st);
                            OnTelemetry?.Invoke(arr, act, selector.LastActionWasRandom);
                        };
                        s.Add(_Q[i]);
                    }
                    ApplyPendingLoad();
                    IMACEPvEEnv<T, ScalarReward> env = (IMACEPvEEnv<T, ScalarReward>)World;
                    if (args.memory_size > 0)
                    {
                        QMemory<T, ScalarReward> mem = null;
                        if (args.episodic)
                        {
                            if (args.use_negpos_memory)
                                mem = new QEpisodicNegPosMemory<T, ScalarReward>(args.memory_size, _Random);
                            else
                                mem = new QEpisodicMemory<T, ScalarReward>(args.memory_size, _Random);
                        }
                        else
                        {
                            if (args.use_negpos_memory)
                                mem = new QNegPosMemory<T, ScalarReward>(args.memory_size, _Random);
                            else
                                mem = new QMemory<T, ScalarReward>(args.memory_size, _Random);
                        }
                        s.EnableMemory(mem);
                    }
                    if (args.dyna_size > 0)
                        s.EnableDyna(new DynaState<T, ScalarReward>(args.dyna_iters, args.dyna_size), args.dyna_freq);
                    HyperParams hp = new HyperParams(Hypers);
                    s.InTraining = true;
                    if (args.warmup_episodes > 0)
                        s.Warmup(env, hp, args.warmup_episodes);
                    RunningAverage avgReward = new RunningAverage();
                    RunningAverage avgEpisodeTime = new RunningAverage();
                    int exceptions = 0;
                    Dictionary<PlayerStatus, int> status_count = new Dictionary<PlayerStatus, int>();
                    status_count[PlayerStatus.Dead] = 0;
                    status_count[PlayerStatus.Escaped] = 0;
                    status_count[PlayerStatus.Win] = 0;
                    double reward_max = double.MinValue;
                    using (StreamWriter status_sw = new StreamWriter(string.Format("train-status-{0}.csv", NumEpisodes)))
                    {
                        status_sw.Write("episodes,exceptions,deaths,escapes,wins,when");
                        status_sw.WriteLine();
                        for (int i = 0; i < NumEpisodes; i++)
                        {
                            lock (this)
                            {
                                if (_Done)
                                    break;
                            }
                            long lstart = DateTime.Now.Ticks;
                            World.Quiet = true;
                            s.Episode(env, hp);
                            var report = World.Report;
                            if ((World.Status == PlayerStatus.Win || World.Metrics.TotalReward > reward_max) && args.obsession)
                            {
                                // TODO: Want to remember this for obsessive replay
                                Console.WriteLine("{0} : +Obsessing on the win with {1} reward!", i, World.Metrics.TotalReward);
                                s.Obsess(hp, 100,ReminisceType.Positive);
                                if (World.Metrics.TotalReward > reward_max)
                                {
                                    reward_max = World.Metrics.TotalReward;
                                }
                            }
                            avgReward.Add(World.Metrics.TotalReward);
                            // Log the result
                            float f = (float)i / (float)NumEpisodes;
                            // Reminisce the past
                            World.Metrics.StartReminiscing();
                            s.Reminisce(hp, 300);
                            World.Metrics.EndReminiscing();
                            // Only decay in training
                            hp.DecayEpsilon();
                            hp.DecayAlpha();
                            if ((i + 1) % args.epStep == 0)
                            {
                                Console.WriteLine("Ending training episode {0} with total reward {1} in {2} states and {3} actions.", i + 1, World.Metrics.TotalReward, World.Metrics.NumberOfStatesVisited, World.Metrics.NumberOfActionsTaken);
                                Console.WriteLine("Hypers: e={0}, a={1}", hp.Epsilon.Value, hp.Alpha.Value);
                                swout.Flush();
                                status_sw.WriteLine("{0},{1},{2},{3},{4},{5:MM/dd/yyyy HH:mm:ss}", i, exceptions, status_count[PlayerStatus.Escaped], status_count[PlayerStatus.Dead], status_count[PlayerStatus.Win], DateTime.Now);
                                status_sw.Flush();
                                World.Metrics.Dump();
                                //
                                // Evaluate
                                //
                                Console.WriteLine("==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==-==");
                                int mm = 1;
                                Console.WriteLine("Evaluating epoch {0}", i / args.epStep);
                                foreach (MACEMind<T> mind in _Q)
                                {
                                    Console.WriteLine("Mind {1} Took {0:N0} random acts...", mind.ActionSelector.RandomActionCount, i / args.epStep, mm);
                                    if (args.nomatrix)
                                    {
                                        mm++;
                                        continue;
                                    }
                                    try
                                    {
                                        double[,] matrix = mind.Mind.AsMatrix;
                                        files.Add($"matrix-{i}-{mm}.csv");
                                        using (StreamWriter swq = new StreamWriter($"matrix-{i}-{mm}.csv"))
                                        {
                                            for (int jx = 0; jx < matrix.GetLength(0); jx++)
                                            {
                                                for (int ix = 0; ix < matrix.GetLength(1); ix++)
                                                {
                                                    swq.Write("{0},", matrix[jx, ix]);
                                                }
                                                swq.WriteLine();
                                            }
                                            swq.Flush();
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        Console.WriteLine("No AsMatrix output.");
                                    }
                                    mm++;
                                }
                                Console.WriteLine("Total memory in use {0:N0} bytes.", GC.GetTotalMemory(false));
                                swout.Flush();
                                using (StreamWriter swout2 = new StreamWriter($"wumpus-eval-{i}-{NumEpisodes}x{args.epStep}-{evalType}.log"))
                                {
                                    Console.SetOut(swout2);
                                    // Evaluate
                                    EvaluateIt(false, (i + 1).ToString());
                                }
                                Console.SetOut(swout);
                            }
                            World.Metrics.ClearActionHistory();
                            lstart = DateTime.Now.Ticks - lstart;
                            TimeSpan ts = new TimeSpan(lstart);
                            avgEpisodeTime.Add(ts.TotalMilliseconds);
                            status_count[World.Status] += 1;
                            WumpusGameStatus sg = World.Report;
                            swResult.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}", status_count[PlayerStatus.Dead], status_count[PlayerStatus.Escaped], World.Metrics.TotalReward, avgReward.Value, hp.Epsilon.Value, hp.Alpha.Value,sg.Gold,sg.FoodLevel, status_count[PlayerStatus.Win]);
                        }
                        status_sw.Write(NumEpisodes);
                        status_sw.Write(',');
                        status_sw.Write(exceptions);
                        status_sw.WriteLine();
                    }
                    Console.WriteLine("===================================================================================");
                    Console.WriteLine("Average reward {0:F3}", avgReward.Value);
                    Console.WriteLine("Average episode runtime {0:F3} ms", avgEpisodeTime.Value);
                    Console.WriteLine("{0} exceptions (out of iterations)", exceptions);
                    Console.WriteLine("{0} Escapes, {1} deaths, {2} wins", status_count[PlayerStatus.Escaped], status_count[PlayerStatus.Dead], status_count[PlayerStatus.Win]);
                    foreach(MACEMind<T> mx in _Q)
                        Console.WriteLine("{0} states, {1} actions, {2} random acts in the Q", mx.Mind.Shape.Item1, mx.Mind.Shape.Item2, mx.ActionSelector.RandomActionCount);
                    // Write the frames file for the animation
                    using (StreamWriter swx = new StreamWriter("wumpus-frames.txt"))
                    {
                        foreach(string s1 in files)
                            swx.WriteLine(s1);
                    }
                    // for /f %i in (frames.txt) do if exist %i python ..\Heatmap.py %i
                }
            }
            // Reset the console output
            Console.SetOut(console);
            OnRunComplete?.Invoke();
        }

        private string _PendingLoad = null;

        /// <summary>
        /// Saves the minds (learners and stateful selectors) as a HyperQ checkpoint. A name ending in .gz is
        /// written compressed; any other name gets .gz appended.
        /// </summary>
        public void Save(string fname)
        {
            if (_Q == null)
            {
                Console.WriteLine("Nothing to save: the minds have not been created yet.");
                return;
            }
            string path = fname.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? fname : fname + ".gz";
            MACECheckpoint.SaveFile(path, _Q);
            Console.WriteLine("Saved the checkpoint of {0} minds to {1}", _Q.Length, path);
        }

        /// <summary>
        /// Schedules a checkpoint to be restored into the minds once Run has created them from the options: a
        /// checkpoint holds learned state, not the learner configuration.
        /// </summary>
        public void Load(string fname)
        {
            if (!File.Exists(fname) && File.Exists(fname + ".gz"))
            {
                fname += ".gz";
            }
            if (!File.Exists(fname))
            {
                Console.WriteLine("Checkpoint {0} does not exist. Ignoring.", fname);
                return;
            }
            _PendingLoad = fname;
        }

        private void ApplyPendingLoad()
        {
            if (_PendingLoad == null || _Q == null)
            {
                return;
            }
            try
            {
                MACECheckpoint.LoadFile(_PendingLoad, _Q);
                Console.WriteLine("Loaded the Q state from {0}", _PendingLoad);
            }
            catch (Exception ex)
            {
                Console.WriteLine("OOPS, {0} could not be loaded into the configured learners ({1}). Ignoring.", _PendingLoad, ex.Message);
            }
            _PendingLoad = null;
        }

        // int epStep, int warmup_episodes = 0, int memory_size = 500, int dyna_size = 200, bool use_negpos_memory = false, double dyna_freq = 0.2, QEvalType evalType = QEvalType.OffPolicy, int max_training_steps = 500, bool use_episodic_memory = false
        public void Run(ProgramArgs args)
        {
            if (args.seedSet)
            {
                _Random.Seed(args.seed);
                WorldSeed = args.seed;
            }
            if (args.randomize)
            {
                this._TheRandom = _Random;
            }
            QEvalType evalType = QEvalType.OffPolicy;
            if (args.onpolicy)
                evalType = QEvalType.OnPolicy;
            TextWriter console = Console.Out;
            Stopwatch watch = new Stopwatch();
            watch.Start();
            if (args.replay != null)
            {
                World = CreateWorld();
                World.RewardModel = args.model;
                World.Dimensions = args.Dimensions;
                World.Reset();
                World.Quiet = false;
                Console.WriteLine("===================================================================================");
                Console.WriteLine("Action Sequence Replay");
                QAction[] action = new QAction[1];
                using (StreamWriter swout2 = new StreamWriter($"wumpus-eval-replay-{NumEpisodes}x{args.epStep}-{evalType}.log"))
                {
                    Console.SetOut(swout2);
                    foreach (string a in args.replay)
                    {
                        action[0] = new QAction(int.Parse(a), 0.0, 0);
                        World.Step(action);
                        World.Render();
                    }
                }
                Console.SetOut(console);
            }
            else
            {
                _EvalCsv = new StreamWriter("wumpus-eval.csv");
                _EvalCsv.WriteLine("episode,status,reward,actions,gold,food");
                TrainIt(args);
                using (StreamWriter swout = new StreamWriter($"wumpus-eval-final-{NumEpisodes}x{args.epStep}-{evalType}.log"))
                {
                    Console.SetOut(swout);
                    // Evaluate
                    EvaluateIt(false, "final");
                }
                _EvalCsv.Dispose();
                _EvalCsv = null;
            }
            // Reset the console output
            Console.SetOut(console);
            // _Q.Dump();
            World.Metrics.Dump();
            Console.WriteLine("===================================================================================");
            Console.WriteLine("Best Action Sequence Replay");
            World.Reset();
            World.Quiet = false;
            /*foreach (Tuple<int[], bool> action in World.Metrics.BestEpisodeTrace)
            {
                World.Step(action.Item1);
                World.Render();
            }*/
            watch.Stop();
            Console.WriteLine("Run took {0:c}", watch.Elapsed);
        }
    }
}
