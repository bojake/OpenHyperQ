using System;
using System.Runtime.CompilerServices;
using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// Centralised temporal-difference update logic shared by all single-table and
    /// double-table Q implementations.
    ///
    /// Composite learners (LayeredHyperQ, MultiHeadHyperQLearner) do NOT call these
    /// methods directly — they loop over sub-learners that do, so the formula is
    /// centralised transitively.
    /// </summary>
    internal static class QUpdateCore
    {
        // ──────────────────────────────────────────────────────────────
        //  Core TD formula
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Q(s,a) ← Q(s,a) + α·(r + γ·V(s') − Q(s,a))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double TDValue(double curr_v, double prime_v, double r, double alpha, double gamma)
        {
            return curr_v + alpha * (r + gamma * prime_v - curr_v);
        }

        // ──────────────────────────────────────────────────────────────
        //  Update + advantage tracking (the two lines that were
        //  copy-pasted across 8 methods)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the TD update and records the step in the advantage trace. Returns the new Q-value,
        /// which the caller writes back to its table.
        /// </summary>
        /// <param name="curr_v">Current Q(s,a)</param>
        /// <param name="prime_v">Q(s',a') or Q(s', argmax a')</param>
        /// <param name="r">Immediate reward</param>
        /// <param name="hp">Hyperparameters (Alpha, Gamma)</param>
        /// <param name="advantage">The advantage trace to update</param>
        /// <param name="baseline">
        /// A state value baseline for s, normally <see cref="Baseline"/> of the action row of s before the
        /// update. The recorded advantage is the updated Q(s,a) minus this baseline.
        /// </param>
        internal static double UpdateAndTrack(
            double curr_v,
            double prime_v,
            double r,
            HyperParams hp,
            QAdvantage advantage,
            double baseline)
        {
            double new_v = TDValue(curr_v, prime_v, r, hp.Alpha, hp.Gamma);
            // The return trace accumulates the rewards of the episode; the advantage trace records how much
            // better the action just taken looks than the average action of its state, A(s,a) = Q(s,a) - V(s),
            // using the freshly updated Q(s,a) and the mean of the state's action row as V(s). A state-only
            // baseline keeps the policy gradient unbiased while giving the policy selectors a signal that
            // persists after the critic converges. (Two earlier definitions did not: a running sum of the
            // updated Q values minus Q(s,a), which grew along the episode whatever the action, and the TD
            // error r + γ·Q(s',a') − Q(s,a), whose expectation is zero for every action once Q has
            // converged, so the selectors ended up following normalized noise.)
            advantage.AddReturn(r, hp.Gamma);
            advantage.AddAdvantage(new_v - baseline);
            return new_v;
        }

        /// <summary>
        /// The state value baseline used for the advantage trace: the mean of the state's action row.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double Baseline(double[] row)
        {
            if (row == null || row.Length == 0)
                return 0.0;
            double sum = 0.0;
            for (int i = 0; i < row.Length; i++)
                sum += row[i];
            return sum / row.Length;
        }

        // ──────────────────────────────────────────────────────────────
        //  Greedy action selection (off-policy eval target)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Selects the greedy action for off-policy evaluation using the provided
        /// ArgMax/ArgMin functions. Returns the action index from the QAction, or
        /// <paramref name="fallback"/> if the selection returns null.
        /// </summary>
        internal static int SelectGreedyAction<TState>(
            TState sprime,
            int fallback,
            EvalMethodType evalType,
            Func<TState, QAction> argMax,
            Func<TState, QAction> argMin)
        {
            QAction action;
            if (evalType == EvalMethodType.Min)
                action = argMin(sprime);
            else
                action = argMax(sprime);
            return action?.Item1 ?? fallback;
        }
    }
}
