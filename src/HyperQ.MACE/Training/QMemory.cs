using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.MACE.Training
{
    [Serializable]
    public struct QMemoryCell<T,RT>
    {
        public T s;
        public T sprime;
        public QAction[] a;
        public QAction[] aprime;
        public RT r;
    }

    [Serializable]
    public class QMemory<T, RT>
    {
        protected List<QMemoryCell<T, RT>> _Memory = new List<QMemoryCell<T, RT>>();
        protected List<QMemoryCell<T, RT>> _Episode = new List<QMemoryCell<T, RT>>();
        protected int _MaxSize = 500;
        protected QRandom _random;

        public QMemory(int maxSize, QRandom ran = null)
        {
            _MaxSize = maxSize;
            _random = ran;
            if (ran == null)
                _random = QRandom.Instance;
        }

        public virtual void StartEpisode()
        {
            _Episode.Clear();
        }
        public virtual void EndEpisode()
        {
            // Do nothing
        }

        public virtual int Capacity
        {
            get
            {
                return (_MaxSize);
            }
        }

        protected virtual QRandom Ran
        {
            get
            {
                return (_random);
            }
        }

        public virtual void ReplayEpisode(HyperParams hp, Func<QMemoryCell<T, RT>, HyperParams, bool> callback)
        {
            foreach (QMemoryCell<T, RT> mem in _Episode)
            {
                callback(mem, hp);
            }
        }

        public virtual bool Playback(int count, HyperParams hp, Func<QMemoryCell<T, RT>, HyperParams, bool> callback)
        {
            List<QMemoryCell<T, RT>> qMemoryCells = Slice(count);
            if (qMemoryCells == null)
            {
                return true;
            }
            for (int i = 0; i < count; i++)
            {
                int x = _random.Ran.Next(0, qMemoryCells.Count);
                if (!callback(qMemoryCells[x], hp))
                {
                    break;
                }
            }
            return true;
        }

        public virtual QMemoryCell<T, RT> Remember(T s, QAction[] a, T sprime, QAction[] aprime, RT r)
        {
            QMemoryCell<T, RT> m = new QMemoryCell<T, RT>() { s = s, a = a, sprime = sprime, r = r, aprime = aprime };
            if (_Memory.Count == _MaxSize)
            {
                _Memory.RemoveAt(0);
            }
            _Memory.Add(m);
            return (m);
        }

        protected virtual List<QMemoryCell<T, RT>> SliceList(List<QMemoryCell<T, RT>> l, int count = 0)
        {
            if (count < 0)
            {
                int i = l.Count + count;
                if (i < 0)
                {
                    return (l);
                }
                return (l.GetRange(i, -count));
            }
            else if (count > 0)
            {
                if (count > l.Count)
                {
                    return (l);
                }
                return (l.GetRange(0, count));
            }
            return (l);
        }
        /// <summary>
        /// Removes all elements from the memory.
        /// </summary>
        public virtual void Clear()
        {
            _Memory.Clear();
            _Episode.Clear();
        }
        /// <summary>
        /// Returns the number of elements in the memory list.
        /// </summary>
        public virtual int Size
        {
            get
            {
                return (_Memory.Count);
            }
        }
        /// <summary>
        /// Returns a subset of the tuples in the memory.
        /// </summary>
        /// <param name="count">Negative to pull from the end, and positive to pull from the start. -1 is the last element in the memory, and 1 is the first element.</param>
        /// <returns></returns>
        public virtual List<QMemoryCell<T, RT>> Slice(int count = 0)
        {
            return (SliceList(_Memory, count));
        }
    }
}
