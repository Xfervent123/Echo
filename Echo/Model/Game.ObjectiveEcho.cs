using System;
using System.Collections.Generic;
using System.Drawing;

namespace Echo
{
    public partial class Game
    {
        private void UpdateObjectiveEchoes(float dt)
        {
            var finishRegions = GetFinishRegions();
            var keyRegions = GetKeyRegions();
            var dangerRegions = GetDangerRegions();
            var dangerRegionWeights = BuildDangerRegionWeights(dangerRegions);
            var finishTargetCount = IsExitUnlocked() ? Maze.ExitCells.Count * FinishEchoRayCount : 0;
            var keyTargetCount = remainingKeyCells.Count * KeyEchoRayCount;
            var dangerTargetCount = Maze.DangerCells.Count * DangerEchoRayCount;

            UpdateObjectiveEchoRaySet(FinishEchoRays, finishRegions, finishTargetCount, dt);
            UpdateObjectiveEchoRaySet(KeyEchoRays, keyRegions, keyTargetCount, dt);
            UpdateObjectiveEchoRaySet(DangerEchoRays, dangerRegions, dangerTargetCount, dt, dangerRegionWeights);
        }

        private void EnsureObjectiveEchoRayCounts()
        {
            var finishRegions = GetFinishRegions();
            var keyRegions = GetKeyRegions();
            var dangerRegions = GetDangerRegions();
            var dangerRegionWeights = BuildDangerRegionWeights(dangerRegions);
            var finishTargetCount = IsExitUnlocked() ? Maze.ExitCells.Count * FinishEchoRayCount : 0;
            var keyTargetCount = remainingKeyCells.Count * KeyEchoRayCount;
            var dangerTargetCount = Maze.DangerCells.Count * DangerEchoRayCount;

            UpdateObjectiveEchoRaySet(FinishEchoRays, finishRegions, finishTargetCount, 0f);
            UpdateObjectiveEchoRaySet(KeyEchoRays, keyRegions, keyTargetCount, 0f);
            UpdateObjectiveEchoRaySet(DangerEchoRays, dangerRegions, dangerTargetCount, 0f, dangerRegionWeights);
        }

        private void UpdateObjectiveEchoRaySet(
            List<Ray> rays,
            List<RectangleF> regions,
            int targetCount,
            float dt,
            List<int>? regionWeights = null)
        {
            if (regions.Count == 0)
            {
                rays.Clear();
                return;
            }

            for (var i = rays.Count - 1; i >= 0; i--)
            {
                var ray = rays[i];
                ray.Update(dt);
                if (!ray.IsAlive)
                {
                    rays.RemoveAt(i);
                }
            }

            while (rays.Count < targetCount)
            {
                var regionIndex = PickObjectiveRegionIndex(regions.Count, regionWeights);
                rays.Add(CreateObjectiveEchoRay(regions[regionIndex]));
            }
        }

        private int PickObjectiveRegionIndex(int regionCount, List<int>? regionWeights)
        {
            if (regionCount <= 1 || regionWeights == null || regionWeights.Count != regionCount)
            {
                return objectiveEchoRandom.Next(regionCount);
            }

            var totalWeight = 0;
            for (var i = 0; i < regionWeights.Count; i++)
            {
                var weight = regionWeights[i];
                if (weight > 0) totalWeight += weight;
            }

            if (totalWeight <= 0) return objectiveEchoRandom.Next(regionCount);

            var pick = objectiveEchoRandom.Next(totalWeight);
            for (var i = 0; i < regionWeights.Count; i++)
            {
                var weight = regionWeights[i];
                if (weight <= 0) continue;
                if (pick < weight) return i;
                pick -= weight;
            }

            return regionCount - 1;
        }

        private Ray CreateObjectiveEchoRay(RectangleF bounds)
        {
            var stopOnBoundsHit = false;
            var echoBounds = GetObjectiveEchoBounds(bounds, out stopOnBoundsHit);
            var angle = (float)(objectiveEchoRandom.NextDouble() * Math.PI * 2.0);
            var dir = new Vec2(MathF.Cos(angle), MathF.Sin(angle));
            var x = bounds.Left + (float)objectiveEchoRandom.NextDouble() * bounds.Width;
            var y = bounds.Top + (float)objectiveEchoRandom.NextDouble() * bounds.Height;
            var origin = new Vec2(x, y);

            var speed = ObjectiveEchoSpeedMin + (float)objectiveEchoRandom.NextDouble() * (ObjectiveEchoSpeedMax - ObjectiveEchoSpeedMin);
            var maxDistance = ObjectiveEchoDistanceMin + (float)objectiveEchoRandom.NextDouble() * (ObjectiveEchoDistanceMax - ObjectiveEchoDistanceMin);

            return Ray.CreateInBounds(
                origin,
                dir,
                echoBounds,
                maxBounces: ObjectiveEchoMaxBounces,
                maxDist: maxDistance,
                speed: speed,
                stopOnBoundsHit: stopOnBoundsHit
            );
        }

