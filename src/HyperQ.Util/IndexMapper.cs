using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace HyperQ.Util
{
    [Serializable]
    public class IndexMapper<T> : IIndexMapper<T>
    {
        /// <summary>
        /// The map from the key to the index
        /// </summary>
        private Dictionary<T, uint> _MapToIndices = new Dictionary<T, uint>();
        /// <summary>
        /// The mapping from the index to the original key
        /// </summary>
        private Dictionary<uint, T> _MapFromIndices = new Dictionary<uint, T>();
        /// <summary>
        /// Hands out the indexes and keeps the ones freed when keys are removed. The mapper's own unless one
        /// was given to share.
        /// </summary>
        private IndexRepo _indices;
        /// <summary>
        /// The max mappable value, defaults to -1 if no max
        /// </summary>
        public T MaxValue { get; set; }

        private uint _RecentQueryResult = default(uint);
        private T _RecentQueryState = default(T);

        /// <summary>
        /// Returns the recent query result for the given state. If the state is not the same as the last query
        /// then the default value is returned.
        /// </summary>
        /// <param name="state">The state to query</param>
        /// <returns>index of the state, if matching the recent state, or default(uint).</returns>
        public uint GetRecentQuery(T state)
        {
            if (EqualityComparer<T>.Default.Equals(state, _RecentQueryState))
            {
                return _RecentQueryResult;
            }
            return default(uint);
        }
        /// <summary>
        /// Sets the cached recent query for the given state and its resulting index.
        /// </summary>
        /// <param name="state">The state that was queried</param>
        /// <param name="result">The result from the mapping</param>
        public void SetRecentQuery(T state, uint result)
        {
            _RecentQueryState = state;
            _RecentQueryResult = result;
        }

        /// <summary>
        /// Returns a randomly selected key from the index map.
        /// </summary>
        /// <param name="ran">The random number generator, can not be null</param>
        /// <returns></returns>
        public virtual uint RandomKey(QRandom ran)
        {
            int ix = ran.Ran.Next(_MapFromIndices.Count);
            return _MapFromIndices.Keys.ElementAt(ix);
        }

        /// <summary>
        /// Returns true if the given index is known, and false if not.
        /// </summary>
        /// <param name="ix">The index to query</param>
        /// <returns>true or false</returns>
        public virtual bool IsKnownIndex(uint ix)
        {
            return _MapFromIndices.ContainsKey(ix);
        }

        #region Metrics
        private RunningAverage _AvgLookup = new RunningAverage(0M);
        public decimal AverageLookupTime
        {
            get
            {
                return _AvgLookup.Value;
            }
        }
        #endregion

        /// <summary>
        /// Creates a new index respository starting at the given index.
        /// </summary>
        /// <param name="start"></param>
        public IndexMapper(uint start)
        {
            _indices = new IndexRepo(start);
        }

        /// <summary>
        /// Creates a mapper with an index repository of its own, starting at zero.
        /// </summary>
        public IndexMapper()
        {
            _indices = new IndexRepo();
        }

        /// <summary>
        /// Creates a mapper that draws its indexes from the given repository, shared with the other mappers
        /// whose indexes must not collide with this one's.
        /// </summary>
        public IndexMapper(IndexRepo repo)
        {
            _indices = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public Dictionary<T,uint>.KeyCollection Keys
        {
            get
            {
                return _MapToIndices.Keys;
            }
        }

        /// <summary>
        /// Enumerates every (key, index) pair held by this mapper.
        /// </summary>
        public IEnumerable<KeyValuePair<T, uint>> Mappings
        {
            get
            {
                return _MapToIndices;
            }
        }
        public virtual IndexRepo Repo
        {
            get
            {
                return _indices;
            }
            set
            {
                _indices = value;
            }
        }
        /// <summary>
        /// Returns the number of keys in the mapping.
        /// </summary>
        public virtual uint MappingCount
        {
            get
            {
                return ((uint)_MapToIndices.Keys.Count);
            }
        }

        public virtual IIndexMapper<T> Clone()
        {
            IndexMapper<T> c = new IndexMapper<T>(_indices);
            c._MapToIndices = new Dictionary<T, uint>(_MapToIndices);
            c._MapFromIndices = new Dictionary<uint, T>(_MapFromIndices);
            return (c);
        }

        /// <summary>
        /// Adds the keys and new index mappings to the given target. The returned 
        /// dictionary contains the new mapping of this MapToIndices values to the
        /// target's new MapToIndices values.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual Dictionary<uint,uint> MergeInto(IndexMapper<T> target)
        {
            Dictionary<uint, uint> remap = new Dictionary<uint, uint>();
            foreach (T key in _MapToIndices.Keys)
            {
                long ltime = DateTime.Now.Ticks;
                uint idx = target[key];
                remap[_MapToIndices[key]] = idx;
                LogQueryTime(DateTime.Now.Ticks - ltime);
            }
            return (remap);
        }

        /// <summary>
        /// Updates the running average time spent per query and the number of queries.
        /// </summary>
        /// <param name="diff"></param>
        private void LogQueryTime(long diff)
        {
            double ms = new TimeSpan(diff).TotalMilliseconds;
            _AvgLookup.Add(ms);
        }
        /// <summary>
        /// Returns true if the given key existed in the mapping and it was removed, otherwise false is returned 
        /// indicating the key did not exist.
        /// </summary>
        /// <param name="key">The key to remove from the mapping.</param>
        /// <returns></returns>
        public virtual bool RemoveKey(T key)
        {
            long ltime = DateTime.Now.Ticks;
            bool b = false;
            uint ix = 0;
            if(_MapToIndices.TryGetValue(key, out ix))
            {
                _indices.Release(ix);
                _MapToIndices.Remove(key);
                _MapFromIndices.Remove(ix);
                b = true;
            }
            LogQueryTime(DateTime.Now.Ticks - ltime);
            return (b);
        }

        /// <summary>
        /// Returns the original value for the given mapped index value.
        /// </summary>
        /// <param name="i">The mapped index</param>
        /// <returns>The original value from the mapped index</returns>
        public virtual T From(uint i)
        {
            long ltime = DateTime.Now.Ticks;
            T x = _MapFromIndices[i];
            LogQueryTime(DateTime.Now.Ticks - ltime);
            return (x);
        }
        /// <summary>
        /// Returns true if the given key is known in the "to" mapping.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual bool Known(T key)
        {
            long ltime = DateTime.Now.Ticks;
            bool x = _MapToIndices.ContainsKey(key);
            LogQueryTime(DateTime.Now.Ticks - ltime);
            return (x);
        }
        /// <summary>
        /// Returns the mapped index of the given mappable value. This is just a wrapper
        /// around the [] accessor.
        /// </summary>
        /// <param name="i">The mappable value</param>
        /// <returns>The mapped index from the original value.</returns>
        public virtual uint To(T i)
        {
            long ltime = DateTime.Now.Ticks;
            uint ix = this[i];
            LogQueryTime(DateTime.Now.Ticks - ltime);
            return (ix);
        }

        /// <summary>
        /// Converts the given key space value into index space.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual uint ToIndex(T key)
        {
            return To(key);
        }

        /// <summary>
        /// Undo the mapping and return the key space value of the given index space value.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public virtual T FromIndex(uint index)
        {
            return From(index);
        }
        public virtual uint this[T key]
        {
            // To - return the mapping of key TO the index
            get
            {
                long ltime = DateTime.Now.Ticks;
                uint ix = GetRecentQuery(key);
                if(ix != default(uint))
                {
                    return (ix);
                }
                if (!_MapToIndices.ContainsKey(key))
                {
                    ix = _indices.Next;
                    _MapToIndices[key] = ix;
                    _MapFromIndices[ix] = key;
                }
                else
                {
                    ix = _MapToIndices[key];
                }
                LogQueryTime(DateTime.Now.Ticks - ltime);
                return (ix);
            }
            private set
            {
                long ltime = DateTime.Now.Ticks;
                _MapToIndices[key] = value;
                _MapFromIndices[value] = key;
                SetRecentQuery(key, value);
                LogQueryTime(DateTime.Now.Ticks - ltime);
            }
        }

        public void Dump(TextWriter sw = null)
        {
            sw = sw ?? Console.Out;
            sw.WriteLine("IndexMapper: nextIndex={0}, #indices={1}, avg {2:F5}ms over {3} queries.", _indices.Peek, _MapToIndices.Keys.Count,_AvgLookup.Value, _AvgLookup.Count);
        }

        // ── checkpoints ──

        /// <summary>
        /// Restores a (key, index) pair recorded by a checkpoint. The index is reserved in the repository so
        /// later allocations cannot collide with it.
        /// </summary>
        public virtual void Restore(T key, uint index)
        {
            _MapToIndices[key] = index;
            _MapFromIndices[index] = key;
            _indices.Reserve(index);
        }

        /// <summary>Forgets every mapping. Indices already handed out stay reserved in the repository.</summary>
        public virtual void Clear()
        {
            _MapToIndices.Clear();
            _MapFromIndices.Clear();
            _RecentQueryState = default(T);
            _RecentQueryResult = default(uint);
        }

        /// <summary>Writes every (key, index) pair and the repository's next index.</summary>
        public void SaveCheckpoint(BinaryWriter writer, IQKeySerializer<T> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            writer.Write(_MapToIndices.Count);
            foreach (KeyValuePair<T, uint> kv in _MapToIndices)
            {
                keys.Serialize(writer.BaseStream, kv.Key);
                writer.Write(kv.Value);
            }
            writer.Write(_indices.Peek);
        }

        /// <summary>Replaces the mappings with those written by <see cref="SaveCheckpoint"/>.</summary>
        public void LoadCheckpoint(BinaryReader reader, IQKeySerializer<T> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                T key = keys.Deserialize(reader.BaseStream);
                uint index = reader.ReadUInt32();
                Restore(key, index);
            }
            uint next = reader.ReadUInt32();
            if (_indices.Peek < next)
            {
                _indices.RestoreAt(next);
            }
        }
    }
}
