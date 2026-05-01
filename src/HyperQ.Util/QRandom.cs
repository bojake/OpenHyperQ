using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    /// <summary>
    /// A convenient random hub for maintaining a consistent random number sequence. Use this instance
    /// for all of the random calculations so that the simulation and learning is predictable.
    /// </summary>
    [Serializable]
    public class QRandom
    {
        public static QRandom Instance { get; private set; } = new QRandom();
        public Random Ran { get; private set; }

        private QRandom()
        {
            Ran = new Random(0);
        }

        /// <summary>
        /// Useful in the Q learner's default value action. It generates te next double from the random
        /// sequence.
        /// </summary>
        /// <returns></returns>
        public double DefaultRandomAction()
        {
            return (Ran.NextDouble());
        }

        public QRandom Seed(int seed)
        {
            Ran = new Random(seed);
            return (this);
        }

        public int Choose(int min, int max)
        {
            return (Ran.Next(min, max));
        }

        public uint Choose(uint min, uint max)
        {
            int diff = (int)(max - min);
            int nd = Ran.Next(diff);
            return (min + (uint)nd);
        }
    }
}
