using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Learners
{
    public interface IActionSelector<T>
    {
        /// <summary>
        /// Chooses an action for a given Q
        /// </summary>
        /// <param name="q">The Q to operate on</param>
        /// <param name="state">The state from which to select an action</param>
        /// <param name="epsilon">The current epsilon/randomness metric</param>
        QAction SelectAction(IArgMinMax<T> q, T state, double epsilon);
        /// <summary>
        /// Returns true if the last action returned was random, and false otherwise
        /// </summary>
        bool LastActionWasRandom { get; }
        /// <summary>
        /// Returns the number of actions that were random
        /// </summary>
        long RandomActionCount { get; }
        void ApplyAdvantage(uint s, QAction a, double advantage, HyperParams hp);
        /// <summary>
        /// Get/set the action selection mode. Default should be the MinMaxActionEnum.Default value
        /// for the default action selection mode, but use min or max for policy evaluation.
        /// </summary>
        MinMaxActionEnum ActionMode { get; set; }

        /// <summary>
        /// Optional callback invoked whenever an action is selected.
        /// </summary>
        Action<T, QAction> TelemetryCallback { get; set; }

        /// <summary>
        /// Called at the start of each episode so that the selector can
        /// perform any book keeping.
        /// </summary>
        void StartEpisode();

        /// <summary>
        /// Called at the end of an episode with the total reward obtained
        /// so that the selector can update any episodic statistics.
        /// </summary>
        /// <param name="reward">The total reward earned in the episode.</param>
        void EndEpisode(RewardAccumulator reward);

    }
}
