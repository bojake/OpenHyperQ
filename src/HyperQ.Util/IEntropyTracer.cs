using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    /// <summary>
    /// Implementors of this interface will provide entropy calculations based upon their
    /// internal probability tracking.
    /// </summary>
    public interface IEntropyTracer
    {
        /// <summary>
        /// The entropy of all probabilities collected during the last episode
        /// </summary>
        double EntropyForLastEpisode { get; }
        /// <summary>
        /// The entropy of all probabilities collecting in the current episode
        /// </summary>
        double CurrentEntropy { get; }
        /// <summary>
        /// The current probability snapshot
        /// </summary>
        double[] Probabilities { get; }
    }
}
