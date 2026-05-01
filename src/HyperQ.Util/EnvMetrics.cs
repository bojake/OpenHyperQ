using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public interface TimingMetrics
    {
        void StartReminiscing();
        void EndReminiscing();
        void StartDyna();
        void EndDyna();
        void StartUpdate();
        void EndUpdate();
        void StartEpisode();
        void EndEpisode(double? reward);
    }

    [Serializable]
    public class EnvMetrics : TimingMetrics
    {
        /// <summary>
        /// The number of time units spent in the episode
        /// </summary>
        public long TimeInEpisode { get; set; } = 0;
        /// <summary>
        /// The total reward accumulated
        /// </summary>
        public RewardAccumulator TotalReward { get; set; } = new RewardAccumulator();
        public decimal AverageReward { get { return _AvgReward.Value; } }
        public decimal AverageRewardPerStep { get { return _AvgRewardPerStep.Value; } }
        /// <summary>
        /// Returns the symmetricized measure of learning. The returned value should be in the range [-1,1]
        /// </summary>
        public decimal MeasureOfLearning { get { return (_RMOL == null || !_RMOL.IsPrimed) ? 0M : _RMOL.Value; } }

        /// <summary>
        /// History of the reward per episode.
        /// </summary>
        private List<RewardAccumulator> _RewardHistory = new List<RewardAccumulator>();
        /// <summary>
        /// The history of time elapsed in each episode
        /// </summary>
        private List<long> _EpisodeTimeHistory = new List<long>();
        /// <summary>
        /// The (s,a) history of the steps.
        /// </summary>
        private Dictionary<int,int> _ActionHistory = new Dictionary<int,int>();
        public List<Tuple<QAction, bool>> LastEpisodeTrace { get; private set; } = new List<Tuple<QAction, bool>>();
        public List<Tuple<QAction, bool>> BestEpisodeTrace { get; private set; } = new List<Tuple<QAction, bool>>();
        public RewardAccumulator BestEpisodeReward { get; private set; } = default;

        private int _ActionCount = 0;
        private long _EpisodeStartTicks=0L;
        private long _ReminisceStartTicks = 0L;
        private long _DynaStartTicks = 0L;
        private long _UpdateStartTicks = 0L;
        private long _ActionStartTicks = 0L;
        private bool _NoHistory = false;
        private RunningAverage _AvgEpisodeTime = new RunningAverage(0M);
        private RunningAverage _AvgReminiscingTime = new RunningAverage(0M);
        private RunningAverage _AvgActionSelectionTime = new RunningAverage(0M);
        private RunningAverage _AvgDynaTime = new RunningAverage(0M);
        private RunningAverage _AvgUpdateTime = new RunningAverage(0M);
        private RunningAverage _AvgReward = new RunningAverage(0M);
        private RunningAverage _AvgRewardPerStep = new RunningAverage(0M);
        // Sliding Average Reward: [n-w, n-w+1, n-w+2, ..., n, ... 0] where [0] is the most recent reward
        private RunningMeasureOfLearning _RMOL = null;

        /// <summary>
        /// Constructs the metrics instance
        /// </summary>
        /// <param name="noHistory">True if no action history is kept, default is false</param>
        /// <param name="molWindow">The Measure of Learning window, the number of episodes of average reward that is kept as the learning metric</param>
        public EnvMetrics(bool noHistory=false, int molWindow = 50)
        {
            TotalReward = new RewardAccumulator();
            _NoHistory = noHistory;
            _RMOL = new RunningMeasureOfLearning(molWindow);
        }

        /// <summary>
        /// Resizes the measure of learning window to the given width
        /// </summary>
        /// <param name="molWindow"></param>
        public virtual void ReframeMeasureOfLearning(int molWindow)
        {
            _RMOL = new RunningMeasureOfLearning(molWindow);
        }

        /// <summary>
        /// Records the ticks for the start of an action
        /// </summary>
        public virtual void StartAction()
        {
            _ActionStartTicks = DateTime.Now.Ticks;
        }

        /// <summary>
        /// End the action, compute the average action time, and keep an episode trace.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="was_random"></param>
        public virtual void EndAction(QAction action, bool was_random = false)
        {
            if (!_NoHistory)
            {
                int cnt = 0;
                _ActionHistory.TryGetValue(action.Action, out cnt);
                _ActionHistory[action.Action] = cnt + 1;
            }
            _ActionCount++;
            LastEpisodeTrace.Add(new Tuple<QAction,bool>(action,was_random));
            _AvgActionSelectionTime.Add(DateTime.Now.Ticks - _ActionStartTicks);
        }

        /// <summary>
        /// End an action just to log the time in the action.
        /// </summary>
        public virtual void EndAction()
        {
            _ActionCount++;
            _AvgActionSelectionTime.Add(DateTime.Now.Ticks - _ActionStartTicks);
        }

        public virtual void ClearActionTrace()
        {
            LastEpisodeTrace.Clear();
        }
        /// <summary>
        /// Clear out the action history but do not reset the action count or any other metrics.
        /// </summary>
        public virtual void ClearActionHistory()
        {
            _ActionHistory.Clear();
        }

        public virtual void StartReminiscing()
        {
            _ReminisceStartTicks = DateTime.Now.Ticks;
        }
        public virtual void EndReminiscing()
        {
            _AvgReminiscingTime.Add(DateTime.Now.Ticks - _ReminisceStartTicks);
        }
        public virtual void StartDyna()
        {
            _DynaStartTicks = DateTime.Now.Ticks;
        }
        public virtual void EndDyna()
        {
            _AvgDynaTime.Add(DateTime.Now.Ticks - _DynaStartTicks);
        }
        public virtual void StartUpdate()
        {
            _UpdateStartTicks = DateTime.Now.Ticks;
        }
        public virtual void EndUpdate()
        {
            _AvgUpdateTime.Add(DateTime.Now.Ticks - _UpdateStartTicks);
        }
        public virtual void StartEpisode()
        {
            _EpisodeStartTicks = DateTime.Now.Ticks;
            LastEpisodeTrace.Clear();
            TotalReward = new RewardAccumulator();
            _ActionCount = 0;
            TimeInEpisode = 0;
        }

        /// <summary>
        /// Ends the episode, records the total reward, updates the running metrics for the reward and timing, and then
        /// calls the Save method.
        /// </summary>
        /// <param name="reward"></param>
        public virtual void EndEpisode(double? reward)
        {
            if (reward.HasValue)
            {
                TotalReward.Add(reward.Value);
            }
            _AvgReward.Add(TotalReward.Scalar0);
            if(_ActionCount > 0)
                _AvgRewardPerStep.Add(TotalReward.Scalar0 / _ActionCount);
            _RMOL.Add(TotalReward.Scalar0); // / _ActionCount);
            if (_ActionCount == 1)
            {
                BestEpisodeReward = TotalReward;
                BestEpisodeTrace = new List<Tuple<QAction, bool>>(LastEpisodeTrace);
            }
            else
            {
                if (BestEpisodeReward == null || TotalReward.Scalar0 > BestEpisodeReward.Scalar0)
                {
                    BestEpisodeReward = TotalReward;
                    BestEpisodeTrace = new List<Tuple<QAction, bool>>(LastEpisodeTrace);
                }
            }
            TimeInEpisode = DateTime.Now.Ticks - _EpisodeStartTicks;
            Save();
        }

        /// <summary>
        /// Saves the history of the metrics
        /// </summary>
        public virtual void Save()
        {
            _RewardHistory.Add(TotalReward);
            _EpisodeTimeHistory.Add(TimeInEpisode);
            _AvgEpisodeTime.Add(TimeInEpisode);
        }

        public virtual int NumberOfActionsTaken
        {
            get
            {
                return (_ActionCount);
            }
        }
        public virtual uint NumberOfStatesVisited
        {
            get;
            set;
        }
        public virtual void Dump()
        {
            Console.WriteLine("== Env Metrics ==");
            Console.WriteLine("   Total Reward: {0}", TotalReward);
            Console.WriteLine("   Average Reward: {0} over {1} episodes", _AvgReward.Value, _RewardHistory.Count);
            Console.WriteLine("   Average Reward Per Step: {0}", _AvgRewardPerStep.Value);
            Console.WriteLine("   # Actions Taken: {0}", NumberOfActionsTaken);
            Console.WriteLine("   Episodes Take: {0:F2} ms", new TimeSpan((long)_AvgEpisodeTime.Value).TotalMilliseconds);
            Console.WriteLine("   Total Episode Time: {0:F2} ms", new TimeSpan((long)_AvgEpisodeTime.Sum).TotalMilliseconds);
            Console.WriteLine("   Reminiscing Takes: {0:F2} ms", new TimeSpan((long)_AvgReminiscingTime.Value).TotalMilliseconds);
            decimal pct = 0M;
            if(_AvgEpisodeTime.Sum > 0) 
                pct = _AvgReminiscingTime.Sum / _AvgEpisodeTime.Sum * 100M;
            Console.WriteLine("   Total Reminiscing Time: {0:F2} ms ({1:F2} %)", new TimeSpan((long)_AvgReminiscingTime.Sum).TotalMilliseconds, pct);
            Console.WriteLine("   Action Selection Takes: {0:F2} ms", new TimeSpan((long)_AvgActionSelectionTime.Value).TotalMilliseconds);
            Console.WriteLine("   Total Action Selection Time: {0:F2} ms", new TimeSpan((long)_AvgActionSelectionTime.Sum).TotalMilliseconds);
            Console.WriteLine("   Dyna Takes: {0:F2} ms", new TimeSpan((long)_AvgDynaTime.Value).TotalMilliseconds);
            pct = 0M;
            if(_AvgEpisodeTime.Sum > 0)
                pct = _AvgDynaTime.Sum / _AvgEpisodeTime.Sum * 100M;
            Console.WriteLine("   Total Dyna Time: {0:F2} ms ({1:F2} %)", new TimeSpan((long)_AvgDynaTime.Sum).TotalMilliseconds, pct);
            Console.WriteLine("   Updates Take: {0:F2} ms", new TimeSpan((long)_AvgUpdateTime.Value).TotalMilliseconds);
            pct = 0M;
            if(_AvgEpisodeTime.Sum > 0) 
                pct = _AvgUpdateTime.Sum / _AvgEpisodeTime.Sum * 100M;
            Console.WriteLine("   Total Updates Time: {0:F2} ms ({1:F2} %)", new TimeSpan((long)_AvgUpdateTime.Sum).TotalMilliseconds, pct);
            Console.WriteLine("   # States Visited: {0}", NumberOfStatesVisited);
            if (_NoHistory)
            {
                Console.WriteLine("   No state history is being recorded.");
            }
            else if (_ActionHistory.Keys.Count > 0)
            {
                Console.WriteLine("   -- Action History --");
                foreach (int key in _ActionHistory.Keys)
                {
                    Console.WriteLine("      {0}:{1}", key, _ActionHistory[key]);
                }
            }
            Console.WriteLine("Best episode reward: {0}", BestEpisodeReward);
            foreach (Tuple<QAction,bool> action in BestEpisodeTrace)
            {
                Console.Write("{1}{0}->", action.Item1.Action, action.Item2?"*":"");
            }
            Console.WriteLine("[END]");
        }
    }
}
