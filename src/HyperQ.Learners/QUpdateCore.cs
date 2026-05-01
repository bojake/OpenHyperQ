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
        /// Computes the TD update, writes the new value, and records the advantage.
        /// Returns the new Q-value.
        /// </summary>
        /// <param name="curr_v">Current Q(s,a)</param>
        /// <param name="prime_v">Q(s',a') or Q(s', argmax a')</param>
        /// <param name="r">Immediate reward</param>
        /// <param name="hp">Hyperparameters (Alpha, Gamma)</param>
        /// <param name="advantage">The advantage trace to update</param>
        internal static double UpdateAndTrack(
            double curr_v,
            double prime_v,
            double r,
            HyperParams hp,
            QAdvantage advantage)
        {
            double new_v = TDValue(curr_v, prime_v, r, hp.Alpha, hp.Gamma);
            advantage.AddAdvantage(advantage.AddReturn(new_v, hp.Gamma) - curr_v);
            return new_v;
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
