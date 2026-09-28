using HyperQ.Util;
using HyperQ.Env;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntTheWumpus
{
    public enum PlayerStatus
    {
        Alive,
        Escaped,
        Dead,
        Win
    }
    public enum RewardModelType
    {
        Model1,
        Model2,
        Model3
    }
    public class WumpusGameStatus : Tuple<int, int, int, int, PlayerStatus, CaveItem[],int>
    {
        public int Row { get { return Item1; } }
        public int Column { get { return Item2; } }
        public int Gold { get { return Item3; } }
        public int Location { get { return Item4; } }
        public CaveItem[] Adjacent { get { return Item6; } }
        public PlayerStatus Status { get { return Item5; } }
        public int FoodLevel { get { return Item7; } }

        public WumpusGameStatus(int row, int column, int gold, int loc, PlayerStatus status, CaveItem[] adjacent, int food) : base(row, column, gold, loc, status, adjacent, food)
        {
        }
    }
    public class WumpusBaseGameEnv
    {
        private const int FOODMAX = 64; // for number of bits optimization
        /*
         * Game State at any time is what can be seen around the player in terms of 8 directions
         * (Tile Status left, top, right, button, left-top, right-top, left-bottom, right-bottom)
         * Tile Status is: 000=blocked,001=open,010=wumpus,011=gold,100=pit,101=food
         * Status encoding is 24 bits (8 directions, 3 bits per direction)
         */
        private int _resetSeed = -1;
        private WumpusCave _Cave = null;
        private int _Steps;
        /// <summary>
        /// The player location as (row,column)
        /// </summary>
        private Tuple<int, int> _Player = new Tuple<int, int>(0, 0);
        private int _Gold = 0;
        /// <summary>
        /// Start location as (row,column)
        /// </summary>
        private Tuple<int, int> _Start = null;
        private int _Food = FOODMAX;
        public PlayerStatus Status { get; private set; } = PlayerStatus.Alive;
        private double[,] _Explored = null;
        public RewardModelType RewardModel { get; set; } = RewardModelType.Model3;
        private QRandom _ran = null;

        public bool Quiet { get; set; } = false;

        public EnvMetrics Metrics { get; set; }
        /// <summary>
        /// Dimensions of the arena, (rows,columns)
        /// </summary>
        public Tuple<int, int> Dimensions { get; set; } = new Tuple<int, int>(12, 12);
        private double _LastStepReward = 0.0;
        private Queue<Tuple<int,int>> _LastLocations = new Queue<Tuple<int,int>>();
        /// <summary>
        /// Movement, up/down/left/right like in GridWorld
        /// </summary>
        public int[] NumActions
        {
            get
            {
                // action[0] = movement 0,1,2,3 and diagonal 4,5,6,7
                return new int[] { 8 };
            }
        }

        public WumpusBaseGameEnv(QRandom ran = null, bool quiet = false, RewardModelType rewardModel = default, Tuple<int,int> dims = default)
        {
            _ran = ran;
            if (_ran == null)
            {
                // A static world: its own source, restarted from the same seed on every reset, so every episode
                // plays the same cave. (It used to reseed the process-wide source, which also rewound the
                // learners' and selectors' draws at every reset.)
                _resetSeed = 903387237;
                _ran = new QRandom(_resetSeed);
                Console.WriteLine("CREATED WORLD WITH STATIC SEED {0}", _resetSeed);
            }
            if (dims == null)
            {
                dims = new Tuple<int, int>(12, 12);
            }
            Quiet = quiet;
            RewardModel = rewardModel;
            Dimensions = dims;
        }
        public void Render()
        {
            if (Quiet)
                return;
            if (_Steps == 0)
            {
                // Output instructions
                Console.WriteLine("HUNT THE WUMPUS. MOVE UP/DOWN/LEFT/RIGHT AND DIAGONAL TO GET TREASURE");
            }
            Console.WriteLine(">>frame");
            Console.WriteLine(_Cave.DrawBoard(_Player));
        }
        public virtual void Reset()
        {
            if (_resetSeed >= 0)
            {
                _ran.Seed(_resetSeed);
                if (!Quiet)
                {
                    Console.WriteLine("RESET WORLD WITH STATIC SEED {0}", _resetSeed);
                }
            }
            _Steps = 0;
            _Cave = new WumpusCave(Dimensions.Item1, Dimensions.Item2, _ran);
            _Player = _Cave.RandomStartLocation();
            _Gold = 0;
            _Food = FOODMAX;
            _Start = null;
            Status = PlayerStatus.Alive;
            _Explored = new double[_Cave.Rows, _Cave.Columns];
            _LastStepReward = 0.0;
            _LastLocations = new Queue<Tuple<int, int>>();
            for (int r = 0; r < _Cave.Rows; r++)
            {
                for (int c = 0; c < _Cave.Columns; c++)
                {
                    _Explored[r, c] = 1.0;
                }
            }
        }

        public WumpusGameStatus Report
        {
            get
            {
                int loc = _Player.Item1 * _Cave.Columns + _Player.Item2;
                return (new WumpusGameStatus(_Player.Item1, _Player.Item2, _Gold, loc, Status, _Cave.WhatDoISee(_Player.Item1, _Player.Item2),_Food));
            }
        }

        /// <summary>
        /// Snapshot of the current cave layout.
        /// </summary>
        /// <returns>Copy of cave items for each cell.</returns>
        public CaveItem[,] GetCaveSnapshot()
        {
            if (_Cave == null)
                return null;
            CaveItem[,] map = new CaveItem[_Cave.Rows, _Cave.Columns];
            for (int r = 0; r < _Cave.Rows; r++)
            {
                for (int c = 0; c < _Cave.Columns; c++)
                {
                    map[r, c] = _Cave.LocationStatus(r, c);
                }
            }
            return map;
        }

        /// <summary>
        /// Current player location as (row,column).
        /// </summary>
        public Tuple<int, int> PlayerLocation => _Player;

        private int DistanceFromStart()
        {
            int dr = _Player.Item1 - _Start.Item1;
            if (dr < 0)
                dr = -dr;
            int dc = _Player.Item2 - _Start.Item2;
            if (dc < 0)
                dc = -dc;
            return dr < dc ? dc : dr;
        }

        private int DistanceFromGold(Tuple<int,int> loc)
        {
            if (loc != null && _Cave.TreasureLocation != null)
            {
                int dr = _Cave.TreasureLocation.Item1 - loc.Item1;
                if (dr < 0)
                    dr = -dr;
                int dc = _Cave.TreasureLocation.Item2 - loc.Item2;
                if (dc < 0)
                    dc = -dc;
                return dr < dc ? dc : dr;
            }
            return (0);
        }
        /// <summary>
        /// Model 1 - No reward for steps, negative reward for invalid moves
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        private Tuple<double, bool> Model1Step(QAction[] action)
        {
            if (_Start == null)
            {
                _Start = _Player;
            }
            double reward = 0.0;
            Tuple<int,int> p = _Cave.AreaOffsets[action[0].Action];
            int r = _Player.Item1 + p.Item1;
            int c = _Player.Item2 + p.Item2;
            int dg1 = DistanceFromGold(_Player);
            if (_Steps % 10 == 0)
            {
                _Food--;
            }
            if (_Food <= 0)
            {
                if (!Quiet)
                    Console.WriteLine("YOU RAN OUT OF FOOD. YOU ARE DEAD.");
                Status = PlayerStatus.Dead;
                reward = -7.0;
            }
            else if (_Cave.CanMoveInto(r, c))
            {
                switch (_Cave[r, c])
                {
                    case CaveItem.Wumpus:
                        if (!Quiet)
                            Console.WriteLine("YOU FOUND THE WUMPUS AND IT ATE YOU. YOU ARE DEAD.");
                        Status = PlayerStatus.Dead;
                        reward = -10.0;
                        break;
                    case CaveItem.Treasure:
                        _Cave.TakeGold(r, c);
                        if (!Quiet)
                            Console.WriteLine("YOU FOUND THE GOLD! NOW YOU NEED TO ESCAPE");
                        _Gold++;
                        reward = 20.0;
                        break;
                    case CaveItem.Pit:
                        if (!Quiet)
                            Console.WriteLine("YOU FELL INTO A BOTTOMLESS PIT. YOU ARE DEAD.");
                        Status = PlayerStatus.Dead;
                        reward = -10.0;
                        break;
                    case CaveItem.Exit:
                        if(!Quiet)
                            Console.WriteLine("YOU FOUND THE EXIT WITH {0} GOLD. BYE", _Gold);
                        Status = PlayerStatus.Escaped;
                        reward = _Gold * 100.0;
                        if (_Gold == 0)
                            reward = -2.0;
                        else
                            Status = PlayerStatus.Win;
                        break;
                    case CaveItem.Food:
                        if (!Quiet)
                            Console.WriteLine("YUM! YOU FOUND SOME FOOD");
                        // If starving, get bigger reward
                        if (_Food < 5)
                            reward = 5.0;
                        else if (_Food < 20)
                            reward = 2.5;
                        else
                            reward = 0.5; // not hungry, wasted food
                        _Food += _Cave.EatFood(r, c);
                        if (_Food > FOODMAX)
                        {
                            _Food = FOODMAX;
                        }
                        break;
                    default:
                        reward = 0.0; // _Explored[r, c];
                        _Player = new Tuple<int, int>(r, c);
                        if (_Gold == 0 && dg1 < 2 && dg1 < DistanceFromGold(_Player))
                        {
                            // negative reward for skipping the gold when near
                            if (!Quiet)
                                Console.WriteLine("YOU MISSED THE TREASURE, PENALTY!");
                            reward = -1.0;
                        }
                        else 
                            _Explored[r, c] *= 0.998; // Decay the value of repeat visits
                        break;
                }
            }
            else {
                if(!Quiet)
                    Console.WriteLine("INVALID MOVE");
                reward = -1.0;
                double food_pct = 1.0 - (double)_Food / (double)FOODMAX;

                if (_Gold == 0 && dg1 < 2)
                {
                    if (!Quiet)
                        Console.WriteLine("INVALID MOVE NEAR GOLD, PENALTY!");
                    reward = -5.0;
                }
                if(food_pct > 0.0)
                    reward = reward / food_pct;
            }
            if (!Quiet)
            {
                Console.WriteLine("STEP: at ({0},{1}), action={2}, reward={3}, gold={4}, Food={5}", _Player.Item1, _Player.Item2, action[0],reward,_Gold,_Food);
                if(Status == PlayerStatus.Alive)
                    if (_Food < 20)
                    {
                        if (_Food < 5)
                        {
                            Console.WriteLine("YOU ARE STARVING");
                        }
                        else
                            Console.WriteLine("YOU ARE GETTING HUNGRY");
                    }
            }
            _Steps++;
            return new Tuple<double, bool>(reward, Status == PlayerStatus.Dead || Status == PlayerStatus.Escaped || Status == PlayerStatus.Win);
        }

        /// <summary>
        /// Model 2 - Reward for steps, negative reward for invalid move, decay step reward when revisit
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        private Tuple<double, bool> Model2Step(QAction[] action)
        {
            if (_Start == null)
            {
                _Start = _Player;
            }
            double reward = 0.0;
            Tuple<int, int> p = _Cave.AreaOffsets[action[0].Action];
            int r = _Player.Item1 + p.Item1;
            int c = _Player.Item2 + p.Item2;
            int dg1 = DistanceFromGold(_Player);
            if (_Steps % 10 == 0)
            {
                _Food--;
            }
            if (_Food <= 0)
            {
                if (!Quiet)
                    Console.WriteLine("YOU RAN OUT OF FOOD. YOU ARE DEAD.");
                Status = PlayerStatus.Dead;
                reward = -7.0;
            }
            else if (_Cave.CanMoveInto(r, c))
            {
                switch (_Cave[r, c])
                {
                    case CaveItem.Wumpus:
                        if (!Quiet)
                            Console.WriteLine("YOU FOUND THE WUMPUS AND IT ATE YOU. YOU ARE DEAD.");
                        Status = PlayerStatus.Dead;
                        reward = -10.0;
                        break;
                    case CaveItem.Treasure:
                        _Cave.TakeGold(r, c);
                        if (!Quiet)
                            Console.WriteLine("YOU FOUND THE GOLD! NOW YOU NEED TO ESCAPE");
                        _Gold++;
                        reward = 20.0;
                        break;
                    case CaveItem.Pit:
                        if (!Quiet)
                            Console.WriteLine("YOU FELL INTO A BOTTOMLESS PIT. YOU ARE DEAD.");
                        Status = PlayerStatus.Dead;
                        reward = -10.0;
                        break;
                    case CaveItem.Exit:
                        if (!Quiet)
                            Console.WriteLine("YOU FOUND THE EXIT WITH {0} GOLD. BYE", _Gold);
                        Status = PlayerStatus.Escaped;
                        reward = _Gold * 200.0 - 100.0;
                        if (_Gold == 1)
                            Status = PlayerStatus.Win;
                        break;
                    case CaveItem.Food:
                        if (!Quiet)
                            Console.WriteLine("YUM! YOU FOUND SOME FOOD");
                        // If starving, get bigger reward
                        if (_Food < 5)
                            reward = 5.0;
                        else if (_Food < 20)
                            reward = 2.5;
                        else
                            reward = 0.5; // not hungry, wasted food
                        _Food += _Cave.EatFood(r, c);
                        if (_Food > FOODMAX)
                        {
                            _Food = FOODMAX;
                        }
                        break;
                    default:
                        reward = _Explored[r, c];
                        _Player = new Tuple<int, int>(r, c);
                        if (_Gold == 0 && dg1 < 2 && dg1 < DistanceFromGold(_Player))
                        {
                            // negative reward for skipping the gold when near
                            if (!Quiet)
                                Console.WriteLine("YOU MISSED THE TREASURE, PENALTY!");
                            reward += -1.0;
                        }
                        else
                            _Explored[r, c] *= 0.998; // Decay the value of repeat visits
                        break;
                }
            }
            else
            {
                if (!Quiet)
                    Console.WriteLine("INVALID MOVE");
                reward = -1.0;
                double food_pct = 1.0 - (double)_Food / (double)FOODMAX;

                if (_Gold == 0 && dg1 < 2)
                {
                    if (!Quiet)
                        Console.WriteLine("INVALID MOVE NEAR GOLD, PENALTY!");
                    reward = -5.0;
                }
                if (food_pct > 0.0)
                    reward = reward / food_pct;
            }
            if (!Quiet)
            {
                Console.WriteLine("STEP: at ({0},{1}), action={2}, reward={3}, gold={4}, Food={5}", _Player.Item1, _Player.Item2, action[0], reward, _Gold, _Food);
                if (Status == PlayerStatus.Alive)
                    if (_Food < 20)
                    {
                        if (_Food < 5)
                        {
                            Console.WriteLine("YOU ARE STARVING");
                        }
                        else
                            Console.WriteLine("YOU ARE GETTING HUNGRY");
                    }
            }
            _Steps++;
            return new Tuple<double, bool>(reward, Status == PlayerStatus.Dead || Status == PlayerStatus.Escaped || Status == PlayerStatus.Win);
        }
        /// <summary>
        /// Model 2 - Reward for steps, negative reward for invalid move, decay step reward when revisit
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        private Tuple<double, bool> Model3Step(QAction[] action)
        {
            if (_Start == null)
            {
                _Start = _Player;
            }
            double reward = 0.0;
            Tuple<int, int> p = _Cave.AreaOffsets[action[0].Action];
            int r = _Player.Item1 + p.Item1;
            int c = _Player.Item2 + p.Item2;
            int dg1 = DistanceFromGold(_Player);
            if (_Steps % 10 == 0)
            {
                _Food--;
            }
            if (_Food <= 0)
            {
                if (!Quiet)
                    Console.WriteLine("YOU RAN OUT OF FOOD. YOU ARE DEAD.");
                Status = PlayerStatus.Dead;
                reward = -7.0;
            }
            else if (_Cave.CanMoveInto(r, c))
            {
                switch (_Cave[r, c])
                {
                    case CaveItem.Wumpus:
                        if (!Quiet)
                            Console.WriteLine("YOU FOUND THE WUMPUS AND IT ATE YOU. YOU ARE DEAD.");
                        Status = PlayerStatus.Dead;
                        reward = -2.0;
                        break;
                    case CaveItem.Treasure:
                        _Cave.TakeGold(r, c);
                        if (!Quiet)
                            Console.WriteLine("YOU FOUND THE GOLD! NOW YOU NEED TO ESCAPE");
                        _Gold++;
                        reward = 5.0;
                        break;
                    case CaveItem.Pit:
                        if (!Quiet)
                            Console.WriteLine("YOU FELL INTO A BOTTOMLESS PIT. YOU ARE DEAD.");
                        Status = PlayerStatus.Dead;
                        reward = -1.0;
                        break;
                    case CaveItem.Exit:
                        if (!Quiet)
                            Console.WriteLine("YOU FOUND THE EXIT WITH {0} GOLD. BYE", _Gold);
                        Status = PlayerStatus.Escaped;
                        reward = _Gold * 2.0;
                        if (_Gold == 1)
                            Status = PlayerStatus.Win;
                        break;
                    case CaveItem.Food:
                        if (!Quiet)
                            Console.WriteLine("YUM! YOU FOUND SOME FOOD");
                        // If starving, get bigger reward
                        if (_Food < 5)
                            reward = 5.0;
                        else if (_Food < 20)
                            reward = 2.5;
                        else
                            reward = 0.5; // not hungry, wasted food
                        _Food += _Cave.EatFood(r, c);
                        if (_Food > FOODMAX)
                        {
                            _Food = FOODMAX;
                        }
                        break;
                    default:
                        reward = 0.0;
                        break;
                }
            }
            else
            {
                if (!Quiet)
                    Console.WriteLine("INVALID MOVE");
                reward = -1.0;
                double food_pct = 1.0 - (double)_Food / (double)FOODMAX;

                if (_Gold == 0 && dg1 < 2)
                {
                    if (!Quiet)
                        Console.WriteLine("INVALID MOVE NEAR GOLD, PENALTY!");
                    reward = -5.0;
                }
                if (food_pct > 0.0)
                    reward = reward / food_pct;
            }
            if (!Quiet)
            {
                Console.WriteLine("STEP: at ({0},{1}), action={2}, reward={3}, gold={4}, Food={5}", _Player.Item1, _Player.Item2, action[0], reward, _Gold, _Food);
                if (Status == PlayerStatus.Alive)
                    if (_Food < 20)
                    {
                        if (_Food < 5)
                        {
                            Console.WriteLine("YOU ARE STARVING");
                        }
                        else
                            Console.WriteLine("YOU ARE GETTING HUNGRY");
                    }
            }
            _Steps++;
            return new Tuple<double, bool>(reward, Status == PlayerStatus.Dead || Status == PlayerStatus.Escaped || Status == PlayerStatus.Win);
        }
        public EnvResult<ScalarReward> Step(QAction[] action)
        {
            Tuple<double, bool> r = null;
            switch (RewardModel)
            {
                case RewardModelType.Model1: r= Model1Step(action); break;
                case RewardModelType.Model2: r= Model2Step(action);break;
                case RewardModelType.Model3: r = Model3Step(action); break;
                default:
                    throw new NotImplementedException();
            }
            // Check for loitering
            double loitering_penalty = 0.0;
            Tuple<int, int> loc = PlayerLocation;
            if (_LastLocations.Count(p => p.Item1 == loc.Item1 && p.Item2 == loc.Item2) > 2)
            {
                // Loitering penalty
                loitering_penalty += 2.0;
            }
            while (_LastLocations.Count > 6)
                _LastLocations.Dequeue();
            _LastLocations.Enqueue(PlayerLocation);
            // Shape the reward as a differential
            double diff = r.Item1 - _LastStepReward - loitering_penalty;
            _LastStepReward = r.Item1 - loitering_penalty;
            return new EnvResult<ScalarReward>(new ScalarReward(diff), r.Item2);

        }
    }
}
