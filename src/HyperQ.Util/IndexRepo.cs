using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    /// <summary>
    /// Hands out the indexes of one index space. Mappers whose indexes must not collide share one repository
    /// (every level of a hyper mapper does); otherwise each mapper owns its own. There is no process-wide
    /// repository.
    /// </summary>
    [Serializable]
    public class IndexRepo
    {
        private List<uint> _FreeIndices = new List<uint>();
        private uint _nextIndex = 0;
        private uint _start = 0;

        /// <summary>
        /// Constructor to make a new repo starting at the given index value.
        /// </summary>
        /// <param name="start">Default of zero</param>
        public IndexRepo(uint start = 0)
        {
            _nextIndex = start;
            _start = start;
        }

        /// <summary>
        /// Sets the next index of the sequence to the given next value.
        /// </summary>
        /// <param name="next"></param>
        public void RestoreAt(uint next)
        {
            _nextIndex = next;
        }

        /// <summary>
        /// Marks an index as taken, as a checkpoint restore does: it leaves the free list and the next index
        /// moves past it.
        /// </summary>
        public void Reserve(uint idx)
        {
            _FreeIndices.Remove(idx);
            if (_nextIndex <= idx)
            {
                _nextIndex = idx + 1;
            }
        }
        /// <summary>
        /// Adjusts the next index by changing the start value of this repo. This only affects the next index
        /// </summary>
        /// <param name="start">The new start of the index sequence</param>
        /// <param name="adjustFreeList">True if the free list will be adjusted. Not recommended</param>
        /// <returns></returns>
        public IndexRepo Reindex(uint start, bool adjustFreeList = false)
        {
            if (_start > start)
            {
                throw (new ArgumentException("The start can not be less than the original start. Negative re-indexing is not supported."));
            }
            uint diff = start - _start;
            _nextIndex += diff;
            if (_FreeIndices.Count > 0)
            {
                List<uint> l = new List<uint>();
                for (int i = 0; i < _FreeIndices.Count; i++)
                {
                    l.Add(_FreeIndices[i] + diff);
                }
                _FreeIndices = l;
            }
            _start = start;
            return (this);
        }
        public IndexRepo Clone()
        {
            IndexRepo c = new IndexRepo();
            c._nextIndex = _nextIndex;
            c._FreeIndices = new List<uint>(_FreeIndices);
            c._start = _start;
            return (c);
        }
        public virtual uint Peek
        {
            get
            {
                return (_nextIndex);
            }
        }
        public virtual void Release(uint idx)
        {
            _FreeIndices.Add(idx);
        }

        public virtual uint Next
        {
            get
            {
                uint i = 0;
                if (_FreeIndices.Count > 0)
                {
                    i = _FreeIndices[0];
                    _FreeIndices.RemoveAt(0);
                }
                else
                {
                    i = _nextIndex;
                    _nextIndex++;
                }
                return (i); // return(_nextIndex++) would work here, but can be confusing
            }
        }

    }
}
