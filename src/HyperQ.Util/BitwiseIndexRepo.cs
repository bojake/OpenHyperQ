using System;
using System.Collections.Generic;
using System.Collections;

namespace HyperQ.Util
{

    internal class BitwiseIndexRepo
    {
        /// <summary>
        /// The map of bits that represent the free and used indices
        /// </summary>
        private BitArray _FreeMap;
        /// <summary>
        /// Number of bits, or indices, managed by the map
        /// </summary>
        private int _MapSize;

        internal BitwiseIndexRepo(int size)
        {
            this._MapSize = size;
            this._FreeMap = new BitArray(size);
        }

        // Marks an entry as in use
        internal void UseEntry(int index)
        {
            if (index >= 0 && index < _MapSize)
                _FreeMap.Set(index, true);
            else
                Console.WriteLine("Index out of range");
        }

        // Marks an entry as free
        internal void FreeEntry(int index)
        {
            if (index >= 0 && index < _MapSize)
                _FreeMap.Set(index, false);
            else
                Console.WriteLine("Index out of range");
        }

        // Checks if an entry is in use
        internal bool IsEntryInUse(int index)
        {
            return index >= 0 && index < _MapSize && _FreeMap.Get(index);
        }

        // Prints the current status of the index repository's free map
        public void Dump()
        {
            Console.Write("Current Free Map: ");
            for (int i = 0; i < _MapSize; i++)
            {
                Console.Write(_FreeMap.Get(i) ? "1" : "0");
                if ((i + 1) % 64 == 0)
                    Console.WriteLine();
            }
            Console.WriteLine();
        }
    }
}
