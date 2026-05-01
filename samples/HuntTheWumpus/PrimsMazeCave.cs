using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntTheWumpus
{
    public class PrimsMazeCave
    {
        private CaveItem[,] maze;
        private int rows, cols;
        private Random random = new Random();

        public PrimsMazeCave(int rows, int cols)
        {
            this.rows = rows;
            this.cols = cols;
            maze = new CaveItem[rows * 2 + 1, cols * 2 + 1];
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

        public PrimsMazeCave Generate()
        {
            HashSet<(int, int)> visited = new HashSet<(int, int)>();
            List<((int, int), (int, int))> walls = new List<((int, int), (int, int))>();

            // Start from a random cell
            (int x, int y) startCell = (random.Next(rows), random.Next(cols));
            visited.Add(startCell);
            AddWalls(startCell, walls);

            while (walls.Count > 0)
            {
                // Choose a random wall
                var wallIndex = random.Next(walls.Count);
                var wall = walls[wallIndex];
                walls.RemoveAt(wallIndex);

                var (cell1, cell2) = wall;

                if (visited.Contains(cell1) && visited.Contains(cell2))
                {
                    continue; // Both cells are already in the maze
                }

                // Add the new cell to the maze
                var newCell = visited.Contains(cell1) ? cell2 : cell1;
                visited.Add(newCell);
                RemoveWall(cell1, cell2);
                AddWalls(newCell, walls);
            }
            return this;
        }

        private void AddWalls((int, int) cell, List<((int, int), (int, int))> walls)
        {
            var (x, y) = cell;
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < rows && ny < cols && maze[2 * nx + 1, 2 * ny + 1] == CaveItem.Wall)
                {
                    walls.Add(((x, y), (nx, ny)));
                }
            }
        }

        private void RemoveWall((int, int) cell1, (int, int) cell2)
        {
            // Convert cell coordinates to maze grid coordinates
            int x1 = cell1.Item1 * 2 + 1, y1 = cell1.Item2 * 2 + 1;
            int x2 = cell2.Item1 * 2 + 1, y2 = cell2.Item2 * 2 + 1;

            // Remove the wall
            maze[(x1 + x2) / 2, (y1 + y2) / 2] = CaveItem.Nothing;
            maze[x1, y1] = CaveItem.Nothing;
            maze[x2, y2] = CaveItem.Nothing;
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
