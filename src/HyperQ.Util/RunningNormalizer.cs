using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public class RunningNormalizer 
    {
        private Dictionary<int,double> _values = new Dictionary<int,double>();
        private double _normalizer = 0.0;

        public RunningNormalizer()
        {
        }

        /// <summary>
        /// Copy ctor that makes a copy of the dictionary of values
        /// </summary>
        /// <param name="copy"></param>
        public RunningNormalizer(RunningNormalizer copy)
        {
            foreach (int key in copy._values.Keys)
                _values[key] = copy._values[key];
            _normalizer = copy._normalizer;
        }

        public virtual IEnumerable<int> Keys
        {
            get
            {
                return _values.Keys.AsEnumerable<int>();
            }
        }

        protected virtual bool __has_value(int idx)
        {
            return (_values.ContainsKey(idx));
        }

        /// <summary>
        /// Returns the value exactly as it was stored for the given index (0 when the index has never been
        /// set), bypassing any transformation a subclass applies on read.
        /// </summary>
        protected double __raw_value(int idx)
        {
            double v;
            return _values.TryGetValue(idx, out v) ? v : 0.0;
        }

        protected virtual double __get_value(int idx)
        {
            if (__has_value(idx))
            {
                return _values[idx];
            }
            return 0f;
        }

        protected virtual bool __set_value(int idx, double v)
        {
            _values[idx] = v;
            return false;
        }

        protected virtual void __renorm()
        {
            _normalizer = 0.0;
            foreach (int key in _values.Keys)
                _normalizer += __get_value(key);
        }

        /// <summary>
        /// Returns or sets the value of the given index, the value returned is normalized
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        public virtual double this[int idx]
        {
            get
            {
                return __get_value(idx) / _normalizer;
            }
            set
            {
                bool updated_norm = false;
                double old_v = 0.0;
                if (__has_value(idx))
                {
                    old_v = __get_value(idx);
                    updated_norm = true;
                }
                if (__set_value(idx, value))
                    __renorm();
                else
                {
                    double v = __get_value(idx);
                    _normalizer += v;
                    if (updated_norm)
                    {
                        _normalizer -= old_v; // Remove the old value from the normalizer
                    }
                }
            }

        }

    }
}
