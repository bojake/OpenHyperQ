using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Samples.GridWorld
{
    public class BaseGridWorld
    {
        QRandom _ran = null;
        /// <summary>
        /// Location of the goal
        /// </summary>
        public int GoalRow { get; private set; } = 0;
        public int GoalColumn { get; private set; } = 0;
        /// <summary>
        /// Location of the subject
        /// </summary>
        public int CurrentRow { get; private set; } = 0;
        public int CurrentColumn { get; private set; } = 0;
        /// <summary>
        /// The dimensions of the grid world
        /// </summary>
        public int MaxRows { get; private set; } = 0;
        public int MaxColumns { get; private set; } = 0;

        public bool SpartanMode { get; set; } = false;

        public BaseGridWorld(int r, int c, QRandom ran)
        {
            this._ran = ran ?? QRandom.Instance;
            GoalRow = _ran.Ran.Next(r);
            GoalColumn = _ran.Ran.Next(c);
            MaxRows = r;
            MaxColumns = c;
            Reset();
            Metrics = new EnvMetrics();
        }

        public decimal[] Update(QAction action)
        {
            decimal reward = 0M;
            // 4 actions, up=0, right=1, down=2, left=3
            switch (action.Action)
            {
                case 0: // up
                    if (CurrentRow + 1 == MaxRows)
                    {
                        reward = -1M;
                    }
                    else
                    {
                        CurrentRow++;
                    }
                    break;
                case 1: // right
                    if (CurrentColumn + 1 == MaxColumns)
                    {
                        reward = -1M;
                    }
                    else
                    {
                        CurrentColumn++;
                    }
                    break;
                case 2: // down
                    if (CurrentRow == 0)
                    {
                        reward = -1M;
                    }
                    else
                    {
                        CurrentRow--;
                    }
                    break;
                case 3: // left
                    if (CurrentColumn == 0)
                    {
                        reward = -1M;
                    }
                    else
                    {
                        CurrentColumn--;
                    }
                    break;
            }
            if (Done)
            {
                reward = 2M;
            }
            else {
                if (!(reward < 0M) && !SpartanMode)
                    {
                        double dx = GoalColumn - CurrentColumn;
                        double dy = GoalRow - CurrentRow;
                        int max_r = Math.Max(Math.Abs(MaxRows - GoalRow), GoalRow);
                        int max_c = Math.Max(Math.Abs(MaxColumns - GoalColumn), GoalColumn);
                        double max = Math.Sqrt(max_r + max_c); // max diag distance from any cell
                        reward = (decimal)(max - Math.Sqrt(dx * dx + dy * dy));
                    }
            }
            return (new decimal[] { CurrentState, reward });
        }

        public void Render()
        {
            // Do nothing
        }

        public EnvResult<ScalarReward> Step(QAction action)
        {
            decimal[] up = Update(action);
            EnvResult<ScalarReward> result = new EnvResult<ScalarReward>((double)up[1], Done);
            return result;
        }

        public void Reset()
        {
            do
            {
                CurrentRow = _ran.Ran.Next(MaxRows);
                CurrentColumn = _ran.Ran.Next(MaxColumns);
            } while (CurrentRow == GoalRow && CurrentColumn == GoalColumn);
        }

        public decimal CurrentState
        {
            get
            {
                return CurrentRow * MaxColumns + CurrentColumn;
            }
        }
        public QState<decimal> CurrentQState
        {
            get
            {
                return new QState<decimal>(new decimal[] { CurrentRow, CurrentColumn });
            }
        }
        public bool Done
        {
            get
            {
                return (CurrentColumn == GoalColumn && CurrentRow == GoalRow);
            }
        }

        public EnvMetrics Metrics { get; private set; }
    }
    public class HyperGridWorld : BaseGridWorld, IPvEEnv<QState<decimal>,ScalarReward>
    {
        public HyperGridWorld(int r, int c, QRandom ran) : base(r, c, ran)
        {
        }

        public virtual QState<decimal> Discretize()
        {
            return (CurrentQState);
        }
    }
    public class GridWorld : BaseGridWorld, IPvEEnv<decimal, ScalarReward>
    {

        public GridWorld(int r, int c, QRandom ran) : base(r, c, ran)
        {
        }


        public virtual decimal Discretize()
        {
            return (CurrentState);
        }

    }
}
