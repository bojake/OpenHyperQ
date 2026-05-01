using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    [Serializable]
    public class HyperParams : ICheckpointable
    {
        /// <summary>
        /// Reward discount rate, default is 1
        /// </summary>
        public QParam Gamma { get; private set; } = null; // new QParam(1.0, 1.0, 1.0);
        /// <summary>
        /// The randomness factor used in e-Greedy operations, default is 1
        /// </summary>
        public QParam Epsilon { get; private set; } = null; // new QParam(1.0, 1.0, 0.0);
        /// <summary>
        /// The learning rate, default is 1
        /// </summary>
        public QParam Alpha { get; private set; } = null; // ew QParam(1.0, 1.0, 1.0);
        /// <summary>
        /// The inter-layer decay rate of the reward
        /// </summary>
        public QParam Tau { get; private set; } = null; // new QParam(1.0, 1.0, 1.0);
        /// <summary>
        /// Controls how the current reward propagates through the history in a gradual decay
        /// of the applied reward.
        /// </summary>
        public QParam Hysteresis { get; private set; } = null; //  new QParam(0.0, 1.0, 0.0);
        /// <summary>
        /// The clipping window width for the advantage calculations used in policy iteration. Values should
        /// be between 0.1 and 0.3
        /// </summary>
        public QParam Omega { get; private set; } = null; //  new QParam(0.3, 1.0, 0.1);
        /// <summary>
        /// A factor used when doing the actor-critic penalty adjustment in the PolicyGradientActionSelector.
        /// </summary>
        public QParam PenaltyAnnealingFactor { get; private set; } = null; // new QParam(0.5, 0.99, 0.1);
        /// <summary>
        /// The GAE bias-variance tradeoff parameter. 0 = one-step TD (high bias, low variance),
        /// 1 = Monte Carlo (low bias, high variance). Typical values: 0.9–0.99.
        /// </summary>
        public QParam Lambda { get; private set; } = null;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="g">Gamma, the blending rate</param>
        /// <param name="e">Epsilon, the randomness</param>
        /// <param name="a">Alpha, the learning rate</param>
        /// <param name="edecay">Epsilon Decay, the decay rate of epsilon, tendency towards order</param>
        /// <param name="adecay">Alpha Decay, the decay rate of alpha</param>
        public HyperParams(double g = 1.0, double e = 1.0, double a = 1.0, double edecay = 1.0, double adecay = 1.0, double min_alpha = 0.0, double min_epsilon = 0.0, double tau = 1.0, double omega = 0.3, double penalty_annealing=0.5, double lambda = 1.0)
        {
            Gamma = new QParamExponential(g, 1.0, g);
            Epsilon = new QParamExponential(e, edecay, min_epsilon);
            Alpha = new QParamExponential(a, adecay, min_alpha);
            Tau = new QParamExponential(tau, 1.0, tau);
            Omega = new QParamStatic(omega);
            Hysteresis = new QParamStatic(0.0);
            PenaltyAnnealingFactor = new QParamExponential(penalty_annealing, 0.99, 0.1);
            Lambda = new QParamStatic(lambda);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="g">Gamma (reward applicability)</param>
        /// <param name="e">Epsilon (randomness)</param>
        /// <param name="a">Alpha (learning rate)</param>
        /// <param name="t">Tau (inter-layer reward applicability)</param>
        /// <param name="h">The hysteresis factor for applying episodic updates in training</param>
        /// <param name="o">The omega for the advantage update, constrains the value of the clipping of the advantage updates</param>
        /// <param name="paf">The penalty annealing factor used in the actor critic update int he PolicyGradientActionSelector</param>
        public HyperParams(QParam g, QParam e, QParam a, QParam t, QParam h = null, QParam o = null, QParam paf=null, QParam lambda=null)
        {
            Gamma = g;
            Epsilon = e;
            Alpha = a;
            Tau = t;
            Omega = o ?? new QParamStatic(0.3);
            Hysteresis = h ?? new QParamStatic(0.0);
            PenaltyAnnealingFactor = paf ?? new QParamExponential(0.5, 0.99, 0.1);
            Lambda = lambda ?? new QParamStatic(1.0);
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="h"></param>
        public HyperParams(HyperParams h)
        {
            Gamma =h.Gamma;
            Epsilon = h.Epsilon;
            Alpha = h.Alpha;
            Tau  = h.Tau;
            Hysteresis = h.Hysteresis;
            Omega = h.Omega;
            PenaltyAnnealingFactor = h.PenaltyAnnealingFactor;
            Lambda = h.Lambda;
        }

        /// <summary>
        /// Resets all hyperparameters (alpha, epsilon, gamma, tau,
        /// hysteresis, omega, and penalty annealing factor) back to their
        /// original values.
        /// </summary>
        public virtual void Reset()
        {
            Alpha.Reset();
            Epsilon.Reset();
            Tau.Reset();
            Gamma.Reset();
            Hysteresis.Reset();
            Omega.Reset();
            PenaltyAnnealingFactor.Reset();
            Lambda.Reset();
        }

        /// <summary>
        /// Decays all of the parameters.
        /// </summary>
        public virtual void Decay()
        {
            Alpha.Decay();
            Epsilon.Decay();
            Gamma.Decay();
            Tau.Decay();
            Hysteresis.Decay();
            Omega.Decay();
            PenaltyAnnealingFactor.Decay();
            Lambda.Decay();
        }

        /// <summary>
        /// Resets the epsilon back to the original value.
        /// </summary>
        public virtual void ResetEpsilon()
        {
            Epsilon.Reset();
        }

        /// <summary>
        /// Resets the alpha back to the original value.
        /// </summary>
        public virtual void ResetAlpha()
        {
            Alpha.Reset();
        }

        /// <summary>
        /// Applies the decay factor the current epsilon and clips it at the MinimumEpsilon value.
        /// </summary>
        public virtual void DecayEpsilon()
        {
            Epsilon.Decay();
        }
        /// <summary>
        /// Applies the decay factor the current alpha and clips it at the MinimumAlpha value.
        /// </summary>
        public virtual void DecayAlpha()
        {
            Alpha.Decay();
        }
        /// <summary>
        /// Decay the hysteresis
        /// </summary>
        public virtual void DecayHysteresis()
        {
            Hysteresis.Decay();
        }

        /// <summary>
        /// Applies the decay factor to the omega parameter
        /// </summary>
        public virtual void DecayOmega()
        {
            Omega.Decay();
        }

        /// <summary>
        /// Resets the value of the omega parameter
        /// </summary>
        public virtual void ResetOmega()
        {
            Omega.Reset();
        }
        /// <summary>
        /// Applies the decay factor to the penalty annealing factor parameter
        /// </summary>
        public virtual void DecayPenaltyAnnealingFactor()
        {
            PenaltyAnnealingFactor.Decay();
        }

        /// <summary>
        /// Resets the value of the penalty annealing factor parameter
        /// </summary>
        public virtual void ResetPenaltyAnnealingFactor()
        {
            PenaltyAnnealingFactor.Reset();
        }

        // ── ICheckpointable ──

        public int CheckpointVersion => 1;

        /// <summary>
        /// Saves all hyperparameter state (including current decay positions) to a binary stream.
        /// </summary>
        public void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            Gamma.SaveCheckpoint(writer);
            Epsilon.SaveCheckpoint(writer);
            Alpha.SaveCheckpoint(writer);
            Tau.SaveCheckpoint(writer);
            Hysteresis.SaveCheckpoint(writer);
            Omega.SaveCheckpoint(writer);
            PenaltyAnnealingFactor.SaveCheckpoint(writer);
            Lambda.SaveCheckpoint(writer);
        }

        /// <summary>
        /// Restores all hyperparameter state from a binary stream, including mid-decay positions.
        /// </summary>
        public void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            Gamma.LoadCheckpoint(reader);
            Epsilon.LoadCheckpoint(reader);
            Alpha.LoadCheckpoint(reader);
            Tau.LoadCheckpoint(reader);
            Hysteresis.LoadCheckpoint(reader);
            Omega.LoadCheckpoint(reader);
            PenaltyAnnealingFactor.LoadCheckpoint(reader);
            Lambda.LoadCheckpoint(reader);
        }
    }
}
