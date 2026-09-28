using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    /// <summary>
    /// Deep mapping class that keeps track of hierarchical indexing.
    /// </summary>
    /// <typeparam name="T">The underlying state type</typeparam>
    [Serializable]
    public sealed class FileBackedHyperMapper<T>
    {
        private IIndexMapper<T> _Final;
        private Dictionary<T, FileBackedHyperMapper<T>> _Sub;
        // Base file name unique to this mapper instance. Sub mappers derive
        // their base from the parent ensuring each branch writes to its own
        // index files.
        private string _BaseFileName;
        private int _Level = 1;
        private readonly IndexRepo _Repo;
        private bool IsEmpty => (_Final == null || _Final.MappingCount == 0) && (_Sub == null || _Sub.Count == 0);

        /// <summary>
        /// Creates a mapper whose every level draws its indexes from <paramref name="repo"/>, or from a
        /// repository of its own when none is given; the levels share one repository because the indexes of all
        /// the states mapped in the tree address the rows of one table. Given a repository, the mapper opens
        /// this level's files at once; otherwise when the first key ends at this level.
        /// </summary>
        public FileBackedHyperMapper(IndexRepo repo = null, string memoryFileName="hyperqindexmapper", int level = 1)
            : this(repo ?? new IndexRepo(), memoryFileName, level, repo != null)
        {
        }

        private FileBackedHyperMapper(IndexRepo repo, string memoryFileName, int level, bool openLevel)
        {
            _Repo = repo;
            _BaseFileName = memoryFileName;
            _Level = level;
            if (openLevel)
            {
                _Final = new FileIndexMapper<T>($"{_BaseFileName}L{_Level}", repo: _Repo);
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

        public FileBackedHyperMapper<T> Clone()
        {
            // Preserve the current level so that any cloned mapper continues to
            // generate file names that match the original hierarchy.
            FileBackedHyperMapper<T> h = new FileBackedHyperMapper<T>(_Repo, _BaseFileName, _Level, false);
            if (_Final != null)
            {
                h._Final = _Final.Clone();
            }
            if (_Sub != null)
            {
                h._Sub = new Dictionary<T, FileBackedHyperMapper<T>>();
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
                // The number of keys mapped anywhere in this hyper path (see MemoryBackedHyperMapper).
                uint count = 0;
                if (_Sub != null)
                {
                    foreach (FileBackedHyperMapper<T> h in _Sub.Values)
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
                    // Each mapper writes to its own index files.  The base file
                    // name is unique per mapper instance, so combine it with the
                    // current level to create the final mapping file.
                    _Final = new FileIndexMapper<T>($"{_BaseFileName}L{_Level}", repo: _Repo);
                }
                return _Final[key];
            }
        }

        private string __mappedFileBaseName
        {
            get
            {
                return $"{_BaseFileName}L{_Level}";
            }
        }

        private static string BuildSubMapperBase(string parentBaseFileName, T key)
        {
            string keyText = key == null ? "<null>" : key.ToString();
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{parentBaseFileName}|{keyText}"));
                // Keep the path short: fixed 16 hex chars per level.
                string hashText = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
                string dir = Path.GetDirectoryName(parentBaseFileName);
                if (string.IsNullOrWhiteSpace(dir))
                {
                    dir = ".";
                }
                return Path.Combine(dir, $"hq_{hashText}");
            }
        }

        public uint MapState(QStateEnum<T> keys)
        {
            var e = keys;
            var map = this;
            while (e.HasNext)
            {
                var k = e.Value;
                if (map._Sub == null) map._Sub = new Dictionary<T, FileBackedHyperMapper<T>>();
                if (!map._Sub.TryGetValue(k, out var next))
                {
                    // Derive a unique base file name for the next level using
                    // the parent's base and a stable key hash.  This prevents
                    // sibling branches from sharing the same backing files and
                    // ensures indices remain unique per state path.
                    string subBase = BuildSubMapperBase(map._BaseFileName, k);
                    map._Sub[k] = next = new FileBackedHyperMapper<T>(map._Repo, subBase, map._Level + 1, map._Final != null);
                }
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
