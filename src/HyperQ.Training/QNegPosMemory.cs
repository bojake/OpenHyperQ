using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Training
{
    [Serializable]
    public class QNegPosMemory<T,RT> : QMemory<T,RT> where RT : IReward
    {
        private QRandom _Ran = null;
        private List<QMemoryCell<T,RT>> _NegMemory = new List<QMemoryCell<T,RT>>();
        private List<QMemoryCell<T,RT>> _PosMemory = new List<QMemoryCell<T,RT>>();
        /// <summary>
        /// Likelihood [0,1) that the negative memory will be used for memory recall, when
        /// negative memory has memories.
        /// </summary>
        public double NegativeRecallLikelihood { get; set; } = 0.3;
        /// <summary>
        /// Likelihood [0,1) that the positive memory will be used for memory recall, when
        /// positive memory has memories.
        /// </summary>
        public double PositiveRecallLikelihood { get; set; } = 0.3;
        /// <summary>
        /// Likelihood that the last memory (LIFO) recorded is culled from the memory when
        /// the memory is full, otherwise the first memory (FIFO) is culled.
        /// </summary>
        public double CullLIFOLikelihood { get; set; } = 0.5;

        private bool _KeepExtremes = false;
        /// <summary>
        /// When true, each sorted memory keeps its most extreme entries: the negative memory its most negative
        /// rewards, the positive memory its most positive. A full memory drops its mildest entry, the oldest among
        /// equals, and <see cref="CullLIFOLikelihood"/> is ignored. The positive memory is then sorted most positive
        /// first, so a positive count passed to Slice takes the most extreme entries of either memory.
        /// Off by default. A full memory then drops its first or last entry by <see cref="CullLIFOLikelihood"/>; both
        /// memories are sorted ascending, so that is its most extreme or its mildest entry, and over time both settle
        /// around their median reward. Set it before remembering anything, or after <see cref="Clear"/>.
        /// </summary>
        public bool KeepExtremes
        {
            get
            {
                return (_KeepExtremes);
            }
            set
            {
                if (value != _KeepExtremes && HasRankedEntries)
                {
                    throw new InvalidOperationException("KeepExtremes decides how the negative and positive memories are sorted; set it before remembering anything, or after Clear().");
                }
                _KeepExtremes = value;
            }
        }

        /// <summary>
        /// When true, Playback chooses the negative, positive or recent memory for each draw, with the recall
        /// likelihoods. Off by default: one memory is chosen per Playback call, and all of its draws come from it.
        /// </summary>
        public bool ChooseListPerDraw { get; set; } = false;

        public QNegPosMemory(int size, QRandom ran) : base(size,ran)
        {
            _Ran = ran;
        }

        /// <summary>True when the sorted memories hold entries, after which <see cref="KeepExtremes"/> cannot change.</summary>
        protected virtual bool HasRankedEntries
        {
            get
            {
                return (_NegMemory.Count > 0 || _PosMemory.Count > 0);
            }
        }

        /// <summary>
        /// The index that a full sorted memory of the given size drops: its last entry, the mildest, when keeping
        /// extremes; otherwise its last entry with probability <see cref="CullLIFOLikelihood"/>, else its first.
        /// </summary>
        protected int CullIndex(int count)
        {
            if (KeepExtremes)
            {
                return (count - 1);
            }
            return (_Ran.Ran.NextDouble() < CullLIFOLikelihood ? count - 1 : 0);
        }

        /// <summary>
        /// The memory that a draw p in [0,1) recalls from: the negative memory when p is below
        /// <see cref="NegativeRecallLikelihood"/>, then the positive memory for the next
        /// <see cref="PositiveRecallLikelihood"/>, else the recent one. An empty memory passes the draw on.
        /// </summary>
        protected List<L> Choose<L>(double p, List<L> neg, List<L> pos, List<L> recent)
        {
            if (p < NegativeRecallLikelihood && neg.Count > 0)
            {
                return (neg);
            }
            if (p - NegativeRecallLikelihood < PositiveRecallLikelihood && pos.Count > 0)
            {
                return (pos);
            }
            return (recent);
        }


        public override QMemoryCell<T,RT> Remember(T s, QAction a, T sprime, QAction aprime, RT r)
        {
            QMemoryCell<T,RT> m = base.Remember(s, a, sprime, aprime, r);
            if (r[0] < 0.0)
            {
                if (_NegMemory.Count == _MaxSize)
                {
                    _NegMemory.RemoveAt(CullIndex(_NegMemory.Count));
                }
                int i = bisect_left(_NegMemory, m);
                _NegMemory.Insert(i, m);
            }
            else
            {
                if (_PosMemory.Count == _MaxSize)
                {
                    _PosMemory.RemoveAt(CullIndex(_PosMemory.Count));
                }
                int i = bisect_left(_PosMemory, m, ascending: !KeepExtremes);
                _PosMemory.Insert(i, m);
            }
            return (m);
        }

        /// <summary>
        /// Clears all of the memory
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            _NegMemory.Clear();
            _PosMemory.Clear();
        }

        private int bisect_left(List<QMemoryCell<T,RT>> a, QMemoryCell<T,RT> x, int start=0, int end=-1, bool ascending = true)
        {
            if (end == -1)
            {
                end = a.Count;
            }
            while (start < end)
            {
                int mid = (start + end) / 2;
                RT r1 = a[mid].r;
                if (ascending)
                {
                    if (r1[0] < x.r[0])
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
                    if (r1[0] > x.r[0])
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
        /// <summary>
        /// Return a sublist of tuples from the memory. 30% of the time it will return the negative 
        /// memory, and 30% the positive memory, and the remainder it will use the default memory.
        /// </summary>
        /// <param name="count">Negative to pull from the end, Positive to pull from the start.</param>
        /// <returns>A subset of tuples in the negative or positive memory.  -1 is the last element in the memory, and 1 is the first element.</returns>
        public override List<QMemoryCell<T,RT>>  Slice(int count = -1)
        {
            return (SliceList(Choose(Ran.Ran.NextDouble(), _NegMemory, _PosMemory, _Memory), count));
        }

        /// <summary>
        /// Replays count random draws. By default all of them come from the one memory that <see cref="Slice"/>
        /// chooses for the call. With <see cref="ChooseListPerDraw"/>, each draw chooses the negative, positive or
        /// recent memory, each sliced by count, with the recall likelihoods.
        /// </summary>
        public override bool Playback(int count, HyperParams hp, Func<QMemoryCell<T,RT>, HyperParams, bool> callback)
        {
            if (!ChooseListPerDraw)
            {
                return (base.Playback(count, hp, callback));
            }
            List<QMemoryCell<T,RT>> neg = SliceList(_NegMemory, count);
            List<QMemoryCell<T,RT>> pos = SliceList(_PosMemory, count);
            List<QMemoryCell<T,RT>> recent = SliceList(_Memory, count);
            for (int i = 0; i < count; i++)
            {
                List<QMemoryCell<T,RT>> l = Choose(Ran.Ran.NextDouble(), neg, pos, recent);
                if (l.Count == 0)
                {
                    continue;
                }
                if (!callback(l[Ran.Ran.Next(0, l.Count)], hp))
                {
                    break;
                }
            }
            return true;
        }
    }
}
