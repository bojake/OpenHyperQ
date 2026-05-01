using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEM
{
    [Serializable]
    public class LEMState
    {
        /// <summary>
        /// The miles above the surface
        /// </summary>
        public double Altitude { get; private set; }
        /// <summary>
        /// The residual feet in the altitude
        /// </summary>
        public double AltitudeFeet
        {
            get
            {
                return Math.Truncate(5280 * (Altitude - Math.Truncate(Altitude)));
            }
        }
        public double Mass { get; private set; }
        public double FuelMass { get; private set; }
        public double Speed { get; private set; }
        public double SpeedMPH { get { return Math.Truncate(Speed * 3600.0); } }
        public double Gravity { get; private set; }
        public double LastBurn { get; private set; }

        private double _AltitudeMax = 0.0;
        private double _SpeedMax = 0.0;

        /// <summary>
        /// The score of the burn, which is based upon the differential change in altitude, speed, and fuel, favoring low fuel consumption
        /// for low altitude and low speed
        /// </summary>
        public double BurnScore { get; private set; }

        private const double Z = 1.8;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="a">Initial Altitude</param>
        /// <param name="m">Initial Total Mass of LEM (includes fuel)</param>
        /// <param name="n">Initial Mass of Fuel</param>
        /// <param name="v">Initial Speed</param>
        /// <param name="g">The gravity</param>
        public LEMState(double a =120, double m =33000, double n =16500, double v =1, double g =0.001)
        {
            Altitude = a;
            Mass = m;
            FuelMass = n;
            Speed = v;
            Gravity = g;
            _SpeedMax = v;
            _AltitudeMax = a;
        }

        public double StateScore
        {
            get
            {
                double d = Math.Pow(Altitude * Altitude + Speed * Speed + FuelMass * FuelMass, 1.0/3.0);
                return (d);
            }
        }

        public double NetFuel
        {
            get
            {
                return (Mass - FuelMass);
            }
        }
        /// <summary>
        /// Returns true when the total mass of the LEM is less than the initial fuel mass
        /// </summary>
        public bool OutOfFuel
        {
            get
            {
                return (NetFuel < 0.001);
            }
        }

        /// <summary>
        /// Computes the drift time for the lander to reach zero altitude and returns the time
        /// </summary>
        /// <returns></returns>
        public double Drift()
        {
            double step = (-Speed + Math.Sqrt(Speed * Speed + 2 * Altitude * Gravity)) / Gravity;
            Speed = Speed + Gravity * step;
            Altitude = 0;
            return (step);
        }
        /// <summary>
        /// Computes the next speed and altitude of the LEM given the step and burn rate
        /// </summary>
        /// <param name="step"></param>
        /// <param name="burn"></param>
        /// <returns>(speed,altitude)</returns>
        private Tuple<double,double> Update(double step, double burn)
        {
            double Q = step * burn / Mass;
            double Q2 = Q * Q;
            double Q3 = Q2 * Q;
            double Q4 = Q3 * Q;
            double Q5 = Q4 * Q;
            double J = Speed + Gravity * step + Z * (-Q - Q2 / 2 - Q3 / 3 - Q4 / 4 - Q5 / 5);
            double I = Altitude - Gravity * step * step / 2 - Speed * step + Z * step * (Q / 2 + Q2 / 6 + Q3 / 12 + Q4 / 20 + Q5 / 30);
            return new Tuple<double, double>(J, I);
        }

        private void Apply(double step, double burn, Tuple<double, double> result)
        {
            // Compute the burn score
            //BurnScore -= step * burn/FuelMass * 0.3;
            double vscore = 0.0;
            if (result.Item1 > Speed)
            {
                vscore = Math.Log10((result.Item1 - Speed) / _SpeedMax);
            }
            else
            {
                vscore = 1.0-Math.Log10((Speed - result.Item1) / _SpeedMax);
            }
            double ascore = 0.0;
            if (result.Item2 < 0.0)
            {
                ascore = -100.0;
            }
            else
            {
                ascore = 1.0 - (result.Item2 < 1e-6 ? 100.0 : Math.Log10(result.Item2 / _AltitudeMax));
            }
            BurnScore = vscore * ascore;
            // Update the telemetry
            Mass -= step * burn;
            Altitude = result.Item2;
            Speed = result.Item1;
        }

        public double ApplyBurn(double burn, double time=10.0)
        {
            LastBurn = burn;
            double elapsed = 0;
            BurnScore = 0.0;
            // Time decay loop
            while (time > 0.001)
            {
                double step = time;
                if (OutOfFuel)
                {
                    break;
                }
                if (Mass < FuelMass + step * burn)
                {
                    step = (Mass - FuelMass) / burn; // line 190
                }
                Tuple<double, double> r = Update(step, burn); // Line 200 -> 420
                if (r.Item2 <= 0.0) // Line 200 (I: altitude)
                {
                    while (step > 5e-3) // line 340
                    {
                        double D = Speed + Math.Sqrt(Speed * Speed + 2 * Altitude * (Gravity - Z * burn / Mass));
                        step = 2 * Altitude / D;
                        r = Update(step, burn); // -> line 420
                        // Line 330 in basic
                        elapsed += step;
                        time -= step;
                        Apply(step, burn, r); // -> Line 330
                    }
                    // Done
                    return elapsed;
                }
                if (Speed <= 0.0) // Line 210
                {
                    elapsed += step;
                    time -= step;
                    Apply(step, burn, r);
                    continue; // Line 230
                }
                if (r.Item1 < 0.0) // J : Speed
                {
                    do
                    {
                        // Line 370
                        double U = (1 - Mass * Gravity / (Z * burn)) / 2;
                        step = Mass * Speed / (Z * burn * (U + Math.Sqrt(U * U + Speed / Z))) + 0.5;
                        r = Update(step, burn);
                        if (r.Item2 <= 0.0)
                        {
                            while (step > 5e-3)
                            {
                                double D = Speed + Math.Sqrt(Speed * Speed + 2 * Altitude * (Gravity - Z * burn / Mass));
                                step = 2 * Altitude / D;
                                r = Update(step, burn);
                                // Line 330 in basic
                                elapsed += step;
                                time -= step;
                                Apply(step, burn, r);
                            }
                            return elapsed;
                        }
                        elapsed += step;
                        time -= step;
                        Apply(step, burn, r);
                        if (r.Item1 > 0.0)
                        {
                            // Line 390 -> 160
                            continue;
                        }
                    }
                    while (Speed > 0.0);
                }
                else
                {
                    elapsed += step;
                    time -= step;
                    Apply(step, burn, r);
                }
            }
            return elapsed;
        }
    }
}
