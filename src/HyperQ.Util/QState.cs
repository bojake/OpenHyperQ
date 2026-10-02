using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    [Serializable]
    public class QStateDecimalComparer : IComparer<decimal>
    {
        public int Compare(decimal d1, decimal d2)
        {
            if (d1 < d2)
            {
                return (-1);
            }
            if (d1 > d2)
            {
                return (1);
            }
            return (0);
        }
    }
    [Serializable]
    public class QStateIntComparer : IComparer<int>
    {
        public int Compare(int d1, int d2)
        {
            if (d1 < d2)
            {
                return (-1);
            }
            if (d1 > d2)
            {
                return (1);
            }
            return (0);
        }
    }
    /// <summary>
    /// Represents a state instance in a Q learner. Two states are equal when they hold equal elements in the same
    /// order, and the hash code follows the elements in order. A state is mutable (Push, PopFirst, PopLast): one
    /// that serves as a dictionary or set key must not be changed while it is a key.
    /// </summary>
    [Serializable]
    public class QState<T>: IEquatable<QState<T>>
    {
        private List<T> _Elements = new List<T>();

        /// <summary>
        /// Compares states by value, as <see cref="QState{T}.Equals(QState{T})"/> does.
        /// </summary>
        public class EqualityComparer : IEqualityComparer<QState<T>>
        {
            public bool Equals(QState<T> x, QState<T> y)
            {
                return x is null ? y is null : x.Equals(y);
            }

            public int GetHashCode(QState<T> x)
            {
                return x is null ? 0 : x.GetHashCode();
            }
        }

        /// <summary>
        /// True when <paramref name="q"/> holds equal elements in the same order. (This used to compare the
        /// hash codes, which XOR-ed the elements' hashes, so [1, 2] equaled [2, 1], [3, 3] equaled [5, 5] and
        /// the empty state, and any hash collision counted as equality.)
        /// </summary>
        public bool Equals(QState<T> q)
        {
            if (q is null)
            {
                return false;
            }
            if (ReferenceEquals(this, q))
            {
                return true;
            }
            if (q._Elements.Count != _Elements.Count)
            {
                return false;
            }
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < _Elements.Count; i++)
            {
                if (!comparer.Equals(_Elements[i], q._Elements[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object q)
        {
            return Equals(q as QState<T>);
        }
        public QState(T[] d = null)
        {
            if (d != null)
            {
                _Elements = new List<T>(d);
            }
        }

        public bool IsSame(QState<T> q, IComparer<T> comp)
        {
            if (q == null)
            {
                return (false);
            }
            if (q._Elements.Count != _Elements.Count)
            {
                return (false);
            }
            for (int i = 0; i < _Elements.Count; i++)
            {
                if (comp.Compare(_Elements[i], q._Elements[i]) != 0)
                {
                    return (false);
                }
            }
            return (true);
        }

        public QState<T> Clone()
        {
            return (new QState<T>(new List<T>(_Elements)));
        }

        public QState<T> Slice(int n)
        {
            List<T> e = new List<T>();
            for (int i = 0; i < n; i++)
            {
                e.Add(_Elements[i]);
            }
            return (new QState<T>(new List<T>(e)));
        }
        public QState<T> SliceFromStart(int n)
        {
            List<T> e = new List<T>();
            for (int i = 0; i < n; i++)
            {
                e.Add(_Elements[i]);
            }
            return (new QState<T>(new List<T>(e)));
        }
        public QState<T> SliceToEnd(int n)
        {
            List<T> e = new List<T>();
            for (int i = 0; i < n; i++)
            {
                e.Add(_Elements[_Elements.Count-n+i]);
            }
            return (new QState<T>(new List<T>(e)));
        }

        /// <summary>
        /// For private cloning functions
        /// </summary>
        /// <param name="e"></param>
        private QState(List<T> e)
        {
            _Elements = e;
        }

        /// <summary>
        /// Returns a new enumerator for this state.
        /// </summary>
        public QStateEnum<T> Enumerator
        {
            get
            {
                return (new QStateEnum<T>(this));
            }
        }

        /// <summary>
        /// Returns the number of items in the state list.
        /// </summary>
        public int Count
        {
            get
            {
                return (_Elements.Count);
            }
        }

        /// <summary>
        /// Array accessor for the elements. No bounds checks are made.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index]
        {
            get
            {
                return (_Elements[index]);
            }
        }

        /// <summary>
        /// Returns true if there is a value at the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool HasValue(int index)
        {
            if (index >= _Elements.Count)
            {
                return (false);
            }
            return (true);
        }

        /// <summary>
        /// Adds the given value to the end of the element list
        /// </summary>
        /// <param name="d"></param>
        public QState<T> Push(T d)
        {
            _Elements.Add(d);
            return (this);
        }

        /// <summary>
        /// Returns the first value from the element list and removes it.
        /// </summary>
        /// <returns></returns>
        public T PopFirst()
        {
            T d = _Elements[0];
            _Elements.RemoveAt(0);
            return (d);
        }

        /// <summary>
        /// Returns the last value from the element list and removes it.
        /// </summary>
        /// <returns></returns>
        public T PopLast()
        {
            if (_Elements.Count == 0)
            {
                throw (new IndexOutOfRangeException("Element list is empty."));
            }
            int ix = _Elements.Count - 1;
            T d = _Elements[ix];
            _Elements.RemoveAt(ix);
            return (d);
        }

        /// <summary>
        /// Returns the first entry from the list and returns it in the given
        /// out parameter. The returned value is the new QState with the
        /// shorter element list.
        /// </summary>
        /// <param name="v">The first value in the list</param>
        /// <returns>this</returns>
        public QState<T> PopFirst(out T v)
        {
            v = PopFirst();
            return (this);
        }

        /// <summary>
        /// Removes the last entry from the list and returns it in the given
        /// out parameter. The returned value is the new QState with the
        /// shorter element list. 
        /// </summary>
        /// <param name="v">The last value in the list</param>
        /// <returns>this</returns>
        public QState<T> PopLast(out T v)
        {
            v = PopLast();
            return (this);
        }

        /// <summary>
        /// Combines the elements' hash codes in order. The combination is deterministic (no per-process seed), so
        /// a state hashes the same way in every run.
        /// </summary>
        public override int GetHashCode()
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            unchecked
            {
                int h = 17;
                foreach (T e in _Elements)
                {
                    h = h * 31 + (e is null ? 0 : comparer.GetHashCode(e));
                }
                return h;
            }
        }
    }
}
