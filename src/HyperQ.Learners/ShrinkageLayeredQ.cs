using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace HyperQ.Learners
{
    /// <summary>
    /// A layered learner that lends what its coarse layers know to its thinly visited fine states when it is read to
    /// act. Its layers learn as those of an unscaled <see cref="LayeredHyperQ{T}"/> (see <see cref="BlendedLayeredQ{T}"/>).
    /// To act in state s it shrinks each layer's value toward its parent's blend, weighted by the layer's own evidence,
    /// and acts on the finest blend:
    /// <code>
    /// b_0(a) = Q_0(s_1, a)
    /// b_i(a) = (n_i(a) Q_i(s_(i+1), a) + κ b_(i-1)(a)) / (n_i(a) + κ),   i = 1, ..., L - 1
    /// </code>
    /// where s_k is the prefix of s of length k, Q_i is layer i's value, n_i(a) counts the updates layer i has applied to
    /// (s_(i+1), a), and L is the number of layers that see s. κ is fixed, or chosen per context by a
    /// <see cref="KappaChooser{T}"/>. With κ = 0 the learner acts exactly as the unscaled layered learner; a larger κ asks
    /// a layer for more evidence before its own value outweighs its parent's.
    /// </summary>
    /// <remarks>
    /// The blend has the form of Bühlmann's credibility estimator, Z X + (1 - Z) M with Z = n / (n + κ) (H. Bühlmann,
    /// "Experience rating and credibility", ASTIN Bulletin 4(3), 1967), and of the posterior mean under a Dirichlet prior
    /// of strength κ centred on the parent (D. J. C. MacKay and L. C. B. Peto, "A hierarchical Dirichlet language model",
    /// Natural Language Engineering 1(3), 1995). Both results assume independent observations of a fixed quantity. A
    /// layer's value is a bootstrapped, non-stationary estimate, so no optimality is claimed here. The blend never
    /// changes the tables: every layer learns exactly as in the unscaled learner, from targets built on its own values,
    /// and only the action choice and the values the learner reports read the blend.
    /// </remarks>
    public sealed class ShrinkageLayeredQ<T> : BlendedLayeredQ<T>
    {
        /// <summary>A learner that blends with one κ everywhere.</summary>
        /// <param name="qGenerator">Creates one layer; the same generator a <see cref="LayeredHyperQ{T}"/> takes.</param>
        /// <param name="actionSpace">The action space shared by the layers.</param>
        /// <param name="kappa">The prior strength: a finite number of at least 0. With 0 the learner does not blend.</param>
        public ShrinkageLayeredQ(Func<IHyperQ<T>> qGenerator, QActionSpace<int> actionSpace, double kappa)
            : base(qGenerator, actionSpace)
        {
            if (!(kappa >= 0.0) || double.IsInfinity(kappa))
            {
                throw new ArgumentOutOfRangeException(nameof(kappa), "κ must be a finite number of at least 0.");
            }
            FixedKappa = kappa;
        }

        /// <summary>A learner whose κ a <see cref="KappaChooser{T}"/> picks per parent prefix and evidence bucket.</summary>
        /// <param name="qGenerator">Creates one layer; the same generator a <see cref="LayeredHyperQ{T}"/> takes.</param>
        /// <param name="actionSpace">The action space shared by the layers.</param>
        /// <param name="chooser">Learns κ from the transitions this learner is updated with.</param>
        public ShrinkageLayeredQ(Func<IHyperQ<T>> qGenerator, QActionSpace<int> actionSpace, KappaChooser<T> chooser)
            : base(qGenerator, actionSpace)
        {
            Chooser = chooser ?? throw new ArgumentNullException(nameof(chooser));
        }

        /// <summary>The chooser that learns κ, or null when κ is fixed.</summary>
        public KappaChooser<T> Chooser { get; }

        /// <summary>The fixed κ; 0, and not used, when <see cref="Chooser"/> is set.</summary>
        public double FixedKappa { get; }

        private bool Blends { get { return Chooser != null || FixedKappa > 0.0; } }

        /// <summary>A state's blend at every layer, with each layer's prefix, own values and evidence.</summary>
        private sealed class Blended
        {
            /// <summary>The state's prefixes by layer: Prefix[i] has length i + 1.</summary>
            public QState<T>[] Prefix;
            public double[][] Value;
            public double[][] Own;
            public int[][] Evidence;
            /// <summary>Actions some layer knows for its prefix of the state.</summary>
            public bool[] Candidate;
            public int Layers;
        }

        /// <summary>Blends a state through its layers; null when no layer knows an action for any prefix of it.</summary>
        private Blended Blend(QState<T> s)
        {
            int layers = LayersFor(s);
            if (layers == 0)
            {
                return null;
            }
            Blended b = new Blended
            {
                Prefix = new QState<T>[layers],
                Value = new double[layers][],
                Own = new double[layers][],
                Evidence = new int[layers][],
                Candidate = new bool[ActionCount],
                Layers = layers,
            };
            bool any = false;
            bool[] known = new bool[ActionCount];
            for (int i = 0; i < layers; i++)
            {
                b.Prefix[i] = s.Slice(i + 1);
                b.Value[i] = new double[ActionCount];
                b.Own[i] = new double[ActionCount];
                LayerValues(i, b.Prefix[i], b.Own[i], known);
                b.Evidence[i] = Evidence(b.Prefix[i]);
                KappaChooser<T>.Row row = i > 0 && Chooser != null ? Chooser.RowOf(b.Prefix[i - 1]) : null;
                for (int a = 0; a < ActionCount; a++)
                {
                    if (known[a])
                    {
                        b.Candidate[a] = true;
                        any = true;
                    }
                    double own = b.Own[i][a];
                    if (i == 0)
                    {
                        b.Value[i][a] = own;
                        continue;
                    }
                    int na = b.Evidence[i][a];
                    double kappa = Chooser != null ? Chooser.Kappa(row, i, EvidenceBuckets.Of(na)) : FixedKappa;
                    b.Value[i][a] = na + kappa > 0.0 ? (na * own + kappa * b.Value[i - 1][a]) / (na + kappa) : own;
                }
            }
            return any ? b : null;
        }

        protected override double[] Values(QState<T> s, out bool[] candidate)
        {
            Blended b = Blends ? Blend(s) : null;
            candidate = b?.Candidate;
            return b?.Value[b.Layers - 1];
        }

        protected override void Observe(QState<T> s, int a, QState<T> sprime, int? aprime, double r, HyperParams hp, EvalMethodType evalType)
        {
            if (Chooser != null)
            {
                ScoreChoices(s, a, sprime, aprime, r, hp, evalType);
            }
        }

        /// <summary>
        /// Before an update is applied, scores every candidate κ of every layer below the coarsest by the squared error
        /// with which its blend of (s, a) predicts that layer's TD target, r + γ max_a' b_i(s', a') off policy and
        /// r + γ b_i(s', a') on policy, and hands the errors to the chooser.
        /// </summary>
        private void ScoreChoices(QState<T> s, int a, QState<T> sprime, int? aprime, double r, HyperParams hp, EvalMethodType evalType)
        {
            Blended bs = Blend(s);
            Blended bp = Blend(sprime);
            if (bs == null)
            {
                return;
            }
            uint ai = ActionSpace.ToIndex(a);
            double gamma = hp.Gamma.Value;
            IReadOnlyList<double> candidates = Chooser.Candidates;
            double[] losses = new double[candidates.Count];
            for (int i = 1; i < bs.Layers; i++)
            {
                double next = 0.0;
                if (bp != null && i < bp.Layers)
                {
                    next = aprime.HasValue ? bp.Value[i][ActionSpace.ToIndex(aprime.Value)] : Best(bp.Value[i], bp.Candidate, evalType);
                }
                double target = r + gamma * next;
                int n = bs.Evidence[i][ai];
                double own = bs.Own[i][ai];
                double prior = bs.Value[i - 1][ai];
                for (int k = 0; k < candidates.Count; k++)
                {
                    double kappa = candidates[k];
                    double p = n + kappa > 0.0 ? (n * own + kappa * prior) / (n + kappa) : own;
                    losses[k] = (target - p) * (target - p);
                }
                Chooser.Observe(bs.Prefix[i - 1], EvidenceBuckets.Of(n), losses);
            }
        }

        protected override void SaveBlendState(BinaryWriter writer, QStateKeySerializer<T> states)
        {
            writer.Write(FixedKappa);
            writer.Write(Chooser != null);
            Chooser?.Save(writer, states);
        }

        protected override void LoadBlendState(BinaryReader reader, QStateKeySerializer<T> states)
        {
            double kappa = reader.ReadDouble();
            bool learned = reader.ReadBoolean();
            if (learned != (Chooser != null) || kappa != FixedKappa)
            {
                throw new InvalidDataException("The checkpoint was written by a learner with " + Describe(learned, kappa)
                    + "; this learner has " + Describe(Chooser != null, FixedKappa) + ".");
            }
            Chooser?.Load(reader, states);
        }

        private static string Describe(bool learned, double kappa)
        {
            return learned ? "a learned κ" : "κ = " + kappa.ToString(CultureInfo.InvariantCulture);
        }
    }
}
