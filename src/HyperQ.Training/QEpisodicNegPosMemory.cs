using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Training
{
    [Serializable]
    public class QEpisodicNegPosMemory<T, RT> : QNegPosMemory<T, RT> where RT : IReward
    {
        /// <summary>
        /// The negative reward memory
        /// </summary>
        protected List<QEpisodicMemoryCell<T, RT>> _NegEpisodicMemory = new List<QEpisodicMemoryCell<T, RT>>();
        /// <summary>
        /// The positive reward memory
        /// </summary>
        protected List<QEpisodicMemoryCell<T, RT>> _PosEpisodicMemory = new List<QEpisodicMemoryCell<T, RT>>();
        /// <summary>
        /// Comprehensive memory
        /// </summary>
        protected List<QEpisodicMemoryCell<T, RT>> _FullEpisodicMemory = new List<QEpisodicMemoryCell<T, RT>>();
        private QEpisodicMemoryCell<T, RT> _CurrentEpisode = null;

        public QEpisodicNegPosMemory(int maxSize, QRandom ran) : base(maxSize, ran: ran)
        {
        }

        /// <summary>
        /// When true, Playback's count is the number of steps to replay rather than a slice of episodes: whole
        /// episodes are drawn at random and replayed, last step first when <see cref="QMemory{T,RT}.ReplayBackward"/>
        /// is set, until count steps have been replayed, the last episode cut short. A replay budget then means the
        /// same for episodic and transition memories. Off by default.
        /// </summary>
        public bool CountSteps { get; set; } = false;

        protected override bool HasRankedEntries
        {
            get
            {
                return (_NegEpisodicMemory.Count > 0 || _PosEpisodicMemory.Count > 0 || base.HasRankedEntries);
            }
        }
        /// <summary>
        /// Stores the current episode memory and starts a new memory episode.
        /// </summary>
        public override void StartEpisode()
        {
            RememberCurrentEpisode();
            _CurrentEpisode = new QEpisodicMemoryCell<T, RT>();
        }
        /// <summary>
        /// Stores the current episode memory and resets the current memory episode.
        /// </summary>
        public override void EndEpisode()
        {
            RememberCurrentEpisode();
            _CurrentEpisode = null;
        }
        public override void ReplayEpisode(HyperParams hp, Func<QMemoryCell<T, RT>, HyperParams, bool> callback)
        {
            if (_FullEpisodicMemory.Count == 0)
            {
                return;
            }
            foreach (QMemoryCell<T, RT> mem in InReplayOrder(_FullEpisodicMemory[_FullEpisodicMemory.Count - 1].Memory))
            {
                callback(mem, hp);
            }
        }
        /// <summary>
        /// Replays episodes from the negative, positive or full memory. By default one memory is chosen per call, its
        /// first count episodes (its last -count when count is negative) are sliced off, and that many random draws
        /// from the slice are replayed. With <see cref="QNegPosMemory{T,RT}.ChooseListPerDraw"/>, each of |count|
        /// draws chooses its own memory and slice. With <see cref="CountSteps"/>, count is a number of steps instead,
        /// and episodes are drawn from the whole of the chosen memory.
        /// </summary>
        public override bool Playback(int count, HyperParams hp, Func<QMemoryCell<T, RT>, HyperParams, bool> callback)
        {
            if (CountSteps)
            {
                List<QEpisodicMemoryCell<T, RT>> chosen = ChooseListPerDraw ? null : SelectMemory();
                EpisodicReplay.PlaySteps(count, () => chosen ?? SelectMemory(), InReplayOrder, Ran, hp, callback);
                return true;
            }
            if (ChooseListPerDraw)
            {
                for (int i = 0; i < Math.Abs(count); i++)
                {
                    List<QEpisodicMemoryCell<T, RT>> slice = EpisodicSlice(SelectMemory(), count);
                    if (slice == null || slice.Count == 0)
                    {
                        continue;
                    }
                    QEpisodicMemoryCell<T, RT> drawn = slice[Ran.Ran.Next(0, slice.Count)];
                    foreach (QMemoryCell<T, RT> cell in InReplayOrder(drawn.Memory))
                        if (!callback(cell, hp))
                        {
                            break;
                        }
                }
                return true;
            }
            List<QEpisodicMemoryCell<T, RT>> qMemoryCells = EpisodicSlice(SelectMemory(), count);
            if (qMemoryCells == null)
            {
                return true;
            }
            for (int i = 0; i < qMemoryCells.Count; i++)
            {
                int ix = Ran.Ran.Next(0, qMemoryCells.Count);
                QEpisodicMemoryCell<T, RT> episode = qMemoryCells[ix];
                foreach (QMemoryCell<T, RT> cell in InReplayOrder(episode.Memory))
                    if (!callback(cell, hp))
                    {
                        break;
                    }
            }
            return true;
        }

        /// <summary>
        /// Save the current episode memory, organize it into the negative
        /// and positive memory slices.
        /// </summary>
        private void RememberCurrentEpisode()
        {
            if (_CurrentEpisode != null)
            {
                _FullEpisodicMemory.Add(_CurrentEpisode);
                if (_FullEpisodicMemory.Count > _MaxSize)
                {
                    _FullEpisodicMemory.RemoveAt(0);
                }
                if (_CurrentEpisode.R.Scalar0 < 0.0)
                {
                    if (_NegEpisodicMemory.Count == _MaxSize)
                    {
                        _NegEpisodicMemory.RemoveAt(CullIndex(_NegEpisodicMemory.Count));
                    }
                    int i = bisect_left(_NegEpisodicMemory, _CurrentEpisode);
                    _NegEpisodicMemory.Insert(i, _CurrentEpisode);
                }
                else
                {
                    if (_PosEpisodicMemory.Count == _MaxSize)
                    {
                        _PosEpisodicMemory.RemoveAt(CullIndex(_PosEpisodicMemory.Count));
                    }
                    int i = bisect_left(_PosEpisodicMemory, _CurrentEpisode, ascending: !KeepExtremes);
                    _PosEpisodicMemory.Insert(i, _CurrentEpisode);
                }
            }
        }

        /// <summary>
        /// Remember the current (s,a)->(s',a') memory and add the reward R to the running
        /// episodic reward.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="a"></param>
        /// <param name="sprime"></param>
        /// <param name="aprime"></param>
        /// <param name="r"></param>
        /// <returns></returns>
        public override QMemoryCell<T, RT> Remember(T s, QAction a, T sprime, QAction aprime, RT r)
        {
            QMemoryCell<T, RT> m = new QMemoryCell<T, RT>() { s = s, a = a, sprime = sprime, r = r, aprime = aprime };
            if (_CurrentEpisode == null)
            {
                _CurrentEpisode = new QEpisodicMemoryCell<T, RT>();
            }
            _CurrentEpisode.Add(m);
            _CurrentEpisode.R.Add(r);
            return (m);
        }

        protected virtual List<QEpisodicMemoryCell<T, RT>> SelectMemory()
        {
            return Choose(Ran.Ran.NextDouble(), _NegEpisodicMemory, _PosEpisodicMemory, _FullEpisodicMemory);
        }
        protected virtual List<QEpisodicMemoryCell<T, RT>> EpisodicSlice(List<QEpisodicMemoryCell<T, RT>> l, int count = 0)
        {
            List<QEpisodicMemoryCell<T, RT>> sub = null;
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

        /// <summary>
        /// Return a sublist of the given list according to the number of items given as count. If count
        /// is negative then items are pulled from the right, otherwise they are pulled from the left.
        /// For instance, -5 would give the last 5 elements, whereas 5 would give the first 5.
        /// </summary>
        /// <param name="l">The list to slice</param>
        /// <param name="count">The cardinality vector of the resulting slice</param>
        /// <returns></returns>
        protected virtual List<QMemoryCell<T, RT>> SliceList(List<QEpisodicMemoryCell<T, RT>> l, int count = 0)
        {
            List<QEpisodicMemoryCell<T, RT>> sub = EpisodicSlice(l, count);
            if (sub != null)
            {
                List<QMemoryCell<T, RT>> m = new List<QMemoryCell<T, RT>>();
                foreach (QEpisodicMemoryCell<T, RT> q in sub)
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
            _NegEpisodicMemory.Clear();
            _PosEpisodicMemory.Clear();
            _FullEpisodicMemory.Clear();
            _CurrentEpisode = null;
        }
        /// <summary>
        /// Returns the number of elements in the memory list.
        /// </summary>
        public override int Size
        {
            get
            {
                return (Math.Max(_NegEpisodicMemory.Count,_PosEpisodicMemory.Count));
            }
        }
        /// <summary>
        /// Returns a subset of the tuples in the memory. Chooses to pull from the negative or positive memory
        /// depending on the NegativeRecallLikelihood and PositiveRecallLikelihood relative to a random value.
        /// </summary>
        /// <param name="count">Negative to pull from the end, and positive to pull from the start. -1 is the last element in the memory, and 1 is the first element.</param>
        /// <returns></returns>
        public override List<QMemoryCell<T, RT>> Slice(int count = 0)
        {
            return (SliceList(SelectMemory(), count));
        }
        /// <summary>
        /// Returns the insertion index of the given x memory cell into the given memory using midpoint testing.
        /// </summary>
        /// <param name="a">The list to insert into</param>
        /// <param name="x">The item to insert</param>
        /// <param name="start">The start in a to place x</param>
        /// <param name="end">The end in a to place x</param>
        /// <param name="ascending">true if the values in a should be in ascending order, false for descending</param>
        /// <returns></returns>
        private int bisect_left(List<QEpisodicMemoryCell<T, RT>> a, QEpisodicMemoryCell<T, RT> x, int start = 0, int end = -1, bool ascending = true)
        {
            if (end == -1)
            {
                end = a.Count;
            }
            while (start < end)
            {
                int mid = (start + end) / 2;
                double r1 = a[mid].R.Scalar0;
                if (ascending)
                {
                    if (r1 < x.R.Scalar0)
                    {
                        start = mid + 1;
                    }
                    else
                    {
                        end = mid;
                    }
                }
                else
                {
                    if (r1 > x.R.Scalar0)
                    {
                        start = mid + 1;
                    }
                    else
                    {
                        end = mid;
                    }
                }
            }
            return (start);
        }
    }
}
