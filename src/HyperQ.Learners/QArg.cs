using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    [Serializable]
    public class GenericComparer<T> : IComparer<T>
        where T : IComparable<T>
    {
        public int Compare(T x, T y)
        {
            return x.CompareTo(y);
        }
    }

    [Serializable]
    public class QArg<T> where T : System.IComparable<T>
    {
        public T Max { get; private set; }
        public T Min { get; private set; }
        public int MaxIndex { get; private set; }
        public int MinIndex { get; private set; }

        private List<Tuple<T, int>> _History = new List<Tuple<T, int>>();

        public QArg()
        {
            MaxIndex = -1;
            MinIndex = -1;
        }

        public QArg(T v, int index) : this()
        {
            Max = v;
            MaxIndex = index;
            Min = v;
            MinIndex = index;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v">The cached value to add</param>
        /// <param name="index">The action index of that added value</param>
        /// <returns>Returns true if the min/max range is invalidated and needs to be exhaustively computed.</returns>
        public bool Set(T v, int index)
        {
            GenericComparer<T> c = new GenericComparer<T>();
            bool extended = false;
            if (MaxIndex == -1 || c.Compare(v,Max) > 0)
            {
                Max = v;
                MaxIndex = index;
                extended = true;
            }
            if (MinIndex == -1 || c.Compare(v,Min) < 0)
            {
                Min = v;
                MinIndex = index;
                extended = true;
            }
            if (!extended)
            {
                // The value is in the range, so it might trigger a new max if the index is the current extrema
                if (MaxIndex == index || MinIndex == index)
                {
                    return (true);
                }
                // Value is in the range, and it does not change the range, so ignore the update
            }
            return (false);
        }
    }
}
