using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntTheWumpus
{
    public enum CaveItem : int
    {
        Wall = 1,
        Nothing = 0,
        Wumpus = 2,
        Pit = 3,
        Treasure = 4,
        Exit = 5,
        Food = 6
    }
    public class WumpusCave
    {
        private QRandom _ran;
        public int Rows { get; private set; } = 10;
        public int Columns { get; private set; } = 10;
        private CaveItem[,] CaveStatus;
        public Tuple<int, int>[] AreaOffsets = {
            new Tuple<int,int>(0,-1), new Tuple<int,int>(-1,0), new Tuple<int,int>(0,1), new Tuple<int,int>(1,0),
            new Tuple<int,int>(-1,-1), new Tuple<int,int>(-1,1), new Tuple<int,int>(1,1), new Tuple<int,int>(1,-1)
        };
        private Tuple<int, int>[] CardinalOffsets = {
            new Tuple<int,int>(0,-1), new Tuple<int,int>(-1,0), new Tuple<int,int>(0,1), new Tuple<int,int>(1,0)
        };
        private int NearFactor = 2;
        /// <summary>
        /// Location of the treasure in (row,column)
        /// </summary>
        public Tuple<int, int> TreasureLocation { get; private set; }
        /// <summary>
        /// Location of the Wumpus in (row,column)
        /// </summary>
        public Tuple<int, int> WumpusLocation { get; private set; }

        public WumpusCave(int rows, int columns, QRandom ran)
        {
            _ran = ran;
            Rows = rows; 
            Columns = columns;
            CaveStatus = new CaveItem[Rows, Columns];
            // Randomly place the wumpus and its gold
            if (Rows > 25 && columns > 25)
                MazePlan();
            else
                DefaultStudioPlan();
            // Place the Wumpus
            Tuple<int, int> p = RandomStartLocation();
            CaveStatus[p.Item1, p.Item2] = CaveItem.Wumpus;
            WumpusLocation = p;
            int wr = p.Item1;
            int wc = p.Item2;
            // Place its treasure
            int tr = 0;
            int tc = 0;
            for (int i = 0; i < AreaOffsets.Length; i++)
            {
                tr = wr + AreaOffsets[i].Item1;
                tc = wc + AreaOffsets[i].Item2;
                if (CaveStatus[tr, tc] == CaveItem.Nothing)
                {
                    // Check distance, Wumpus likes to be near its treasure
                    CaveStatus[tr, tc] = CaveItem.Treasure;
                    TreasureLocation = new Tuple<int, int>(tr, tc);
                    break;
                }
            }
            // Place a pit using the random start location algorithm
            p = RandomStartLocation();
            CaveStatus[p.Item1, p.Item2] = CaveItem.Pit;
            // Place the exit
            p = RandomStartLocation();
            CaveStatus[p.Item1, p.Item2] = CaveItem.Exit;
            // Place food
            p = RandomStartLocation();
            CaveStatus[p.Item1, p.Item2] = CaveItem.Food;
        }

        private void MazePlan()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    CaveStatus[r, c] = CaveItem.Wall;
                }
            }
            CaveStatus = new BacktrackingMazeCave(Rows, Columns).Generate().GetMaze();
            // CarveMaze(Columns/2, Rows/2);
        }

        private void DefaultStudioPlan()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    CaveStatus[r, c] = CaveItem.Nothing;
                    // Place the walls
                    if (r == 0 || r == Rows - 1)
                        CaveStatus[r, c] = CaveItem.Wall;
                    else if (c == 0 || c == Columns - 1)
                        CaveStatus[r, c] = CaveItem.Wall;
                }
            }
        }

        /// <summary>
        /// Finds a start location that is more than NearFactor from the wumpus.
        /// </summary>
        /// <returns></returns>
        public Tuple<int, int> RandomStartLocation()
        {
            int tr = 0;
            int tc = 0;
            int wr = 0;
            int wc = 0;
            if (WumpusLocation != null)
            {
                wr = WumpusLocation.Item1;
                wc = WumpusLocation.Item2;
            }
            int iters = 100;
            do
            {
                tr = _ran.Ran.Next(1,Rows-2); // The wall is always the perimeter
                tc = _ran.Ran.Next(1,Columns-2);
                if (CaveStatus[tr, tc] == CaveItem.Nothing)
                {
                    if (WumpusLocation == null)
                    {
                        return new Tuple<int, int>(tr, tc);
                    }
                    int dr = tr - wr < 0 ? wr - tr : tr - wr;
                    int dc = tc - wc < 0 ? wc - tr : tc - wc;
                    // Check distance, Wumpus likes to be near its food and its treasure
                    if (dr >= NearFactor && dc >= NearFactor)
                    {
                        return new Tuple<int, int>(tr, tc);
                    }
                }
                iters--;
            } while (iters > 0);
            return new Tuple<int, int>(tr, tc);
        }

        public CaveItem this[int row, int col]
        {
            get
            {
                return (CaveStatus[row, col]);
            }
            private set
            {
                CaveStatus[row, col] = value;
            }
        }

        /// <summary>
        /// eat some food, which gives back 20 health
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public int EatFood(int row, int col)
        {
            if (this[row, col] == CaveItem.Food)
            {
                this[row, col] = CaveItem.Nothing;
                return 20;
            }
            return 0;
        }
        /// <summary>
        /// get one gold
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public int TakeGold(int row, int col)
        {
            if (this[row, col] == CaveItem.Treasure)
            {
                this[row, col] = CaveItem.Nothing;
                TreasureLocation = null;
                return 1;
            }
            return 0;
        }

        public Tuple<int,int> MoveWumpus()
        {
            int tr = 0;
            int tc = 0;
            int iterations = 0;
            do
            {
                int ix = _ran.Ran.Next(AreaOffsets.Length);
                tr = AreaOffsets[ix].Item1;
                tc = AreaOffsets[ix].Item2;
                int wr = WumpusLocation.Item1 + tr;
                int wc = WumpusLocation.Item2 + tc;
                if(wr < 0 || wr >= Rows)
                    continue;
                if (wc < 0 || wc >= Columns)
                    continue;
                if (CaveStatus[wr, wc] == CaveItem.Nothing)
                {
                    // Check distance, Wumpus likes to be near its treasure
                    if ((TreasureLocation.Item1 > wr && TreasureLocation.Item1 - wr <= NearFactor) || (wr > TreasureLocation.Item1 && wr - TreasureLocation.Item1 <= NearFactor))
                    {
                        if ((TreasureLocation.Item2 > wc && TreasureLocation.Item2 - wc <= NearFactor) || (wc > TreasureLocation.Item2 && wc - TreasureLocation.Item2 <= NearFactor))
                        {
                            CaveStatus[WumpusLocation.Item1, WumpusLocation.Item2] = CaveItem.Nothing;
                            CaveStatus[wr, wc] = CaveItem.Wumpus;
                            WumpusLocation = new Tuple<int, int>(wr, wc);
                            break;
                        }
                    }
                }
                // Max of 10 tries to move the wumpus
            } while (++iterations < 10);
            return WumpusLocation;
        }

        public bool CanMoveInto(int row, int col)
        {
            if (row < 0 || row >= Rows)
                return false;
            if (col < 0 || col >= Columns)
                return false;
            if (CaveStatus[row, col] == CaveItem.Wall)
                return false;
            return true;
        }

        public CaveItem LocationStatus(int row, int col)
        {
            return CaveStatus[row, col];
        }

        public CaveItem[] WhatDoISee(int row, int col)
        {
            CaveItem[] area = new CaveItem[8];
            for (int i = 0; i < AreaOffsets.Length; i++)
            {
                area[i] = CaveItem.Wall;
                int ir = row + AreaOffsets[i].Item1;
                int ic = col + AreaOffsets[i].Item2;
                if (ir >= 0 && ir < Rows && ic >= 0 && ic < Columns)
                {
                    area[i] = CaveStatus[ir, ic];
                }
            }
            return (area);
        }

        private char MapChar(CaveItem item)
        {
            switch (item)
            {
                case CaveItem.Nothing:
                    return ('.');
                case CaveItem.Wall:
                    return ('#');
                case CaveItem.Pit:
                    return ('O');
                case CaveItem.Treasure:
                    return ('$');
                case CaveItem.Wumpus:
                    return ('W');
                case CaveItem.Exit:
                    return ('=');
                case CaveItem.Food:
                    return ('+');
            }
            return (' ');
        }

        public string DrawBoard(Tuple<int,int> player, bool drawBorder = false)
        {
            CaveItem[] seen = WhatDoISee(player.Item1, player.Item2);
            StringBuilder sb = new StringBuilder();
            if (drawBorder)
            {
                sb.Append('|');
                for (int c = 0; c < Columns + 2; c++)
                {
                    sb.Append('#');
                }
                sb.Append('\n');
            }
            for (int r = 0; r < Rows; r++)
            {
                sb.Append('|');
                if (drawBorder)
                    sb.Append('#');
                for (int c = 0; c < Columns; c++)
                {
                    if (c == player.Item2 && r == player.Item1)
                    {
                        sb.Append('@');
                    }
                    else
                        sb.Append(MapChar(CaveStatus[r, c]));
                }
                if(drawBorder)
                    sb.Append('#');
                sb.Append(' ');
                sb.Append('|');
                // Player visibility board
                if (r >= player.Item1 - 1 && r <= player.Item1 + 1)
                {
                    for (int c = player.Item2 - 1; c <= player.Item2 + 1; c++)
                    {
                        if (c == player.Item2 && r == player.Item1)
                            sb.Append('@');
                        else
                            for (int i = 0; i < AreaOffsets.Length; i++)
                            {
                                int rr = player.Item1 + AreaOffsets[i].Item1;
                                int cc = player.Item2 + AreaOffsets[i].Item2;
                                if (rr == r && cc == c)
                                {
                                    sb.Append(MapChar(CaveStatus[rr, cc]));
                                    break;
                                }
                            }
                    }
                }
                sb.Append('\n');
            }
            if (drawBorder)
            {
                sb.Append('|');
                for (int c = 0; c < Columns + 2; c++)
                {
                    sb.Append('#');
                }
                sb.Append('\n');
            }
            sb.Append('|');
            for (int i = 0; i < AreaOffsets.Length; i++)
            {
                int rr = player.Item1 + AreaOffsets[i].Item1;
                int cc = player.Item2 + AreaOffsets[i].Item2;
                sb.Append(MapChar(CaveStatus[rr, cc]));
            }
            sb.Append('\n');
            return (sb.ToString());
        }
    }
}
