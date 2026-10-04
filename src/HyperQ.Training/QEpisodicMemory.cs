using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Training
{
    [Serializable]
    public class QEpisodicMemoryCell<T,RT> where RT : IReward
    {
        private List<QMemoryCell<T, RT>> _Memory = new List<QMemoryCell<T, RT>>();
        public RewardAccumulator R { get; } = new RewardAccumulator();
        public QEpisodicMemoryCell()
        {
        }
        public IList<QMemoryCell<T, RT>> Memory { get { return _Memory; } }
        public QEpisodicMemoryCell<T,RT> Add(QMemoryCell<T,RT> m)
        {
            _Memory.Add(m);
            return (this);
        }
        public void AddTo(List<QMemoryCell<T, RT>> target)
        {
            target.AddRange(_Memory);
        }
        public int Size
        {
            get
            {
                return (_Memory.Count);
            }
        }
    }

    /// <summary>Replay helpers shared by the episodic memories.</summary>
    internal static class EpisodicReplay
    {
        /// <summary>
        /// Replays whole episodes until steps steps have been replayed, cutting the last one short. Each episode is
        /// drawn at random from the memory that chooseMemory returns for that draw, and its cells are replayed in the
        /// order inReplayOrder gives. A draw that finds nothing to replay is a miss, and steps misses in a row end
        /// the replay, so memories that hold only empty episodes cannot stall it.
        /// </summary>
        internal static void PlaySteps<T, RT>(int steps, Func<List<QEpisodicMemoryCell<T, RT>>> chooseMemory,
            Func<IList<QMemoryCell<T, RT>>, IEnumerable<QMemoryCell<T, RT>>> inReplayOrder, QRandom ran, HyperParams hp,
            Func<QMemoryCell<T, RT>, HyperParams, bool> callback) where RT : IReward
        {
            int replayed = 0;
            int misses = 0;
            while (replayed < steps && misses < steps)
            {
                List<QEpisodicMemoryCell<T, RT>> l = chooseMemory();
                if (l == null || l.Count == 0)
                {
                    misses++;
                    continue;
                }
                QEpisodicMemoryCell<T, RT> episode = l[ran.Ran.Next(0, l.Count)];
                if (episode.Memory.Count == 0)
                {
                    misses++;
                    continue;
                }
                misses = 0;
                foreach (QMemoryCell<T, RT> cell in inReplayOrder(episode.Memory))
                {
                    replayed++;
                    if (!callback(cell, hp) || replayed == steps)
                    {
                        break;
                    }
                }
            }
        }
    }

    [Serializable]
    public class QEpisodicMemory<T, RT> : QMemory<T, RT> where RT : IReward
    {
        protected List<QEpisodicMemoryCell<T, RT>> _EpisodicMemory = new List<QEpisodicMemoryCell<T, RT>>();
        private QEpisodicMemoryCell<T, RT> _CurrentEpisode = null;

        public QEpisodicMemory(int maxSize, QRandom ran) : base(maxSize, ran: ran)
        {
        }

        /// <summary>
        /// When true, Playback's count is the number of steps to replay rather than a slice of episodes: whole
        /// episodes are drawn at random and replayed, last step first when <see cref="QMemory{T,RT}.ReplayBackward"/>
        /// is set, until count steps have been replayed, the last episode cut short. A replay budget then means the
        /// same for episodic and transition memories. Off by default.
        /// </summary>
        public bool CountSteps { get; set; } = false;

        public override void StartEpisode()
        {
            RememberCurrentEpisode();
            _CurrentEpisode = new QEpisodicMemoryCell<T, RT>();
        }
        public override void EndEpisode()
        {
            RememberCurrentEpisode();
            _CurrentEpisode = null;
        }

        private void RememberCurrentEpisode()
        {
            if (_CurrentEpisode != null)
            {
                if (_EpisodicMemory.Count == _MaxSize)
                {
                    _EpisodicMemory.RemoveAt(0);
                }
                _EpisodicMemory.Add(_CurrentEpisode);
            }
        }

        public override void ReplayEpisode(HyperParams hp, Func<QMemoryCell<T,RT>, HyperParams, bool> callback)
        {
            if (_EpisodicMemory.Count == 0)
            {
                return;
            }
            foreach (QMemoryCell<T,RT> mem in InReplayOrder(_EpisodicMemory[_EpisodicMemory.Count - 1].Memory))
            {
                callback(mem, hp);
            }
        }

        public override bool Playback(int count, HyperParams hp, Func<QMemoryCell<T,RT>, HyperParams, bool> callback)
        {
            if (CountSteps)
            {
                EpisodicReplay.PlaySteps(count, SelectMemory, InReplayOrder, _random, hp, callback);
                return true;
            }
            List<QEpisodicMemoryCell<T,RT>> qMemoryCells = EpisodicSlice(SelectMemory(), count);
            if (qMemoryCells == null)
            {
                return true;
            }
            for (int i = 0; i < qMemoryCells.Count; i++)
            {
                int ix = _random.Ran.Next(0, qMemoryCells.Count);
                QEpisodicMemoryCell<T,RT> episode = qMemoryCells[ix];
                foreach (QMemoryCell<T,RT> cell in InReplayOrder(episode.Memory))
                    if (!callback(cell, hp))
                    {
                        break;
                    }
            }
            return true;
        }

        public override QMemoryCell<T,RT> Remember(T s, QAction a, T sprime, QAction aprime, RT r)
        {
            QMemoryCell<T,RT> m = new QMemoryCell<T,RT>() { s = s, a = a, sprime = sprime, r = r, aprime = aprime };
            if (_CurrentEpisode == null)
            {
                _CurrentEpisode = new QEpisodicMemoryCell<T,RT>();
            }
            _CurrentEpisode.Add(m);
            _CurrentEpisode.R.Add(r);
            return (m);
        }

        protected virtual List<QEpisodicMemoryCell<T,RT>> SelectMemory()
        {
            return _EpisodicMemory;
        }
        protected virtual List<QEpisodicMemoryCell<T,RT>> EpisodicSlice(List<QEpisodicMemoryCell<T,RT>> l, int count = 0)
        {
            List<QEpisodicMemoryCell<T,RT>> sub = null;
            if (count < 0)
            {
                int i = l.Count + count;
                if (i < 0)
                {
                    sub = l;
                }
                else
                {
                    sub = l.GetRange(i, -count);
                }
            }
            else if (count > 0)
            {
                if (count > l.Count)
                {
                    sub = l;
                }
                else
                {
                    sub = l.GetRange(0, count);
                }
            }
            return sub;
        }

        protected virtual List<QMemoryCell<T,RT>> SliceList(List<QEpisodicMemoryCell<T,RT>> l, int count = 0)
        {
            List<QEpisodicMemoryCell<T,RT>> sub = EpisodicSlice(l, count);
            if (sub != null)
            {
                List<QMemoryCell<T,RT>> m = new List<QMemoryCell<T,RT>>();
                foreach (QEpisodicMemoryCell<T,RT> q in sub)
                {
                    q.AddTo(m);
                }
                return (m);
            }
            return (null);
        }
        /// <summary>
        /// Removes all elements from the memory.
        /// </summary>
        public override void Clear()
        {
            _EpisodicMemory.Clear();
            _CurrentEpisode = null;
        }
        /// <summary>
        /// Returns the number of elements in the memory list.
        /// </summary>
        public override int Size
        {
            get
            {
                return (_EpisodicMemory.Count);
            }
        }
        /// <summary>
        /// Returns a subset of the tuples in the memory.
        /// </summary>
        /// <param name="count">Negative to pull from the end, and positive to pull from the start. -1 is the last element in the memory, and 1 is the first element.</param>
        /// <returns></returns>
        public override List<QMemoryCell<T,RT>> Slice(int count = 0)
        {
            return (SliceList(SelectMemory(), count));
        }
    }
}
