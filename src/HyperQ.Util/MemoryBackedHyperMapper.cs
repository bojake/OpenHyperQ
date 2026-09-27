using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    /// <summary>
    /// Deep mapping class that keeps track of hierarchical indexing.
    /// </summary>
    /// <typeparam name="T">The underlying state type</typeparam>
    [Serializable]
    public sealed class MemoryBackedHyperMapper<T>
    {
        private IIndexMapper<T> _Final;
        private Dictionary<T, MemoryBackedHyperMapper<T>> _Sub;
        private bool IsEmpty => (_Final == null || _Final.MappingCount == 0) && (_Sub == null || _Sub.Count == 0);

        public MemoryBackedHyperMapper(IndexRepo repo = null)
        {
            if (repo != null)
            {
                _Final = new IndexMapper<T>();
                _Final.Repo = repo;
            }
        }

        public T From(uint index)
        {
            if (_Final != null)
            {
                return _Final.FromIndex(index);
            }
            if (_Sub != null)
            {
                foreach (T key in _Sub.Keys)
                {
                    try
                    {
                        return _Sub[key].From(index);
                    }
                    catch (Exception)
                    {
                        // Ignore this here because we expect the argument exception
                    }
                }
            }
            throw new ArgumentOutOfRangeException($"{index} is not known in the map");
        }

        public MemoryBackedHyperMapper<T> Clone()
        {
            MemoryBackedHyperMapper<T> h = new MemoryBackedHyperMapper<T>();
            if (_Final != null)
            {
                h._Final = _Final.Clone();
            }
            if (_Sub != null)
            {
                h._Sub = new Dictionary<T, MemoryBackedHyperMapper<T>>();
                foreach (T key in _Sub.Keys)
                {
                    h._Sub[key] = _Sub[key].Clone();
                }
            }
            return h;
        }

        /// <summary>
        /// Returns the number of keys mapped anywhere in this hyper path: the keys held at this level plus the
        /// keys held by every sub path. A learner whose states all have the same number of elements therefore
        /// sees the number of distinct states it has mapped. (This used to multiply the level sizes, which is
        /// not a count of anything and overflows quickly.)
        /// </summary>
        public uint MappingCount
        {
            get
            {
                uint count = 0;
                if (_Sub != null)
                {
                    foreach (MemoryBackedHyperMapper<T> h in _Sub.Values)
                    {
                        count += h.MappingCount;
                    }
                }
                if (_Final != null)
                {
                    count += _Final.MappingCount;
                }
                return count;
            }
        }

        /// <summary>
        /// Returns the number of keys mapped exactly <paramref name="depth"/> elements below this level, which
        /// is the number of states with that many elements. Depth 1 counts the keys held at this level.
        /// </summary>
        public uint MappingCountAtDepth(int depth)
        {
            if (depth <= 0)
            {
                return 0;
            }
            if (depth == 1)
            {
                return _Final != null ? _Final.MappingCount : 0;
            }
            uint count = 0;
            if (_Sub != null)
            {
                foreach (MemoryBackedHyperMapper<T> h in _Sub.Values)
                {
                    count += h.MappingCountAtDepth(depth - 1);
                }
            }
            return count;
        }

        /// <summary>
        /// Returns a snapshot of every mapped state with its index. Each state lists the key elements from this
        /// level down to the mapped key, so a mapper that only ever saw n-element states yields n-element states.
        /// </summary>
        public List<KeyValuePair<QState<T>, uint>> Mappings()
        {
            List<KeyValuePair<QState<T>, uint>> result = new List<KeyValuePair<QState<T>, uint>>();
            CollectMappings(new List<T>(), result);
            return result;
        }

        private void CollectMappings(List<T> prefix, List<KeyValuePair<QState<T>, uint>> result)
        {
            IndexMapper<T> final = _Final as IndexMapper<T>;
            if (final != null)
            {
                foreach (KeyValuePair<T, uint> kv in final.Mappings)
                {
                    T[] path = new T[prefix.Count + 1];
                    prefix.CopyTo(path);
                    path[prefix.Count] = kv.Key;
                    result.Add(new KeyValuePair<QState<T>, uint>(new QState<T>(path), kv.Value));
                }
            }
            if (_Sub != null)
            {
                foreach (KeyValuePair<T, MemoryBackedHyperMapper<T>> kv in _Sub)
                {
                    prefix.Add(kv.Key);
                    kv.Value.CollectMappings(prefix, result);
                    prefix.RemoveAt(prefix.Count - 1);
                }
            }
        }
        public bool RemoveKey(T key)
        {
            if (_Final != null)
            {
                if (_Final.RemoveKey(key))
                {
                    return true;
                }
            }
            if (_Sub != null && _Sub.ContainsKey(key))
            {
                if (_Sub[key].RemoveKey(key))
                {
                    return true;
                }
            }
            return false;
        }

        public bool RemoveKey(QStateEnum<T> keys)
        {
            if (keys.HasNext)
            {
                if (_Sub == null)
                {
                    // Does not have a sub key dictionary, but there is a key next in the list. This is
                    // not a known state
                    return false;
                }
                T k = keys.Value;
                if (!_Sub.ContainsKey(k))
                {
                    // The key is not known, return
                    return false;
                }
                keys.MoveNext();
                bool removed = _Sub[k].RemoveKey(keys);
                if (removed && _Sub[k].IsEmpty)
                {
                    _Sub.Remove(k);
                }
                return removed;
            }
            if (_Final == null)
            {
                // No final value, but we are at the final value, which is an invalid state.
                return false;
            }
            if (_Final.Known(keys.Value))
            {
                _Final.RemoveKey(keys.Value);
                return true;
            }
            return false;
        }

        public bool Known(T key)
        {
            if (_Final != null && _Final.Known(key))
            {
                return true;
            }
            if (_Sub != null && _Sub.ContainsKey(key))
            {
                return _Sub[key].Known(key);
            }
            return false;
        }

        public bool Known(QStateEnum<T> keys)
        {
            if (keys.HasNext)
            {
                if (_Sub == null)
                {
                    // Does not have a sub key dictionary, but there is a key next in the list. This is
                    // not a known state
                    return false;
                }
                T k = keys.Value;
                if (!_Sub.ContainsKey(k))
                {
                    // The key is not known, return
                    return false;
                }
                keys.MoveNext();
                return _Sub[k].Known(keys);
            }
            if (_Final == null)
            {
                // No final value, but we are at the final value, which is an invalid state.
                return false;
            }
            if (_Final.Known(keys.Value))
            {
                return true;
            }
            return false;
        }

        public uint this[T key] 
        {
            get
            {
                if (_Final == null)
                {
                    _Final = new IndexMapper<T>();
                }
                return _Final[key];
            }
        }

        public uint MapState(QStateEnum<T> keys)
        {
            var e = keys;
            var map = this;
            while (e.HasNext)
            {
                var k = e.Value;
                if (map._Sub == null) map._Sub = new Dictionary<T, MemoryBackedHyperMapper<T>>();
                if (!map._Sub.TryGetValue(k, out var next))
                    map._Sub[k] = next = new MemoryBackedHyperMapper<T>(map._Final?.Repo);
                map = next;
                e.MoveNext();
            }
            // now e.Value is the final key
            return map[e.Value];
        }

        public uint this[QStateEnum<T> keys]
        {
            get
            {
                return MapState(keys);
                /*
                if (keys.HasNext)
                {
                    T k = keys.Value;
                    HyperMapper<T> s = null;
                    if (_Sub == null)
                    {
                        _Sub = new Dictionary<T, HyperMapper<T>>();
                    }
                    if (!_Sub.TryGetValue(k, out s))
                    {
                        s = new HyperMapper<T>(_Final != null ? _Final.Repo : null);
                        _Sub[k] = s;
                    }
                    keys.MoveNext();
                    return (s[keys]);
                }
                return (this[keys.Value]);
                */
            }
        }
        public void Dump(TextWriter sw = null)
        {
            sw = sw ?? Console.Out;
            sw.WriteLine("==HyperMapper==");
            if (_Final != null)
            {
                //_Final.Dump(sw);
            }
            if(_Sub != null)
                foreach (T key in _Sub.Keys)
                {
                    sw.WriteLine("==HyperMapper ({0})==", key);
                    _Sub[key].Dump(sw);
                }
            sw.WriteLine("=============");
        }

        // ── checkpoints ──

        /// <summary>Restores one mapping recorded by a checkpoint: the state's key path maps to the given index.</summary>
        public void Restore(QState<T> state, uint index)
        {
            QStateEnum<T> e = state.Enumerator;
            MemoryBackedHyperMapper<T> map = this;
            while (e.HasNext)
            {
                T k = e.Value;
                if (map._Sub == null) map._Sub = new Dictionary<T, MemoryBackedHyperMapper<T>>();
                MemoryBackedHyperMapper<T> next;
                if (!map._Sub.TryGetValue(k, out next))
                    map._Sub[k] = next = new MemoryBackedHyperMapper<T>(map._Final?.Repo);
                map = next;
                e.MoveNext();
            }
            if (map._Final == null) map._Final = new IndexMapper<T>();
            IndexMapper<T> final = map._Final as IndexMapper<T>;
            if (final == null)
                throw new NotSupportedException("Only memory backed index mappers can be restored from a checkpoint.");
            final.Restore(e.Value, index);
        }

        /// <summary>Forgets every mapping at every depth.</summary>
        public void Clear()
        {
            (_Final as IndexMapper<T>)?.Clear();
            _Sub = null;
        }

        /// <summary>Writes every mapped state, as its key path, with its index.</summary>
        public void SaveCheckpoint(BinaryWriter writer, IQKeySerializer<T> elementKeys)
        {
            QStateKeySerializer<T> states = new QStateKeySerializer<T>(elementKeys);
            List<KeyValuePair<QState<T>, uint>> mappings = Mappings();
            writer.Write(mappings.Count);
            foreach (KeyValuePair<QState<T>, uint> kv in mappings)
            {
                states.Serialize(writer.BaseStream, kv.Key);
                writer.Write(kv.Value);
            }
        }

        /// <summary>Replaces the mappings with those written by <see cref="SaveCheckpoint"/>.</summary>
        public void LoadCheckpoint(BinaryReader reader, IQKeySerializer<T> elementKeys)
        {
            QStateKeySerializer<T> states = new QStateKeySerializer<T>(elementKeys);
            Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                QState<T> state = states.Deserialize(reader.BaseStream);
                uint index = reader.ReadUInt32();
                Restore(state, index);
            }
        }
    }
}
