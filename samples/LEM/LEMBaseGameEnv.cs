using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEM
{
    public enum LanderStatus
    {
        Landing, // In the process of landing
        Crashed, // Smash
        Landed, // Successful
        FreeFall // Out of fuel
    }

    [Serializable]
    public class LEMBaseGameEnv
    {
        /// <summary>
        /// The factor used for the analog thruster input. This is also the number of actions per
        /// unit step of thrust. A value of 1 means only integer thrust values are supported. A value of
        /// 10 means a single significant figure is supported. 100 is 2 sigfigs, 1000 is 3, etc.
        /// </summary>
        protected const int DEFAULT_ACTION_PRECISION = 10;
        // Starting boundary conditions
        private const double START_ALTITUDE = 120;
        private const double START_VELOCITY = 1;
        private const double START_MASS = 32500;

        private double _ElapsedTime;
        private double _StepTimeDuration = 10.0;

        private double _LastScore = 0.0;
        private double _LastReward = 0.0;
        private double _RewardMax = 0.0;

        private Random _ran = null;

        public bool Quiet { get; set; } = false;

        public LanderStatus Status { get; private set; }

        private LEMState _LEM = null;

        private int _Precision = 0;
        public uint NumActions
        {
            get
            {
                // Action = 15034 is 150.34 thrust in the classic game.
                // Allowable is analog 8 - 200 in the original, or zero for freefall
                return ((uint)((200 - 8) * _Precision + 1)); // Lots of actions for specific precision
            }
        }
        public EnvMetrics Metrics { get; set; }

        public LEMBaseGameEnv(double g = 0.001, double netmass = 16500, Random ran = null, bool quiet = false, int precision = DEFAULT_ACTION_PRECISION, double stepduration = 10.0)
        {
            _ran = ran ?? new Random(0);
            _LEM = new LEMState(a: START_ALTITUDE, m: START_MASS, v: START_VELOCITY, g: g, n: netmass);
            _RewardMax = _LEM.StateScore;
            _StepTimeDuration = stepduration;
            Quiet = quiet;
            _Precision = precision;
        }
        /// <summary>
        /// Returns (altitude, velocity, mass, elapsed time, fuel mass remaining)
        /// </summary>
        public Tuple<double, double, double, double, double, LanderStatus> Report
        {
            get
            {
                return (new Tuple<double, double, double, double, double, LanderStatus>(_LEM.Altitude, _LEM.Speed, _LEM.Mass, _ElapsedTime, _LEM.NetFuel, Status));
            }
        }

        public void RenderGUI()
        {
            double burn = _LEM.LastBurn;
            // Compute the number of bars
            int bars3 = 0;
            while (burn > 100.0)
            {
                bars3++;
                burn = burn - 100.0;
            }
            int bars1 = 0;
            while (burn > 0)
            {
                bars1++;
                burn = burn - 25.0;
            }
            /*
               +-----+
               + LEM +
               +-----+
              /  ***  \
                 ***
                 *** bars3
                  *
                  *
                  * bars1
             */
            int max_lines = 30;
            int lem_lines = 4 + bars3 + bars1 - 1;
            max_lines -= lem_lines;
            double from_max_pct = 1 - (_LEM.Altitude * 5280.0 + _LEM.AltitudeFeet)/ (120.0 * 5280.0);
            int top_lines = (int)Math.Truncate(max_lines * from_max_pct);
            // 30 lines for the 120 miles of altitude
            Console.WriteLine(">>frame");
            string leftPad = new string(' ', 28);
            Console.WriteLine("|~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            for (int i = 0; i < top_lines; i++)
            {
                Console.WriteLine("|");
                max_lines--;
            }
            Console.WriteLine($"|{leftPad}+-----+");
            Console.WriteLine($"|{leftPad}+ LEM +");
            Console.WriteLine($"|{leftPad}+-----+");
            Console.Write($"|{leftPad}/ ");
            if (bars3 > 0)
            {
                Console.Write("***");
                bars3--;
            }
            else
            {
                if (bars1 > 0)
                {
                    Console.Write(" * ");
                    bars1--;
                }
                else
                {
                    Console.Write("   ");
                }
            }
            Console.WriteLine(" \\ ");
            while (bars3 > 0)
            {
                Console.WriteLine($"|{leftPad}  ***  ");
                bars3--;
            }
            while (bars1 > 0)
            {
                Console.WriteLine($"|{leftPad}   *   ");
                bars1--;
            }
            for (int i = 0; i < max_lines; i++)
            {
                Console.WriteLine("|");
            }
            Console.WriteLine("|");
            Console.WriteLine("|                        __     ____       ^");
            Console.WriteLine("|~~~~~~~~~~~~~~~~~~~~~~~/  \\~~~/----\\~~~~~~|~~~");
            // 24 columns for the graph
            Console.Write("|SPEED: [");
            int ratio = (int)Math.Truncate(_LEM.SpeedMPH/3600.0 * 24);
            int c = 0;
            if (ratio < 0)
            {
                ratio = 24 + ratio;
                if (ratio > 24)
                {
                    ratio = 24;
                }
                for (int i = 24 - ratio; i > 0; i--)
                {
                    Console.Write(" ");
                    c++;
                }
            }
            else if (ratio > 24)
            {
                ratio = 24;
            }
            for (int i = 0; i < ratio; i++)
            {
                Console.Write("*");
                c++;
            }
            while (c < 24)
            {
                Console.Write(" ");
                c++;
            }
            Console.WriteLine("] {0:F2} MPH", _LEM.SpeedMPH);
            c = 0;
            Console.Write("| FUEL: [");
            ratio = (int)Math.Truncate(_LEM.NetFuel / 16000.0 * 24);
            if (ratio < 0)
            {
                ratio = 0;
            }
            else if (ratio > 24)
            {
                ratio = 24;
            }
            for (int i = 0; i < ratio; i++)
            {
                Console.Write("*");
                c++;
            }
            while (c < 24)
            {
                Console.Write(" ");
                c++;
            }
            Console.WriteLine("] {0:F2} Lbs", _LEM.NetFuel);
        }

        public void Render()
        {
            if (Quiet)
                return;
            if (_ElapsedTime < 1e-5)
            {
                Console.WriteLine("CONTROL CALLING LUNAR MODULE. MANUAL CONTROL IS NECESSARY");
                Console.WriteLine("YOU MAY RESET FUEL RATE K EACH 10 SECS TO 0 OR ANY VALUE");
                Console.WriteLine("BETWEEN 8 & 200 LBS/SEC. YOU'VE 16000 LBS FUEL. ESTIMATED");
                Console.WriteLine("FREE FALL IMPACT TIME-120 SECS. CAPSULE WEIGHT-32500 LBS");
                Console.WriteLine("FIRST RADAR CHECK COMING UP");
                Console.WriteLine("COMMENCE LANDING PROCEDURE");
                Console.WriteLine("TIME,SECS   ALTITUDE,MILES+FEET   VELOCITY,MPH   FUEL,LBS   FUEL RATE,LPS   SCORE");
            }
            Console.Write("{0,8:F0}", _ElapsedTime);
            // Altitude
            Console.Write("{0,15}{1,7}", Math.Truncate(_LEM.Altitude), Math.Truncate(_LEM.AltitudeFeet));
            // VSI
            Console.Write("{0,15:F2}", _LEM.SpeedMPH);
            // Fuel
            Console.Write("{0,12:F1}", _LEM.NetFuel);
            // Fuel Rate
            double rate = (16000.0 - _LEM.NetFuel) / _ElapsedTime;
            Console.Write("{0,12:F2}", rate);
            // Reward for this maneuver
            Console.Write("{0,12:F2}", _LastReward);
            Console.WriteLine();
        }
        public virtual void Reset()
        {
            _LEM = new LEMState(a: START_ALTITUDE, m: START_MASS, v: START_VELOCITY, g: _LEM.Gravity, n: _LEM.FuelMass);
            _RewardMax = _LEM.StateScore;
            _ElapsedTime = 0;
            Status = LanderStatus.Landing;
            _LastScore = 0.0;
            _LastReward = 0.0;
        }
        public double ActionToBurn(int action)
        {
            double burn = 0.0;
            if (action > 0)
            {
                burn = (double)(action - 1 + 8 * _Precision) / _Precision;
            }
            return (burn);
        }
        public Tuple<double, bool> Step(QAction action)
        {
            double score = 0.0;
            // score_diff captures the previous step's score so that in-flight
            // rewards are expressed as a differential (improvement signal) rather
            // than an absolute value, giving the agent a smoother gradient to follow.
            double score_diff = _ElapsedTime > 0.0 ? _LastScore : 0.0;
            double burn = ActionToBurn(action.Action);
            if (!Quiet)
                Console.WriteLine("K=: {0:F2}", burn);
            _ElapsedTime += _LEM.ApplyBurn(burn, time: _StepTimeDuration);
            if (_LEM.OutOfFuel)
            {
                _ElapsedTime += _LEM.Drift();
                if (!Quiet)
                {
                    Console.WriteLine("FUEL OUT, IMPACT AT {0} SECONDS @ {1:F2} MPH", _ElapsedTime, _LEM.SpeedMPH);
                }
            }
            double U = _LEM.SpeedMPH;
            score = _LEM.BurnScore;
            bool terminal = false;
            if (_LEM.Altitude < 1e-5)
            {
                terminal = true;
                // Check for landing
                if (U <= 1.2)
                {
                    if (!Quiet)
                        Console.WriteLine("PERFECT LANDING!");
                    Status = LanderStatus.Landed;
                    score = (10.0 - U) * 100;          // up to 880
                }
                else if (U <= 10.0)
                {
                    if (!Quiet)
                        Console.WriteLine("GOOD LANDING (COULD BE BETTER)");
                    score = 2.0 * 100;                  // 200
                    Status = LanderStatus.Landed;
                }
                else if (U > 60.0)
                {
                    if (!Quiet)
                    {
                        Console.WriteLine("SORRY THERE WERE NO SURVIVORS. YOU BLEW IT!");
                        Console.WriteLine("IN FACT, YOU BLASTED A NEW LUNAR CRATER {0} FEET DEEP!", 0.277 * U);
                    }
                    Status = LanderStatus.Crashed;
                    // Penalty scaled to be commensurate with terminal landing rewards
                    // (original -0.277*U gave only ~-17 for a 60MPH crash vs +880 for a
                    // perfect landing, providing almost no deterrent signal.)
                    score = -(U * 10.0);
                }
                else
                {
                    if (!Quiet)
                    {
                        Console.WriteLine("CRAFT DAMAGE ... YOU'RE STRANDED HERE UNTIL A RESCUE");
                        Console.WriteLine("PARTY ARRIVES. HOPE YOU HAVE ENOUGH OXYGEN!");
                    }
                    // Reward proportional to how slow the impact was (0–100 range),
                    // clearly below a good landing (200) to preserve the gradient.
                    score = (60.0 - U) * 2.0;
                    Status = LanderStatus.Landed;
                }
            }
            else if (_LEM.OutOfFuel)
            {
                terminal = true;
                Status = LanderStatus.Crashed;
                score = -(U * 10.0);
            }
            // Terminal steps use the absolute reward; in-flight steps use the
            // differential so the agent is rewarded for improvement each step.
            double reward = terminal ? score : (score - score_diff);
            _LastScore = score;
            _LastReward = reward;
            return new Tuple<double, bool>(reward, Status == LanderStatus.Landing ? false : true);
        }
    }
}
