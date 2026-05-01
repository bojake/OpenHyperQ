using System;

namespace HyperQ.Learners
{
    /// <summary>
    /// Maps states of type T to compact uint indices.
    /// </summary>
    public interface IStateMapper<T>
    {
        uint AddState(T stateKey);
        uint MapState(T stateKey);
        bool RemoveState(T stateKey);
    }
}
