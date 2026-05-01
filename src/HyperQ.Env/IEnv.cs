using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperQ.Util;

namespace HyperQ.Env
{
    public interface IEnv<T>
    {
        /// <summary>
        /// Visually renders the environment.
        /// </summary>
        void Render();
        /// <summary>
        /// Resets the environment to the start state.
        /// </summary>
        void Reset();
        /// <summary>
        /// Returns the metrics for this environment.
        /// </summary>
        /// <returns></returns>
        EnvMetrics Metrics { get; }
    }

    public class EnvResult<RT>
    {
        public RT Reward { get; set; }
        public bool Done { get; set; }

        public EnvResult()
        {
            Reward = default;
            Done = false;
        }
        public EnvResult(RT reward, bool done)
        {
            this.Reward = reward;
            this.Done = done;
        }
    }
}
