using System;

namespace HyperQ.MultiHead
{
    /*
LinearScalarizer(weights[]) => dot product

ChebyshevScalarizer(weights[], utopia[]) => Pareto-ish coverage

ConstraintScalarizer(costHead, maxCost, innerScalarizer) => hard constraints
    */
    public interface IRewardScalarizer
    {
        int Dims { get; }

        // Per-head Q estimates for a single (s,a) -> returns scalar score
        double Score(ReadOnlySpan<double> qPerHead);
    }
    public class LinearScalarizer : IRewardScalarizer
    {
        private readonly double[] _weights;
        public int Dims { get; }
        public LinearScalarizer(double[] weights)
        {
            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }
            if (weights.Length == 0)
            {
                throw new ArgumentException("weights can not be empty.", nameof(weights));
            }
            _weights = (double[])weights.Clone();
            Dims = weights.Length;
        }
        public double Score(ReadOnlySpan<double> qPerHead)
        {
            if (qPerHead.Length < _weights.Length)
            {
                throw new ArgumentException("qPerHead has fewer dimensions than scalarizer weights.", nameof(qPerHead));
            }
            double r = 0;
            for (int i = 0; i < _weights.Length; i++)
            {
                r += _weights[i] * qPerHead[i];
            }
            return r;
        }
    }
}

