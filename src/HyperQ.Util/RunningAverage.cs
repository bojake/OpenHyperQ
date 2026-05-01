using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    [Serializable]
    public class RunningAverage
    {
        private decimal _avg;
        private decimal _sum = 0M;
        private long _count;
        private int _gradient_window = 0;
        private readonly Queue<decimal> _gradient = new Queue<decimal>();

        /// <summary>
        /// The first value in the run
        /// </summary>
        public decimal First { get; private set; }
        /// <summary>
        /// The last value added to the run
        /// </summary>
        public decimal Last { get; private set; }

        public RunningAverage(decimal init_value = 0M, int gradient_window = 10)
        {
            _avg = init_value;
            _count = 0L;
            _gradient_window = gradient_window;
        }
        /// <summary>
        /// Returns the average differential of the last n average values where n
        /// is the gradient_window 
        /// </summary>
        public decimal Gradient
        {
            get
            {
                if (_gradient.Count > 0)
                {
                    decimal[] dd = _gradient.ToArray();
                    decimal diff = 0M;
                    for (int i = 1; i < dd.Length && i < _gradient_window; i++)
                    {
                        diff += dd[i] - dd[i - 1];
                    }
                    diff /= _gradient.Count;
                }
                return 0M;
            }
        }
        /// <summary>
        /// When a member in the run changes, and we know the diff, just adjust
        /// the average according to that diff. The sum is recomputed using this new
        /// average (sum = average * count)
        /// </summary>
        /// <param name="amt">The diff of x_new - x_old</param>
        /// <returns></returns>
        public decimal Update(decimal amt)
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("Can not update a running average with zero entries.");
            }
            _avg += amt / _count;
            _sum = _avg * _count;
            return _avg;
        }
        /// <summary>
        /// When a member in the run changes, and we know the diff, just adjust
        /// the average according to that diff. The sum is recomputed using this new
        /// average (sum = average * count)
        /// </summary>
        /// <param name="amt">The diff of x_new - x_old</param>
        /// <returns></returns>
        public decimal Update(double amt)
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("Can not update a running average with zero entries.");
            }
            _avg += (decimal)amt / _count;
            _sum = _avg * _count;
            return _avg;
        }
        /// <summary>
        /// When a member in the run changes, and we know the diff, just adjust
        /// the average according to that diff. The sum is recomputed using this new
        /// average (sum = average * count)
        /// </summary>
        /// <param name="diff">The diff of x_new - x_old</param>
        /// <returns></returns>
        public decimal Update(int diff)
        {
            if (_count == 0)
            {
                throw new InvalidOperationException("Can not update a running average with zero entries.");
            }
            _avg += (decimal)diff / _count;
            _sum = _avg * _count;
            return _avg;
        }
        /// <summary>
        /// Adds the given value to the running sum and average
        /// </summary>
        /// <param name="amt">Amount to add</param>
        /// <returns>The running average</returns>
        public decimal Add(decimal amt)
        {
            if (_count == 0)
            {
                First = amt;
            }
            _count++;
            _avg = _avg + (amt - _avg) / _count;
            Last = amt;
            _sum += amt;
            _gradient.Enqueue(amt);
            if (_gradient.Count > _gradient_window)
                _gradient.Dequeue();
            return (_avg);
        }

        /// <summary>
        /// Adds the given value to the running sum and average
        /// </summary>
        /// <param name="amt">Amount to add</param>
        /// <returns>The running average</returns>
        public decimal Add(int amt)
        {
            return Add((decimal)amt);
        }
        /// <summary>
        /// Adds the given value to the running sum and average
        /// </summary>
        /// <param name="amt">Amount to add</param>
        /// <returns>The running average</returns>
        public decimal Add(float amt)
        {
            return Add((decimal)amt);
        }
        /// <summary>
        /// Adds the given value to the running sum and average
        /// </summary>
        /// <param name="amt">Amount to add</param>
        /// <returns>The running average</returns>
        public decimal Add(double amt)
        {
            return Add((decimal)amt);
        }
        /// <summary>
        /// Adds the given value to the running sum and average
        /// </summary>
        /// <param name="amt">Amount to add</param>
        /// <returns>The running average</returns>
        public decimal Add(long amt)
        {
            return Add((decimal)amt);
        }

        /// <summary>
        /// The running average
        /// </summary>
        public decimal Value
        {
            get
            {
                return (_avg);
            }
        }

        /// <summary>
        /// The running sum
        /// </summary>
        public decimal Sum
        {
            get
            {
                return (_sum);
            }
        }

        /// <summary>
        /// The number of items in the run
        /// </summary>
        public long Count
        {
            get
            {
                return (_count);
            }
        }
    }
}
