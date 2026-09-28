using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    /// <summary>
    /// A seeded random source. There is no process-wide instance: whatever draws random numbers (an action
    /// space, learner, selector, trainer, memory or environment) is handed its source, and learners, selectors
    /// and trainers that are not handed one use their action space's. A run is then reproducible from the
    /// seeds it was given, and runs or tests that do not share a source cannot disturb one another.
    /// </summary>
    [Serializable]
    public class QRandom
    {
        public Random Ran { get; private set; }

        /// <summary>
        /// Creates a source whose sequence is fixed by the seed.
        /// </summary>
        public QRandom(int seed)
        {
            Ran = new Random(seed);
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

        /// <summary>
        /// Restarts the sequence from the given seed.
        /// </summary>
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
