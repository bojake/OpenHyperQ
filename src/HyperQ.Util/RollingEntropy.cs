using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{

    public class RollingEntropy
    {
        private Queue<double> entropyHistory;
        private int windowSize;
        private double sumEntropy;

        public RollingEntropy(int windowSize = 100)
        {
            this.windowSize = windowSize;
            entropyHistory = new Queue<double>();
            sumEntropy = 0.0;
        }

        /// <summary>
        /// Updates the rolling entropy tracker with the latest entropy value.
        /// </summary>
        public void Update(double entropy)
        {
            entropyHistory.Enqueue(entropy);
            sumEntropy += entropy;

            if (entropyHistory.Count > windowSize)
            {
                sumEntropy -= entropyHistory.Dequeue(); // Remove oldest value
            }
        }

        /// <summary>
        /// Returns the current smoothed entropy.
        /// </summary>
        public double GetSmoothedEntropy()
        {
            return entropyHistory.Count > 0 ? sumEntropy / entropyHistory.Count : 0.0;
        }

        /// <summary>
        /// Allows adjusting window size dynamically.
        /// </summary>
        public void SetWindowSize(int newSize)
        {
            windowSize = newSize;
            while (entropyHistory.Count > windowSize)
            {
                sumEntropy -= entropyHistory.Dequeue();
            }
        }
    }
}
