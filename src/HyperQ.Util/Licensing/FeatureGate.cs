using System;
using System.Collections.Generic;

namespace HyperQ.Util.Licensing
{
    /// <summary>
    /// Answers whether a licensed feature is enabled and what limits apply. The library asks this at the
    /// points that used to be license checks; what stands behind it is up to the host: the default allows
    /// everything, and a commercial host installs a gate fed by its entitlement.
    /// </summary>
    public interface IFeatureGate
    {
        /// <summary>True when the feature key is enabled.</summary>
        bool IsEnabled(string featureKey);

        /// <summary>
        /// Returns true and the value when the limit key is present. An absent limit means unlimited; a value
        /// of zero means the capability is off (the Beyond Ordinary catalog convention).
        /// </summary>
        bool TryGetLimit(string limitKey, out long value);
    }

    /// <summary>The default gate: every feature is enabled and no limit applies.</summary>
    public sealed class AllowAllFeatureGate : IFeatureGate
    {
        public static readonly AllowAllFeatureGate Instance = new AllowAllFeatureGate();

        public bool IsEnabled(string featureKey)
        {
            return true;
        }

        public bool TryGetLimit(string limitKey, out long value)
        {
            value = 0;
            return false;
        }
    }

    /// <summary>A gate over an explicit feature set and limit table, such as a validated license payload.</summary>
    public sealed class FeatureSetGate : IFeatureGate
    {
        private readonly HashSet<string> _features;
        private readonly Dictionary<string, long> _limits;

        public FeatureSetGate(IEnumerable<string> features, IDictionary<string, long> limits = null)
        {
            _features = new HashSet<string>(features ?? new string[0], StringComparer.OrdinalIgnoreCase);
            _limits = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (limits != null)
            {
                foreach (KeyValuePair<string, long> kv in limits)
                {
                    _limits[kv.Key] = kv.Value;
                }
            }
        }

        public IReadOnlyCollection<string> Features
        {
            get { return _features; }
        }

        public IReadOnlyDictionary<string, long> Limits
        {
            get { return _limits; }
        }

        public bool IsEnabled(string featureKey)
        {
            return featureKey != null && _features.Contains(featureKey);
        }

        public bool TryGetLimit(string limitKey, out long value)
        {
            if (limitKey != null && _limits.TryGetValue(limitKey, out value))
            {
                return true;
            }
            value = 0;
            return false;
        }
    }

    /// <summary>Thrown when a component is constructed whose feature the current gate does not enable.</summary>
    public class FeatureNotLicensedException : InvalidOperationException
    {
        public string FeatureKey { get; private set; }

        public FeatureNotLicensedException(string featureKey)
            : base("The feature '" + featureKey + "' is not enabled by the current HyperQ license.")
        {
            FeatureKey = featureKey;
        }
    }

    /// <summary>
    /// The process-wide feature gate. Library components call <see cref="Require"/> where a license check
    /// belongs; a host that enforces entitlements sets <see cref="Current"/> once at startup.
    /// </summary>
    public static class FeatureGate
    {
        private static IFeatureGate _current = AllowAllFeatureGate.Instance;

        /// <summary>The active gate. Setting null restores the allow-all default.</summary>
        public static IFeatureGate Current
        {
            get { return _current; }
            set { _current = value ?? AllowAllFeatureGate.Instance; }
        }

        public static bool IsEnabled(string featureKey)
        {
            return _current.IsEnabled(featureKey);
        }

        /// <summary>Throws <see cref="FeatureNotLicensedException"/> when the feature is not enabled.</summary>
        public static void Require(string featureKey)
        {
            if (!_current.IsEnabled(featureKey))
            {
                throw new FeatureNotLicensedException(featureKey);
            }
        }

        /// <summary>The limit's value, or null when no limit is set (unlimited).</summary>
        public static long? Limit(string limitKey)
        {
            long v;
            return _current.TryGetLimit(limitKey, out v) ? v : (long?)null;
        }
    }

    /// <summary>
    /// The feature and limit keys HyperQ enforces. They follow the Beyond Ordinary catalog conventions
    /// (dot-namespaced feature keys, "limit.*" keys with long values, absent limit means unlimited) and are
    /// the keys to register for the product in the master entitlement catalog.
    /// </summary>
    public static class HyperQFeatures
    {
        /// <summary>The product code registered in the Beyond Ordinary licensing portal.</summary>
        public const string ProductCode = "BOSS.HYPERQ";

        /// <summary>Tabular Q learners (ClassicQ, MappedQ and everything built on BaseQ).</summary>
        public const string LearnerQ = "hyperq.learners.q";
        /// <summary>Double-Q learners: ClassicQQ, MappedQQ, DoubleHyperQ.</summary>
        public const string LearnerDoubleQ = "hyperq.learners.doubleQ";
        /// <summary>Hyper (composite state) learners: SingleHyperQ and the double variant.</summary>
        public const string LearnerHyperQ = "hyperq.learners.hyperQ";
        /// <summary>The layered hyper learner.</summary>
        public const string LearnerLayered = "hyperq.learners.layered";
        /// <summary>Multi-mind (MACE) training, evaluation and replay memory.</summary>
        public const string Mace = "hyperq.mace";
        /// <summary>Dyna model-based planning.</summary>
        public const string Dyna = "hyperq.dyna";
        /// <summary>Multi-head reward decomposition learners and evaluator.</summary>
        public const string MultiHead = "hyperq.multihead";

        /// <summary>Optional cap on the number of states a learner may hold; absent means unlimited.</summary>
        public const string LimitMaxStates = "limit.hyperq.maxStates";

        /// <summary>Every feature key, for hosts that enable everything (for example an evaluation edition).</summary>
        public static readonly string[] All =
        {
            LearnerQ, LearnerDoubleQ, LearnerHyperQ, LearnerLayered, Mace, Dyna, MultiHead
        };
    }
}
