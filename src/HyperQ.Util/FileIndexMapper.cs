using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    /// <summary>
    /// File based implementation of an index mapper that stores the mapping in a
    /// memory mapped file. A small in-memory cache can be configured to speed up
    /// lookups. New keys may be broadcast to other compute nodes using TCP/IP.
    /// </summary>
    /// <typeparam name="T">Serializable key type</typeparam>
    [Serializable]
    public class FileIndexMapper<T> : IIndexMapper<T>, IDisposable
    {
        private readonly string _basePath;
        private MemoryMappedFile _indexMap;
        private MemoryMappedViewAccessor _indexAccessor;
        private readonly FileStream _dataStream;
        private readonly ConcurrentDictionary<T, uint> _cache;
        private readonly LinkedList<T> _lru;
        private readonly object _lruLock = new object();
        private readonly int _cacheSize;
        private readonly IQKeySerializer<T> _keySerializer;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly List<IPEndPoint> _remotes = new List<IPEndPoint>();
        private readonly List<KeyValuePair<T, uint>> _pending = new List<KeyValuePair<T, uint>>();
        private readonly Dictionary<uint, T> _pendingIndex = new Dictionary<uint, T>();
        private readonly int _flushThreshold;
        private bool _disposed;
        private long _indexCapacity;
        private long _knownSlots;
        private const int AccessRetryCount = 3;
        private const int AccessRetryDelayMs = 10;
        /// <summary>
        /// Header written at the start of every .dat file. Reserving this
        /// space guarantees real key offsets are never zero, which lets slot
        /// discovery on disk distinguish "never written" from a live entry.
        /// </summary>
        private static readonly byte[] DataFileHeader = new byte[] { (byte)'H', (byte)'Q', (byte)'D', (byte)'1', 0, 0, 0, 0 };
        /// <summary>
        /// Hands out the indexes and keeps the ones freed when keys are removed. The mapper's own unless one
        /// was given to share.
        /// </summary>
        private IndexRepo _indices;

        /// <summary>
        /// Creates a new file backed index mapper.
        /// </summary>
        /// <param name="baseFile">Path without extension used to store files.</param>
        /// <param name="cacheSize">Number of entries to keep in memory.</param>
        /// <param name="flushThreshold">Number of new keys to accumulate before flushing to disk.</param>
        /// <param name="keySerializer">
        /// Serializer for key type <typeparamref name="T"/>. When null, the framework
        /// attempts to resolve a built-in serializer via <see cref="QKeySerializerFactory"/>.
        /// Pass an explicit instance for custom key types.
        /// </param>
        /// <param name="repo">
        /// The index repository to draw from, shared with the mappers whose indexes must not collide with
        /// this one's (the levels of a <see cref="FileBackedHyperMapper{T}"/>). When null the mapper has a
        /// repository of its own.
        /// </param>
        public FileIndexMapper(string baseFile, int cacheSize = 1000, int flushThreshold = 10,
            IQKeySerializer<T> keySerializer = null, IndexRepo repo = null)
        {
            _indices = repo ?? new IndexRepo();
            _keySerializer = keySerializer ?? QKeySerializerFactory.GetDefault<T>();
            _basePath = baseFile;
            string dir = Path.GetDirectoryName(baseFile);
            if (string.IsNullOrEmpty(dir)) {
                dir = ".";
            }
            else 
                Directory.CreateDirectory(dir);
            string indexPath = baseFile + ".idx";
            string dataPath = baseFile + ".dat";
            try
            {
                _dataStream = DataFileStreamFactory.Get(dataPath);
                if (_dataStream.Length == 0)
                {
                    // Reserve the start of the data file with a header so the
                    // first key never lands at offset zero. A slot value of
                    // zero can then unambiguously mean "never written" when
                    // the known slot count is recovered from disk.
                    _dataStream.Write(DataFileHeader, 0, DataFileHeader.Length);
                    _dataStream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to create or open the memory mapped IDX file at {0} {1}", indexPath, ex.Message);
                throw ex;
            }
            long len = new FileInfo(indexPath).Exists ? new FileInfo(indexPath).Length : 0;
            long minCapacity = 8L * 1024L * 1024L; // enough room for ~1M index slots
            long capacity = Math.Max(len == 0 ? 8 * 1024 : len, minCapacity);
            try
            {
                _indexMap = MemoryMappedFileFactory.Get(indexPath, capacity);
                _indexAccessor = _indexMap.CreateViewAccessor(0, capacity, MemoryMappedFileAccess.ReadWrite);
                _indexCapacity = capacity;
                _knownSlots = DiscoverKnownSlots(len);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to create or open the memory mapped DAT file at {0} {1}", dataPath, ex.Message);
                throw ex;
            }
            // Only move the index repository forward if the backing file already
            // contains data. Creating a new mapper with an empty file must not
            // rewind a repository it shares with sibling mappers, as that would
            // reuse their indexes.
            uint next = (uint)Math.Min(_knownSlots, uint.MaxValue);
            if (next > 0 && next > _indices.Peek)
            {
                _indices.RestoreAt(next);
            }
            _cacheSize = cacheSize;
            _cache = new ConcurrentDictionary<T, uint>();
            _lru = new LinkedList<T>();
            _flushThreshold = flushThreshold;
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
        private void AddToCache(T key, uint idx)
        {
            if (_cacheSize <= 0) return;
            // The cache and the LRU list are touched from multiple threads
            // (the GetOrAddIndex fast path runs without the reader/writer
            // lock), so all mutations of the pair happen under _lruLock.
            lock (_lruLock)
            {
                if (_cache.TryGetValue(key, out uint existing))
                {
                    _lru.Remove(key);
                }
                else if (_cache.Count >= _cacheSize)
                {
                    T oldest = _lru.First.Value;
                    _lru.RemoveFirst();
                    _cache.TryRemove(oldest, out _);
                }
                _cache[key] = idx;
                _lru.AddLast(key);
            }
        }

        private long ReadIndexOffset(uint index)
        {
            if (index >= _knownSlots)
            {
                return -1L;
            }
            int retry = AccessRetryCount;
            while (retry-- > 0)
            {
                try
                {
                    return _indexAccessor.ReadInt64(index * sizeof(long));
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(AccessRetryDelayMs);
                }
            }
            return _indexAccessor.ReadInt64(index * sizeof(long));
        }

        private void WriteIndexOffset(uint index, long offset)
        {
            long position = (long)index * sizeof(long);
            if (position >= _indexCapacity)
            {
                throw new IndexOutOfRangeException($"Index {index} exceeds index capacity {_indexCapacity / sizeof(long)}.");
            }
            if (index >= _knownSlots)
            {
                for (long i = _knownSlots; i < index; i++)
                {
                    _indexAccessor.Write(i * sizeof(long), -1L);
                }
                _knownSlots = index + 1;
            }
            int retry = AccessRetryCount;
            while (retry-- > 0)
            {
                try
                {
                    _indexAccessor.Write(position, offset);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(AccessRetryDelayMs);
                }
            }
            _indexAccessor.Write(position, offset);
        }

        private uint GetScanUpperBound()
        {
            uint upper = _indices.Peek;
            uint knownUpper = (uint)Math.Min(_knownSlots, uint.MaxValue);
            if (knownUpper > upper)
            {
                upper = knownUpper;
            }
            foreach (uint idx in _pendingIndex.Keys)
            {
                uint candidate = idx + 1;
                if (candidate > upper)
                {
                    upper = candidate;
                }
            }
            return upper;
        }

        private long DiscoverKnownSlots(long fileLength)
        {
            long slotCount = fileLength / sizeof(long);
            if (slotCount <= 0)
            {
                return 0;
            }
            for (long i = slotCount - 1; i >= 0; i--)
            {
                long offset = _indexAccessor.ReadInt64(i * sizeof(long));
                // Zero slots were never written and -1 slots are tombstones
                // of removed keys. Every live entry holds a non-zero offset
                // because the data file reserves a header.
                if (offset != 0L && offset != -1L)
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private uint FindKeyInFile(T key)
        {
            bool releaseLock = !_lock.IsWriteLockHeld && !_lock.IsReadLockHeld;
            if (releaseLock)
                _lock.EnterReadLock();
            try
            {
                uint upper = GetScanUpperBound();
                for (uint i = 0; i < upper; i++)
                {
                    long offset = ReadIndexOffset(i);
                    if (offset < 0) continue;
                    if (offset < _dataStream.Length - 4)
                    {
                        _dataStream.Position = offset;
                        try
                        {
                            int len = new BinaryReader(_dataStream).ReadInt32();
                            byte[] data = new byte[len];
                            _dataStream.Read(data, 0, len);
                            using (var ms = new MemoryStream(data))
                            {
                                T k = _keySerializer.Deserialize(ms);
                                if (EqualityComparer<T>.Default.Equals(k, key))
                                {
                                    return i;
                                }
                            }
                        }
                        catch (System.IO.EndOfStreamException)
                        {
                            // Key is not in the file
                        }
                    }
                }
            }
            finally
            {
                if (releaseLock)
                    _lock.ExitReadLock();
            }
            return uint.MaxValue;
        }

        /// <summary>
        /// Returns the index assigned to the key while it is still queued in
        /// _pending, or uint.MaxValue. The caller must hold _lock because the
        /// pending queue mutates under the write lock.
        /// </summary>
        private uint FindPendingIndex(T key)
        {
            foreach (KeyValuePair<T, uint> kv in _pending)
            {
                if (EqualityComparer<T>.Default.Equals(kv.Key, key))
                {
                    return kv.Value;
                }
            }
            return uint.MaxValue;
        }

        private void WriteKeyToDisk(T key, uint index)
        {
            long offset = _dataStream.Seek(0, SeekOrigin.End);
            using (var ms = new MemoryStream())
            {
                _keySerializer.Serialize(ms, key);
                byte[] data = ms.ToArray();
                BinaryWriter bw = new BinaryWriter(_dataStream);
                bw.Write(data.Length);
                bw.Write(data);
            }
            WriteIndexOffset(index, offset);
        }

        private async Task FlushPendingAsync()
        {
            KeyValuePair<T, uint>[] toFlush;
            _lock.EnterWriteLock();
            try
            {
                if (_pending.Count == 0) return;
                toFlush = _pending.ToArray();
                _pending.Clear();
                foreach (var kv in toFlush)
                {
                    _pendingIndex.Remove(kv.Value);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            await Task.Run(() =>
            {
                _lock.EnterWriteLock();
                try
                {
                    foreach (var kvp in toFlush)
                    {
                        WriteKeyToDisk(kvp.Key, kvp.Value);
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            });
        }

        private uint GetOrAddIndex(T key)
        {
            if (_cache.TryGetValue(key, out uint idx))
            {
                lock (_lruLock)
                {
                    _lru.Remove(key);
                    _lru.AddLast(key);
                }
                return idx;
            }
            idx = FindKeyInFile(key);
            if (idx != uint.MaxValue)
            {
                AddToCache(key, idx);
                return idx;
            }
            bool shouldFlush = false;
            bool created = false;
            _lock.EnterWriteLock();
            try
            {
                // Re-check before allocating: a concurrent caller may have
                // inserted this key after our lookup, and pending keys are
                // not yet visible to FindKeyInFile because they are not on
                // disk. Without this check the same key can be allocated
                // several slots.
                if (_cache.TryGetValue(key, out uint raced))
                {
                    idx = raced;
                }
                else
                {
                    idx = FindPendingIndex(key);
                    if (idx == uint.MaxValue)
                    {
                        idx = FindKeyInFile(key);
                    }
                }
                if (idx == uint.MaxValue)
                {
                    idx = _indices.Next;
                    AddToCache(key, idx);
                    _cache[key] = idx;
                    _pending.Add(new KeyValuePair<T, uint>(key, idx));
                    _pendingIndex[idx] = key;
                    BroadcastNewKey(key);
                    shouldFlush = _pending.Count >= _flushThreshold;
                    created = true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            if (shouldFlush)
            {
                FlushPendingAsync().Wait();
            }
            if (created)
            {
                return idx;
            }
            AddToCache(key, idx);
            return idx;
        }

        private T KeyFromIndex(uint index)
        {
            _lock.EnterReadLock();
            try
            {
                if (index >= _indices.Peek) throw new IndexOutOfRangeException();
                if (_pendingIndex.TryGetValue(index, out T pending))
                {
                    return pending;
                }
                long offset = ReadIndexOffset(index);
                if (offset < 0)
                {
                    throw new IndexOutOfRangeException();
                }
                _dataStream.Position = offset;
                int len = new BinaryReader(_dataStream).ReadInt32();
                byte[] data = new byte[len];
                _dataStream.Read(data, 0, len);
                using (var ms = new MemoryStream(data))
                {
                    return _keySerializer.Deserialize(ms);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        /// <summary>
        /// Get the index for the given state key. 
        /// </summary>
        /// <param name="key">The key in source key space (not index space)</param>
        /// <returns>The index space mapping of the key</returns>
        public virtual uint this[T key] { 
            get
            {
                return ToIndex(key);
            }
        }

        /// <summary>
        /// Returns a randomly selected key from the index map.
        /// </summary>
        /// <param name="ran">The random number generator, can not be null</param>
        /// <returns></returns>
        public virtual uint RandomKey(QRandom ran)
        {
            if (ran == null)
                throw new ArgumentNullException(nameof(ran));
            _lock.EnterReadLock();
            try
            {
                if (_indices.Peek == 0)
                {
                    throw new InvalidOperationException("No keys in mapper");
                }
                uint upper = GetScanUpperBound();
                if (upper == 0)
                {
                    throw new InvalidOperationException("No keys in mapper");
                }
                int attempts = 0;
                int maxAttempts = Math.Max(1, (int)upper * 2);
                while (attempts++ < maxAttempts)
                {
                    uint idx = ran.Choose(0u, upper);
                    if (_pendingIndex.ContainsKey(idx))
                    {
                        return idx;
                    }
                    long offset = ReadIndexOffset(idx);
                    if (offset >= 0)
                    {
                        return idx;
                    }
                }
                for (uint idx = 0; idx < upper; idx++)
                {
                    if (_pendingIndex.ContainsKey(idx))
                    {
                        return idx;
                    }
                    if (ReadIndexOffset(idx) >= 0)
                    {
                        return idx;
                    }
                }
                throw new InvalidOperationException("No keys in mapper");
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Remove the given key in source key space from the index and returns true
        /// if the key was found, false if not.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual bool RemoveKey(T key)
        {
            uint idx = uint.MaxValue;
            bool pending = false;
            lock (_lruLock)
            {
                if (_cache.TryGetValue(key, out uint cachedIdx))
                {
                    idx = cachedIdx;
                    _cache.TryRemove(key, out _);
                    _lru.Remove(key);
                }
            }
            if (idx == uint.MaxValue)
            {
                _lock.EnterWriteLock();
                try
                {
                    for (int i = 0; i < _pending.Count; i++)
                    {
                        if (EqualityComparer<T>.Default.Equals(_pending[i].Key, key))
                        {
                            idx = _pending[i].Value;
                            _pending.RemoveAt(i);
                            _pendingIndex.Remove(idx);
                            pending = true;
                            break;
                        }
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
                idx = FindKeyInFile(key);
                if (idx == uint.MaxValue)
                {
                    return false;
                }
            }

            _lock.EnterWriteLock();
            try
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    if (_pending[i].Value == idx)
                    {
                        _pending.RemoveAt(i);
                        _pendingIndex.Remove(idx);
                        pending = true;
                        break;
                    }
                }
                if (!pending)
                {
                    WriteIndexOffset(idx, -1L);
                }
                _indices.Release(idx);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            return true;
        }
        /// <summary>
        /// Clone this index
        /// </summary>
        /// <returns></returns>
        public virtual IIndexMapper<T> Clone()
        {
            FlushPendingAsync().Wait();
            string newBase = _basePath + ".clone." + Guid.NewGuid().ToString();
            _lock.EnterWriteLock();
            try
            {
                _indexAccessor.Flush();
                _dataStream.Flush();
                File.Copy(_basePath + ".idx", newBase + ".idx", true);
                File.Copy(_basePath + ".dat", newBase + ".dat", true);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            FileIndexMapper<T> clone = new FileIndexMapper<T>(newBase, _cacheSize, _flushThreshold, repo: _indices);
            foreach (var ep in _remotes)
            {
                clone.AddRemote(ep);
            }
            return clone;
        }
        private void BroadcastNewKey(T key)
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            {
                _keySerializer.Serialize(ms, key);
                payload = ms.ToArray();
            }
            foreach (var ep in _remotes)
            {
                Task.Run(() =>
                {
                    try
                    {
                        using (TcpClient c = new TcpClient())
                        {
                            c.Connect(ep);
                            using (var s = c.GetStream())
                            {
                                BinaryWriter bw = new BinaryWriter(s);
                                bw.Write(payload.Length);
                                bw.Write(payload);
                            }
                        }
                    }
                    catch { /* ignore */ }
                });
            }
        }

        public void AddRemote(IPEndPoint ep)
        {
            _remotes.Add(ep);
        }

        public void StartServer(int port)
        {
            Task.Run(() =>
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Task.Run(() =>
                    {
                        using (var s = client.GetStream())
                        {
                            BinaryReader br = new BinaryReader(s);
                            int len = br.ReadInt32();
                            byte[] data = br.ReadBytes(len);
                            using (var ms = new MemoryStream(data))
                            {
                                T k = _keySerializer.Deserialize(ms);
                                bool shouldFlush = false;
                                _lock.EnterWriteLock();
                                try
                                {
                                    // Same re-check as GetOrAddIndex: pending
                                    // keys are invisible to FindKeyInFile.
                                    if (!_cache.TryGetValue(k, out uint known)
                                        && FindPendingIndex(k) == uint.MaxValue
                                        && FindKeyInFile(k) == uint.MaxValue)
                                    {
                                        uint newIdx = _indices.Next;
                                        AddToCache(k, newIdx);
                                        _pending.Add(new KeyValuePair<T, uint>(k, newIdx));
                                        _pendingIndex[newIdx] = k;
                                        shouldFlush = _pending.Count >= _flushThreshold;
                                    }
                                }
                                finally
                                {
                                    _lock.ExitWriteLock();
                                }
                                if (shouldFlush)
                                {
                                    FlushPendingAsync().Wait();
                                }
                            }
                        }
                    });
                }
            });
        }

        public uint ToIndex(T key)
        {
            return GetOrAddIndex(key);
        }

        public T FromIndex(uint index)
        {
            return KeyFromIndex(index);
        }

        public bool Known(T key)
        {
            if (_cache.ContainsKey(key))
                return true;
            _lock.EnterReadLock();
            try
            {
                foreach (var kv in _pending)
                {
                    if (EqualityComparer<T>.Default.Equals(kv.Key, key))
                        return true;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
            return FindKeyInFile(key) != uint.MaxValue;
        }

        public bool IsKnownIndex(uint index)
        {
            _lock.EnterReadLock();
            try
            {
                if (_pendingIndex.ContainsKey(index))
                    return true;
                if (index >= GetScanUpperBound())
                    return false;
                return ReadIndexOffset(index) >= 0;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public Task FlushAsync()
        {
            return FlushPendingAsync();
        }

        public uint MappingCount
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    uint count = (uint)_pendingIndex.Count;
                    uint upper = GetScanUpperBound();
                    for (uint i = 0; i < upper; i++)
                    {
                        if (_pendingIndex.ContainsKey(i))
                            continue;
                        if (ReadIndexOffset(i) >= 0)
                            count++;
                    }
                    return count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                FlushPendingAsync().Wait();
                _indexAccessor?.Dispose();
                _indexMap?.Dispose();
                _dataStream?.Dispose();
                _lock?.Dispose();
            }
            _disposed = true;
        }
    }
}
