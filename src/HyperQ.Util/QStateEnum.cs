using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    /// <summary>
    /// Enumerates through a QState instance without causing any state changes
    /// to the QState instance. This will return the next element in the QState
    /// without checking for errors. Use the HasNext() method to control iterations
    /// through the QState members.
    /// </summary>
    [Serializable]
    public class QStateEnum<T>
    {
        private QState<T> _State;
        private int _Index = 0;
        internal QStateEnum(QState<T> state)
        {
            _State = state;
        }
        /// <summary>
        /// Resets the read index to 0
        /// </summary>
        public void Reset()
        {
            _Index = 0;
        }

        /// <summary>
        /// Returns true if there is a value at the current index. Uses
        /// the QState.Peek method.
        /// </summary>
        /// <returns></returns>
        public bool HasValue
        {
            get
            {
                return (_State.HasValue(_Index));
            }
        }

        /// <summary>
        /// Returns true if there is a value at the next index.
        /// </summary>
        public bool HasNext
        {
            get
            {
                return (_State.HasValue(_Index+1));
            }
        }

        /// <summary>
        /// Returns the value at the current index.
        /// </summary>
        public T Value
        {
            get
            {
                return (_State[_Index]);
            }
        }

        /// <summary>
        /// Returns the value at the current index. Uses the QState [] array
        /// accessor.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            _Index++;
            return (HasValue);
        }
    }
}