        private RectangleF GetObjectiveEchoBounds(RectangleF bounds, out bool stopOnBoundsHit)
        {
            stopOnBoundsHit = false;
            if (objectiveEchoRandom.NextDouble() >= ObjectiveEchoOverflowChance) return bounds;

            var maxOverflowDistance = Maze.TileSize * ObjectiveEchoOverflowMaxTileRatio;
            var overflowDistance = (float)objectiveEchoRandom.NextDouble() * maxOverflowDistance;
            if (overflowDistance <= 0f) return bounds;

            stopOnBoundsHit = true;
            return RectangleF.FromLTRB(
                bounds.Left - overflowDistance,
                bounds.Top - overflowDistance,
                bounds.Right + overflowDistance,
                bounds.Bottom + overflowDistance);
        }

        private List<RectangleF> GetFinishRegions()
        {
            if (!IsExitUnlocked()) return EmptyRegions;
            return Maze.ExitRegions;
        }

        private List<RectangleF> GetKeyRegions()
        {
            return remainingKeyRegions;
        }

        private List<RectangleF> GetDangerRegions()
        {
            return Maze.DangerRegions;
        }

        private List<int> BuildDangerRegionWeights(List<RectangleF> dangerRegions)
        {
            var weights = new List<int>(dangerRegions.Count);
            if (dangerRegions.Count == 0) return weights;

            for (var i = 0; i < dangerRegions.Count; i++)
            {
                var region = dangerRegions[i];
                var count = 0;

                for (var cellIndex = 0; cellIndex < Maze.DangerCells.Count; cellIndex++)
                {
                    var cell = Maze.DangerCells[cellIndex];
                    var centerX = (cell.X * Maze.TileSize) + (Maze.TileSize * 0.5f);
                    var centerY = (cell.Y * Maze.TileSize) + (Maze.TileSize * 0.5f);
                    if (region.Contains(centerX, centerY))
                    {
                        count++;
                    }
                }

                weights.Add(Math.Max(1, count));
            }

            return weights;
        }

        private List<RectangleF> BuildObjectiveRegions(List<Point> cells)
        {
            var regions = new List<RectangleF>();
            if (cells.Count == 0) return regions;

            var objectiveCells = new HashSet<Point>(cells);
            var visited = new HashSet<Point>();
            var queue = new Queue<Point>();

            for (var i = 0; i < cells.Count; i++)
            {
                var start = cells[i];
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

                    EnqueueObjectiveNeighbor(new Point(current.X - 1, current.Y), objectiveCells, visited, queue);
                    EnqueueObjectiveNeighbor(new Point(current.X + 1, current.Y), objectiveCells, visited, queue);
                    EnqueueObjectiveNeighbor(new Point(current.X, current.Y - 1), objectiveCells, visited, queue);
                    EnqueueObjectiveNeighbor(new Point(current.X, current.Y + 1), objectiveCells, visited, queue);
                }

                var leftPx = minX * Maze.TileSize;
                var topPx = minY * Maze.TileSize;
                var widthPx = (maxX - minX + 1) * Maze.TileSize;
                var heightPx = (maxY - minY + 1) * Maze.TileSize;
                regions.Add(new RectangleF(leftPx, topPx, widthPx, heightPx));
            }

            return regions;
        }

        private void EnqueueObjectiveNeighbor(
            Point cell,
            HashSet<Point> objectiveCells,
            HashSet<Point> visited,
            Queue<Point> queue)
        {
            if (cell.X < 0 || cell.X >= Maze.Width || cell.Y < 0 || cell.Y >= Maze.Height) return;
            if (!objectiveCells.Contains(cell)) return;
            if (visited.Contains(cell)) return;

            visited.Add(cell);
            queue.Enqueue(cell);
        }
    }
}
