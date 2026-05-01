using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public class RunningMeasureOfLearning
    {
        private RunningAverage _past = new RunningAverage(0M);
        private RunningAverage _recent = new RunningAverage(0M);
        private RunningAverage _avgValue = new RunningAverage(0M);
        private RunningAverage _avgRelativeValue = new RunningAverage(0M);
        private double[] _entries = null;
        private int _width = 0;
        private int _primingToGo = 0;
        public RunningMeasureOfLearning(int windowWidth)
        {
            _entries = new double[windowWidth*2];
            _width = windowWidth;
            _primingToGo = windowWidth * 2;
        }
        public RunningMeasureOfLearning Add(double value)
        {
            // Fill in the list during the priming phase
            if (_primingToGo == 0)
            {
                // Primed, now replace
                // The first past entry is being replaced with the first recent entry
                double pastUpdate = _entries[_width] - _entries[0];
                _past.Update(pastUpdate);
                // The first recent entry is being replaced by the new value
                double recentUpdate = value - _entries[_width];
                _recent.Update(recentUpdate);
                try
                {
                    _avgValue.Add(Value);
                    _avgRelativeValue.Add(RelativeValue);
                }
                catch (Exception)
                {
                    // Can happen if the values blow up in the denom
                }
                // Shift the array
                for (int j = 1; j < _entries.Length; j++)
                {
                    _entries[j - 1] = _entries[j];
                }
                _entries[_entries.Length - 1] = value;
            }
            else if (_primingToGo > _width)
            {
                _entries[_entries.Length - _primingToGo] = value;
                _past.Add(value);
                _primingToGo--;
            }
            else if (_primingToGo <= _width)
            {
                _entries[_entries.Length - _primingToGo] = value;
                _recent.Add(value);
                _primingToGo--;
            }
            return this;
        }

        public bool IsPrimed
        {
            get
            {
                return _primingToGo == 0;
            }
        }
        /// <summary>
        /// Returns the running average value of the Value metric
        /// as the relative value is recomputed after every change
        /// </summary>
        public decimal AverageValue
        {
            get
            {
                return _avgValue.Value;
            }
        }

        /// <summary>
        /// Returns the running average value of the Relative Value metric
        /// as the relative value is recomputed after every change
        /// </summary>
        public decimal AverageRelativeValue
        {
            get
            {
                return _avgRelativeValue.Value;
            }
        }

        /// <summary>
        /// Returns the symmetrized ratio (recent-past)/(|recent|+|past|)
        /// </summary>
        public decimal Value
        {
            get
            {
                // Return the symmetrized ratio
                decimal rv = _recent.Value;
                decimal pv = _past.Value;
                decimal diff = rv - pv;
                decimal denom = Math.Abs(rv) + Math.Abs(pv);
                if (denom < 1e-9M)
                    return 0M;
                return diff / denom;
            }
        }
        /// <summary>
        /// Returns the relative change of the recent value to the past value: (recent-past)/|past|
        /// </summary>
        public decimal RelativeValue
        {
            get
            {
                decimal rv = _recent.Value;
                decimal pv = _past.Value;
                decimal diff = rv - pv;
                decimal denom = Math.Abs(pv);
                if (denom < 1e-9M)
                    return 0M;
                return diff / denom;
            }
        }
    }
}
