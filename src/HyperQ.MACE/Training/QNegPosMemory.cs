using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.MACE.Training
{
    [Serializable]
    public class QNegPosMemory<T, RT> : QMemory<T, RT> where RT : IReward
    {
        private QRandom _Ran = null;
        private List<QMemoryCell<T, RT>> _NegMemory = new List<QMemoryCell<T, RT>>();
        private List<QMemoryCell<T, RT>> _PosMemory = new List<QMemoryCell<T, RT>>();
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
        public QNegPosMemory(int size, QRandom ran = null) : base(size, ran)
        {
            _Ran = ran ?? QRandom.Instance;
        }


        public override QMemoryCell<T, RT> Remember(T s, QAction[] a, T sprime, QAction[] aprime, RT r)
        {
            QMemoryCell<T, RT> m = base.Remember(s, a, sprime, aprime, r);
            if (r[0] < 0.0)
            {
                if (_NegMemory.Count == _MaxSize)
                {
                    int ix = 0;
                    if (_Ran.Ran.NextDouble() < CullLIFOLikelihood)
                    {
                        ix = _NegMemory.Count - 1;
                    }
                    _NegMemory.RemoveAt(ix);
                }
                int i = bisect_left(_NegMemory, m);
                _NegMemory.Insert(i, m);
            }
            else
            {
                if (_PosMemory.Count == _MaxSize)
                {
                    int ix = 0;
                    if (_Ran.Ran.NextDouble() < CullLIFOLikelihood)
                    {
                        ix = _PosMemory.Count - 1;
                    }
                    _PosMemory.RemoveAt(ix);
                }
                int i = bisect_left(_PosMemory, m);
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

        private int bisect_left(List<QMemoryCell<T, RT>> a, QMemoryCell<T, RT> x, int start = 0, int end = -1, bool ascending = true)
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
        public override List<QMemoryCell<T, RT>> Slice(int count = -1)
        {
            double p = Ran.Ran.NextDouble();
            List<QMemoryCell<T, RT>> l = _Memory;
            if (p < NegativeRecallLikelihood && _NegMemory.Count > 0)
            {
                l = _NegMemory;
            }
            else if (p - NegativeRecallLikelihood < PositiveRecallLikelihood && _PosMemory.Count > 0)
            {
                l = _PosMemory;
            }
            return (SliceList(l, count));
        }
    }

}
