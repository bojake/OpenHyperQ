using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntTheWumpus
{
    public class BacktrackingMazeCave
    {
        private CaveItem[,] maze;
        private int rows, cols;
        private Random random = new Random();
        private Tuple<int, int>[] CardinalOffsets = {
            new Tuple<int,int>(0,-1), new Tuple<int,int>(-1,0), new Tuple<int,int>(0,1), new Tuple<int,int>(1,0)
        };
        private int[] wallFlags = { 1, 2, 4, 8 };

        public BacktrackingMazeCave(int rows, int cols)
        {
            this.rows = rows;
            this.cols = cols;
            maze = new CaveItem[rows,cols];
            InitializeMaze();
        }

        private void InitializeMaze()
        {
            for (int i = 0; i < maze.GetLength(0); i++)
            {
                for (int j = 0; j < maze.GetLength(1); j++)
                {
                    maze[i, j] = CaveItem.Wall; // Fill with walls
                }
            }
        }

        public BacktrackingMazeCave Generate()
        {
            CarveMaze(1+random.Next(cols-2), 1+random.Next(rows-2));
            return this;
        }

        private void CarveMaze(int x, int y)
        {
            List<Tuple<int, int>> work = new List<Tuple<int, int>>();
            int[] directions = { 0, 1, 2, 3 };
            work.Add(new Tuple<int, int>(y, x));
            int maxiters = (rows - 1) * (cols - 1);
            while (work.Count > 0 && maxiters > 0)
            {
                if (maze[y, x] == CaveItem.Wall)
                {
                    Shuffle(directions);
                    maze[y, x] = CaveItem.Nothing; //  wallFlags[direction]; // Remove wall in the current cell
                    int num = random.Next(directions.Length);
                    foreach (int direction in directions)
                    {
                        int nx = x + CardinalOffsets[direction].Item2;
                        int ny = y + CardinalOffsets[direction].Item1;
                        if (nx > 0 && ny > 0 && ny < rows - 1 && nx < cols - 1 && maze[ny, nx] == CaveItem.Wall)
                        {
                            work.Add(new Tuple<int, int>(ny, nx));
                            num--;
                        }
                        if (num == 0)
                        {
                            break;
                        }
                    }
                }
                Tuple<int, int> w = work[0];
                work.RemoveAt(0);
                y = w.Item2;
                x = w.Item1;
                maxiters--;
            }
        }
        private void Shuffle(int[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]); // Swap
            }
        }

        public void PrintMaze()
        {
            for (int i = 0; i < maze.GetLength(0); i++)
            {
                for (int j = 0; j < maze.GetLength(1); j++)
                {
                    Console.Write(maze[i, j] == CaveItem.Wall ? "#" : " ");
                }
                Console.WriteLine();
            }
        }

        public CaveItem[,] GetMaze()
        {
            return maze;
        }
    }
}
