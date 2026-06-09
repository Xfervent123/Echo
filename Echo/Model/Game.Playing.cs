using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Echo
{
    public partial class Game
    {
        private bool IsPlayerInDangerZone()
        {
            for (var i = 0; i < Maze.DangerCells.Count; i++)
            {
                var cell = Maze.DangerCells[i];
                var left = cell.X * Maze.TileSize;
                var right = left + Maze.TileSize;
                var top = cell.Y * Maze.TileSize;
                var bottom = top + Maze.TileSize;
                if (IsCircleTouchingRect(Player.Position, Player.Radius, left, top, right, bottom))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryCollectTouchedKeys()
        {
            var collectedAny = false;
            for (var i = remainingKeyCells.Count - 1; i >= 0; i--)
            {
                if (!IsPlayerTouchingCell(remainingKeyCells[i])) continue;
                remainingKeyCells.RemoveAt(i);
                collectedAny = true;
            }

            if (!collectedAny) return false;

            Sound.PlayKeyFound();
            remainingKeyRegions = BuildObjectiveRegions(remainingKeyCells);
            EnsureObjectiveEchoRayCounts();
            return true;
        }

        private bool TryGoToNextMapThroughExit()
        {
            if (!IsExitUnlocked()) return false;

            for (var i = 0; i < Maze.ExitCells.Count; i++)
            {
                if (!IsPlayerTouchingCell(Maze.ExitCells[i])) continue;
                GoToNextMap();
                return true;
            }

            return false;
        }

        private bool IsExitUnlocked()
        {
            return remainingKeyCells.Count == 0;
        }

        private bool IsPlayerTouchingCell(Point cell)
        {
            var left = cell.X * Maze.TileSize;
            var right = left + Maze.TileSize;
            var top = cell.Y * Maze.TileSize;
            var bottom = top + Maze.TileSize;
            return IsCircleTouchingRect(Player.Position, Player.Radius, left, top, right, bottom);
        }

        private bool IsCircleTouchingRect(Vec2 center, float radius, float left, float top, float right, float bottom)
        {
            var closestX = center.X;
            if (closestX < left) closestX = left;
            else if (closestX > right) closestX = right;

            var closestY = center.Y;
            if (closestY < top) closestY = top;
            else if (closestY > bottom) closestY = bottom;

            var distanceX = center.X - closestX;
            var distanceY = center.Y - closestY;
            var distanceSquared = (distanceX * distanceX) + (distanceY * distanceY);
            return distanceSquared <= (radius * radius);
        }

        private string BuildMapPath(int mapNumber)
        {
            return Path.Combine(mapsFolderPath, mapNumber + ".txt");
        }

        private void GoToNextMap()
        {
            var nextMapNumber = currentMapNumber + 1;
            if (!UnlockAllLevelsWithoutProgress && nextMapNumber > maxUnlockedLevel)
            {
                maxUnlockedLevel = nextMapNumber;
            }

            var nextMapPath = BuildMapPath(nextMapNumber);
            if (File.Exists(nextMapPath))
            {
                currentMapNumber = nextMapNumber;
                currentMapPath = nextMapPath;
                RestartCurrentMap();
                StartLevelIntro();
                Sound.PlayEntering();
                return;
            }

            // Уровней больше нет — показываем финальную катсцену.
            PlayEndingCutscene();
        }

        private bool IsPlayerCaughtByEnemy()
        {
            var playerPos = Player.Position;
            var killRadius = Player.Radius + EnemyCollisionRadius;
            var killRadiusSquared = killRadius * killRadius;

            for (var i = 0; i < Enemies.Count; i++)
            {
                var enemyPos = Enemies[i].Position;
                var dx = playerPos.X - enemyPos.X;
                var dy = playerPos.Y - enemyPos.Y;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared <= killRadiusSquared)
                {
                    return true;
                }
            }

            var patrolKillRadius = Player.Radius + PatrolEnemyCollisionRadius;
            var patrolKillRadiusSquared = patrolKillRadius * patrolKillRadius;

            for (var i = 0; i < PatrolEnemies.Count; i++)
            {
                var enemyPos = PatrolEnemies[i].Position;
                var dx = playerPos.X - enemyPos.X;
                var dy = playerPos.Y - enemyPos.Y;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared <= patrolKillRadiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private void RestartCurrentMap()
        {
            Maze = new Maze(currentMapPath);
            Player = new Player(Maze.GetSpawnPoint());
            CursorDirection = new Vec2(1, 0);
            StepEchoTimer = StepEchoInterval;
            WasMoving = false;
            isDeathAnimationActive = false;
            deathAnimationTimer = DeathAnimationDuration;
            remainingKeyCells = new List<Point>(Maze.KeyCells);
            remainingKeyRegions = BuildObjectiveRegions(remainingKeyCells);

            ActiveRays.Clear();
            FinishEchoRays.Clear();
            KeyEchoRays.Clear();
            DangerEchoRays.Clear();
            Enemies.Clear();
            PatrolEnemies.Clear();

            for (var i = 0; i < Maze.EnemySpawnCells.Count; i++)
            {
                var cell = Maze.EnemySpawnCells[i];
                var center = new Vec2(
                    (cell.X * Maze.TileSize) + (Maze.TileSize / 2f),
                    (cell.Y * Maze.TileSize) + (Maze.TileSize / 2f));
                Enemies.Add(new Enemy(center));
            }

            for (var i = 0; i < Maze.PatrolRegions.Count; i++)
            {
                var area = Maze.PatrolRegions[i];
                var spawnPos = new Vec2(area.Left + (area.Width / 2f), area.Top + (area.Height / 2f));
                var seed = (currentMapNumber * 1000) + i;
                var patrol = new PatrolEnemy(spawnPos, area, seed);
                patrol.Initialize(Maze);
                PatrolEnemies.Add(patrol);
            }

            EnsureObjectiveEchoRayCounts();
            Sound.StopEnemySound();
        }

        private void StartLevelIntro()
        {
            levelIntroTimer = 0f;
            isLevelIntroAwaitingDismiss = true;
            // Туториал показывается, если для этого уровня он определён.
            tutorialTime = 0f;
            isTutorialAwaitingDismiss = HasTutorialForLevel(currentMapNumber);
        }

        private static bool HasTutorialForLevel(int levelNumber)
        {
            return (levelNumber >= 1 && levelNumber <= 4) || levelNumber == 10;
        }

        private void StartDeathAnimation()
        {
            if (isDeathAnimationActive) return;

            deathAnimationCenter = Player.Position;
            var dir = CursorDirection.Normalized();
            if (dir.Length <= 0.001f)
            {
                dir = new Vec2(1, 0);
            }

            deathAnimationDirection = dir;
            deathAnimationTimer = 0f;
            isDeathAnimationActive = true;
            Sound.StopEnemySound();
        }

        private int CountAvailableLevels()
        {
            if (!Directory.Exists(mapsFolderPath)) return 1;

            var mapFiles = Directory.GetFiles(mapsFolderPath, "*.txt");
            var validCount = 0;
            for (var i = 0; i < mapFiles.Length; i++)
            {
                var fileName = Path.GetFileNameWithoutExtension(mapFiles[i]);
                if (!int.TryParse(fileName, out var mapNumber)) continue;
                if (mapNumber < StartMapNumber) continue;
                validCount++;
            }

            return Math.Max(1, validCount);
        }

        private void UpdatePatrolEnemies(float dt)
        {
            for (var i = 0; i < PatrolEnemies.Count; i++)
            {
                var patrol = PatrolEnemies[i];
                Ray? touchingRay = null;
                var touchDistance = 0f;
                var bestDistanceSq = float.MaxValue;
                var bestScore = float.MaxValue;
                var searchRadius = patrol.IsEngaged ? EnemyRayReacquireRadius : EnemyRayFollowHitRadius;

                for (var rayIndex = 0; rayIndex < ActiveRays.Count; rayIndex++)
                {
                    var ray = ActiveRays[rayIndex];
                    if (!Enemy.TryGetTouchDistanceOnRevealedRay(ray, patrol.Position, searchRadius, out var rayDistance, out var distanceSq))
                    {
                        continue;
                    }

                    var pointOnRay = Enemy.GetPointOnRayDistance(ray, rayDistance);
                    var score = rayDistance;
                    score += Vec2.Distance(pointOnRay, Player.Position) * EnemyRayPlayerDistanceWeight;

                    var toPlayer = Player.Position - patrol.Position;
                    var towardRayStart = ray.Path[0] - pointOnRay;
                    if (toPlayer.Length > 0.001f && towardRayStart.Length > 0.001f)
                    {
                        var alignment = Vec2.Dot(toPlayer.Normalized(), towardRayStart.Normalized());
                        score -= alignment * EnemyRayAlignmentBonus;
                    }

                    if (score < bestScore
                        || (MathF.Abs(score - bestScore) < 0.001f && distanceSq < bestDistanceSq))
                    {
                        bestScore = score;
                        bestDistanceSq = distanceSq;
                        touchDistance = rayDistance;
                        touchingRay = ray;
                    }
                }

                if (touchingRay != null)
                {
                    patrol.FollowRay(touchingRay, touchDistance);
                }

                patrol.Update(
                    dt,
                    PatrolEnemySpeed,
                    PatrolEnemyChaseSpeed,
                    Maze,
                    PatrolEnemyCollisionRadius,
                    Player.Position);
            }
        }

        private void UpdateEnemyRayFollowing(float dt)
        {
            for (var enemyIndex = 0; enemyIndex < Enemies.Count; enemyIndex++)
            {
                var enemy = Enemies[enemyIndex];
                Ray? touchingRay = null;
                var touchDistance = 0f;
                var bestDistanceSq = float.MaxValue;
                var bestScore = float.MaxValue;
                var searchRadius = enemy.IsEngaged ? EnemyRayReacquireRadius : EnemyRayFollowHitRadius;

                for (var rayIndex = 0; rayIndex < ActiveRays.Count; rayIndex++)
                {
                    var ray = ActiveRays[rayIndex];
                    if (!Enemy.TryGetTouchDistanceOnRevealedRay(ray, enemy.Position, searchRadius, out var rayDistance, out var distanceSq))
                    {
                        continue;
                    }

                    var pointOnRay = Enemy.GetPointOnRayDistance(ray, rayDistance);
                    var score = rayDistance;
                    score += Vec2.Distance(pointOnRay, Player.Position) * EnemyRayPlayerDistanceWeight;

                    var toPlayer = Player.Position - enemy.Position;
                    var towardRayStart = ray.Path[0] - pointOnRay;
                    if (toPlayer.Length > 0.001f && towardRayStart.Length > 0.001f)
                    {
                        var alignment = Vec2.Dot(toPlayer.Normalized(), towardRayStart.Normalized());
                        score -= alignment * EnemyRayAlignmentBonus;
                    }

                    if (score < bestScore
                        || (MathF.Abs(score - bestScore) < 0.001f && distanceSq < bestDistanceSq))
                    {
                        bestScore = score;
                        bestDistanceSq = distanceSq;
                        touchDistance = rayDistance;
                        touchingRay = ray;
                    }
                }

                if (touchingRay != null)
                {
                    enemy.FollowRay(touchingRay, touchDistance);
                }

                enemy.UpdateFollowing(dt, EnemyRayFollowSpeed, Maze, Player.Position, EnemyCollisionRadius);
            }
        }

        private void UpdateEnemySound()
        {
            if (Enemies.Count == 0 && PatrolEnemies.Count == 0)
            {
                Sound.StopEnemySound();
                return;
            }

            var closestDistance = float.MaxValue;
            var playerPos = Player.Position;

            for (var i = 0; i < Enemies.Count; i++)
            {
                var enemyPos = Enemies[i].Position;
                var dx = playerPos.X - enemyPos.X;
                var dy = playerPos.Y - enemyPos.Y;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }

            for (var i = 0; i < PatrolEnemies.Count; i++)
            {
                var enemyPos = PatrolEnemies[i].Position;
                var dx = playerPos.X - enemyPos.X;
                var dy = playerPos.Y - enemyPos.Y;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }

            Sound.UpdateEnemySound(closestDistance);
        }
    }
}
