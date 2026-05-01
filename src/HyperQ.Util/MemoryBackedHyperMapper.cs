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
        /// Returns the permutable count of states found in this hyper path
        /// </summary>
        /// <returns></returns>
        public uint MappingCount
        {
            get
            {
                uint count = 0;
                if(_Sub != null)
                    foreach (MemoryBackedHyperMapper<T> h in _Sub.Values)
                    {
                        if (count == 0)
                            count = 1;
                        count *= h.MappingCount;
                    }
                if (_Final != null)
                {
                    if (count == 0)
                        count = 1;
                    count *= _Final.MappingCount;
                }
                return count;
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
    }
}
