using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Echo
{
    public class HitInfo
    {
        public Vec2 Point;
        public Vec2 Normal;

        public HitInfo(Vec2 point, Vec2 normal)
        {
            Point = point;
            Normal = normal;
        }
    }

    public class Maze
    {
        public int TileSize;
        public int Width;
        public int Height;

        private bool[,] walls;
        private Point playerSpawnCell;

        public List<Point> EnemySpawnCells;
        public List<Point> KeyCells;
        public List<Point> ExitCells;
        public List<Point> DangerCells;
        public List<Point> PatrolCells;
        public List<RectangleF> KeyRegions;
        public List<RectangleF> ExitRegions;
        public List<RectangleF> DangerRegions;
        public List<RectangleF> PatrolRegions;
        public Point KeyCell;
        public bool HasKeyCell;
        public Point ExitCell;
        public bool HasExitCell;

        public Maze(string mapFilePath)
        {
            TileSize = 64;
            Width = 1;
            Height = 1;
            walls = new bool[1, 1];
            playerSpawnCell = new Point(1, 1);
            EnemySpawnCells = new List<Point>();
            KeyCells = new List<Point>();
            ExitCells = new List<Point>();
            DangerCells = new List<Point>();
            PatrolCells = new List<Point>();
            KeyRegions = new List<RectangleF>();
            ExitRegions = new List<RectangleF>();
            DangerRegions = new List<RectangleF>();
            PatrolRegions = new List<RectangleF>();
            LoadFromTextFile(mapFilePath);
        }

        private void LoadFromTextFile(string mapFilePath)
        {
            var lines = File.ReadAllLines(mapFilePath);
            var mapWidth = lines[0].Length;

            Width = mapWidth;
            Height = lines.Length;
            walls = new bool[Width, Height];
            EnemySpawnCells.Clear();
            KeyCells.Clear();
            ExitCells.Clear();
            DangerCells.Clear();
            PatrolCells.Clear();
            HasKeyCell = false;
            HasExitCell = false;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var token = char.ToUpperInvariant(lines[y][x]);

                    if (token == 'X')
                    {
                        walls[x, y] = true;
                    }
                    else
                    {
                        walls[x, y] = false;

                        if (token == 'P')
                        {
                            playerSpawnCell = new Point(x, y);
                        }

                        if (token == 'E')
                        {
                            EnemySpawnCells.Add(new Point(x, y));
                        }
                        else if (token == 'K')
                        {
                            KeyCells.Add(new Point(x, y));
                        }
                        else if (token == 'O')
                        {
                            ExitCells.Add(new Point(x, y));
                        }
                        else if (token == 'R')
                        {
                            DangerCells.Add(new Point(x, y));
                        }
                        else if (token == 'T')
                        {
                            PatrolCells.Add(new Point(x, y));
                        }
                    }
                }
            }

            if (KeyCells.Count > 0)
            {
                KeyCell = KeyCells[0];
                HasKeyCell = true;
            }

            if (ExitCells.Count > 0)
            {
                ExitCell = ExitCells[0];
                HasExitCell = true;
            }

            KeyRegions = BuildObjectiveRegions(KeyCells);
            ExitRegions = BuildObjectiveRegions(ExitCells);
            DangerRegions = BuildObjectiveRegions(DangerCells);
            PatrolRegions = BuildObjectiveRegions(PatrolCells);
        }

        private List<RectangleF> BuildCellRegions(List<Point> cells)
        {
            var regions = new List<RectangleF>(cells.Count);
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var leftPx = cell.X * TileSize;
                var topPx = cell.Y * TileSize;
                regions.Add(new RectangleF(leftPx, topPx, TileSize, TileSize));
            }

            return regions;
        }

        private List<RectangleF> BuildObjectiveRegions(List<Point> cells)
        {
            var regions = new List<RectangleF>();
            if (cells.Count == 0) return regions;

            var objectiveCells = new HashSet<Point>(cells);
            var visited = new HashSet<Point>();
            var queue = new Queue<Point>();

            foreach (var start in cells)
            {
                if (visited.Contains(start)) continue;

                visited.Add(start);
                queue.Enqueue(start);

                var minX = start.X;
                var maxX = start.X;
                var minY = start.Y;
                var maxY = start.Y;

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    if (current.X < minX) minX = current.X;
                    if (current.X > maxX) maxX = current.X;
                    if (current.Y < minY) minY = current.Y;
                    if (current.Y > maxY) maxY = current.Y;

                    var left = new Point(current.X - 1, current.Y);
                    var right = new Point(current.X + 1, current.Y);
                    var up = new Point(current.X, current.Y - 1);
                    var down = new Point(current.X, current.Y + 1);

                    TryEnqueueNeighbor(left, objectiveCells, visited, queue);
                    TryEnqueueNeighbor(right, objectiveCells, visited, queue);
                    TryEnqueueNeighbor(up, objectiveCells, visited, queue);
                    TryEnqueueNeighbor(down, objectiveCells, visited, queue);
                }

                var leftPx = minX * TileSize;
                var topPx = minY * TileSize;
                var widthPx = (maxX - minX + 1) * TileSize;
                var heightPx = (maxY - minY + 1) * TileSize;
                regions.Add(new RectangleF(leftPx, topPx, widthPx, heightPx));
            }

            return regions;
        }

        private void TryEnqueueNeighbor(
            Point cell,
            HashSet<Point> objectiveCells,
            HashSet<Point> visited,
            Queue<Point> queue)
        {
            if (cell.X < 0 || cell.X >= Width || cell.Y < 0 || cell.Y >= Height) return;
            if (!objectiveCells.Contains(cell)) return;
            if (visited.Contains(cell)) return;

            visited.Add(cell);
            queue.Enqueue(cell);
        }

        // Проверяет сетку: является ли клетка [x, y] стеной?
        // (Также возвращает true, если координаты вышли за пределы карты)
        public bool IsWall(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return true;
            }
            return walls[x, y];
        }

        // Конвертирует пиксели в координаты сетки и вызывает IsWall.
        public bool IsWallAtPixel(float px, float py)
        {
            var coorXGrid = px / TileSize;
            var coorYGrid = py / TileSize;
            return IsWall((int)coorXGrid, (int)coorYGrid);
        }

        // Возвращает центр клетки спавна игрока (P) в пикселях
        public Vec2 GetSpawnPoint()
        {
            float centerX = (playerSpawnCell.X * TileSize) + (TileSize / 2f);
            float centerY = (playerSpawnCell.Y * TileSize) + (TileSize / 2f);
            return new Vec2(centerX, centerY);
        }

        // Шагает по сетке вдоль вектора направления (direction) на длину maxDist.
        // Как только натыкается на стену (IsWall) — возвращает точку удара и нормаль.
        // Если пролетел maxDist и ничего не задел — возвращает null.
        public HitInfo Raycast(Vec2 origin, Vec2 direction, float maxDist)
        {
            var dir = direction.Normalized();
            if (dir.Length == 0) return null;

            // Текущие координаты в логической сетке
            var mapX = (int)(origin.X / TileSize);
            var mapY = (int)(origin.Y / TileSize);

            // Длина луча, необходимая для пересечения ровно одной клетки по осям
            var deltaDistX = (dir.X == 0) ? float.MaxValue : (float)Math.Abs(TileSize / dir.X);
            var deltaDistY = (dir.Y == 0) ? float.MaxValue : (float)Math.Abs(TileSize / dir.Y);

            // Расстояние от старта луча до ближайшей границы клетки
            var sideDistX = 0f;
            var sideDistY = 0f;

            // Направление обхода сетки
            var stepX = 0;
            var stepY = 0;

            // Инициализация стартовых смещений
            if (dir.X < 0)
            {
                stepX = -1;
                sideDistX = (origin.X - (mapX * TileSize)) * Math.Abs(1f / dir.X);
            }
            else
            {
                stepX = 1;
                sideDistX = (((mapX + 1) * TileSize) - origin.X) * Math.Abs(1f / dir.X);
            }

            if (dir.Y < 0)
            {
                stepY = -1;
                sideDistY = (origin.Y - (mapY * TileSize)) * Math.Abs(1f / dir.Y);
            }
            else
            {
                stepY = 1;
                sideDistY = (((mapY + 1) * TileSize) - origin.Y) * Math.Abs(1f / dir.Y);
            }

            var hit = false;
            var side = 0; // 0 = пересечение по оси X (вертикальная стена), 1 = по оси Y (горизонтальная)
            var currentDist = 0f;

            // Основной цикл DDA
            while (currentDist <= maxDist)
            {
                // Прыжок к ближайшей границе следующей клетки
                if (sideDistX < sideDistY)
                {
                    sideDistX += deltaDistX;
                    mapX += stepX;
                    side = 0;
                }
                else
                {
                    sideDistY += deltaDistY;
                    mapY += stepY;
                    side = 1;
                }

                currentDist = (side == 0) ? (sideDistX - deltaDistX) : (sideDistY - deltaDistY);

                if (currentDist > maxDist) break;

                if (IsWall(mapX, mapY))
                {
                    hit = true;
                    break;
                }
            }

            if (!hit) return null;

            // Вычисление точной точки пересечения и нормали поверхности для отскока
            var hitPoint = new Vec2(
                origin.X + dir.X * currentDist,
                origin.Y + dir.Y * currentDist
            );

            var normal = side == 0
                ? new Vec2(-stepX, 0)
                : new Vec2(0, -stepY);

            return new HitInfo(hitPoint, normal);
        }
    }
}
