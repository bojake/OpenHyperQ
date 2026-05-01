using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// Adapts a <see cref="MemoryBackedHyperMapper{T}"/> to the
    /// <see cref="IStateMapper{T}"/> interface used by the action selectors.
    /// Used internally by the 1-arg convenience constructors.
    /// </summary>
    internal sealed class MemoryMapperAdapter<T> : HyperQ.Learners.IStateMapper<T>
    {
        private readonly MemoryBackedHyperMapper<T> _inner = new MemoryBackedHyperMapper<T>();

        public uint AddState(T stateKey) => _inner[stateKey];
        public uint MapState(T stateKey) => _inner[stateKey];
        public bool RemoveState(T stateKey) => _inner.RemoveKey(stateKey);
    }
}
