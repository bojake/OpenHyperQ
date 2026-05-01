using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntTheWumpus
{
    public class KruskalMazeCave
    {
        private CaveItem[,] maze;
        private int rows, cols;
        private Random random = new Random();
        private Dictionary<(int, int), (int, int)> parent;

        public KruskalMazeCave(int rows, int cols)
        {
            this.rows = rows;
            this.cols = cols;
            maze = new CaveItem[rows * 2 + 1, cols * 2 + 1];
            InitializeMaze();
            InitializeDisjointSet();
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

        private void InitializeDisjointSet()
        {
            parent = new Dictionary<(int, int), (int, int)>();
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    parent[(i, j)] = (i, j); // Initially, each cell is its own parent
                }
            }
        }

        private (int, int) Find((int, int) cell)
        {
            if (parent[cell] != cell)
            {
                parent[cell] = Find(parent[cell]); // Path compression
            }
            return parent[cell];
        }

        private void Union((int, int) cell1, (int, int) cell2)
        {
            var root1 = Find(cell1);
            var root2 = Find(cell2);
            if (root1 != root2)
            {
                parent[root2] = root1;
            }
        }

        public KruskalMazeCave Generate()
        {
            List<((int, int), (int, int))> walls = new List<((int, int), (int, int))>();

            // Initialize all walls
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (i < rows - 1) walls.Add(((i, j), (i + 1, j))); // Vertical walls
                    if (j < cols - 1) walls.Add(((i, j), (i, j + 1))); // Horizontal walls
                }
            }

            // Shuffle walls randomly
            Shuffle(walls);

            foreach (var wall in walls)
            {
                var cell1 = wall.Item1;
                var cell2 = wall.Item2;

                if (Find(cell1) != Find(cell2))
                {
                    Union(cell1, cell2);
                    RemoveWall(cell1, cell2);
                }
            }
            return this;
        }

        private void RemoveWall((int, int) cell1, (int, int) cell2)
        {
            // Convert cell coordinates to maze grid coordinates
            int x1 = cell1.Item1 * 2 + 1, y1 = cell1.Item2 * 2 + 1;
            int x2 = cell2.Item1 * 2 + 1, y2 = cell2.Item2 * 2 + 1;

            // Remove wall
            maze[x1, y1] = CaveItem.Nothing;
            maze[x2, y2] = CaveItem.Nothing;
            maze[(x1 + x2) / 2, (y1 + y2) / 2] = CaveItem.Nothing;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]); // Swap
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
