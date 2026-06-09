using System;
using System.Collections.Generic;
using System.Drawing;

namespace Echo
{
    // Враг-патрульный: ходит по случайным точкам внутри заданной прямоугольной зоны.
    // Реагирует на лучи игрока так же, как обычный Enemy, но не может выйти из своей зоны.
    public class PatrolEnemy
    {
        public Vec2 Position { get; private set; }
        public RectangleF PatrolArea { get; }
        public Vec2 CurrentTarget { get; private set; }

        private Vec2 velocity;
        private readonly Random random;
        private float idleTimer;
        private float stuckTimer;

        // Состояние преследования по лучу 
        private Ray? followRay;
        private float followDistance;
        private List<Vec2>? rememberedPath;
        private float rememberedDistance;
        private float rememberedTimeLeft;
        private Vec2 rememberedDirection;
        private Vec2 rememberedContinuationDirection;

        // Параметры патрулирования.
        private const float WaypointReachDistance = 8f; // Дистанция, на которой считается что точка достигнута (px).
        private const float SteeringStrength = 4f; // Плавность поворота к цели.
        private const float StopDamping = 6f; // Затухание скорости в режиме ожидания.
        private const float IdleDurationMin = 0.3f; // Минимальная пауза между точками маршрута (сек).
        private const float IdleDurationMax = 0.5f; // Максимальная пауза между точками маршрута (сек).
        private const float StuckResetSeconds = 0.1f; // Сколько секунд враг может почти не двигаться, прежде чем сменит цель.
        private const float StuckMovementThreshold = 4f; // Скорость, ниже которой считается что враг застрял (px/s).
        private const int TargetPickAttempts = 24;

        // Параметры режима преследования
        private const float FollowMemorySeconds = 1f;
        private const float FollowMemorySpeedFactor = 0.9f;
        private const float ChaseSteeringStrength = 6f;
        private const float ChaseStopDamping = 9f;
        private const float LivePlayerTargetBlend = 0.23f;
        private const float LivePlayerSnapDistanceOnRay = 8f;
        private const float RayTargetStaleDistance = 42f;

        public bool IsEngaged => followRay != null || rememberedTimeLeft > 0f;

        public PatrolEnemy(Vec2 position, RectangleF patrolArea, int seed)
        {
            Position = position;
            PatrolArea = patrolArea;
            random = new Random(seed);
            CurrentTarget = position;
            idleTimer = 0f;
            stuckTimer = 0f;
            rememberedDirection = Vec2.Zero();
            rememberedContinuationDirection = Vec2.Zero();
        }

        public void Initialize(Maze maze)
        {
            CurrentTarget = PickNewTarget(maze);
        }

        public void FollowRay(Ray ray, float distanceOnRay)
        {
            followRay = ray;
            followDistance = distanceOnRay;
            if (followDistance < 0f) followDistance = 0f;

            rememberedPath = BuildRevealedPath(ray);
            rememberedDistance = followDistance;
            var rememberedTotal = GetPathTotalLength(rememberedPath);
            if (rememberedDistance > rememberedTotal)
            {
                rememberedDistance = rememberedTotal;
            }
            UpdateRememberedContinuationDirection();
            rememberedTimeLeft = FollowMemorySeconds;
        }

        public void Update(
            float dt,
            float patrolSpeed,
            float chaseSpeed,
            Maze maze,
            float collisionRadius,
            Vec2 playerPosition)
        {
            if (IsEngaged)
            {
                UpdateChasing(dt, chaseSpeed, maze, playerPosition, collisionRadius);
                // На время преследования сбрасываем таймеры патрулирования,
                // чтобы возврат к маршруту был свежим.
                idleTimer = 0f;
                stuckTimer = 0f;
                return;
            }

            UpdatePatrolling(dt, patrolSpeed, maze, collisionRadius);
        }

        private void UpdatePatrolling(float dt, float speed, Maze maze, float collisionRadius)
        {
            if (idleTimer > 0f)
            {
                idleTimer -= dt;
                var stopLerp = MathF.Min(1f, dt * StopDamping);
                velocity = Vec2.Lerp(velocity, Vec2.Zero(), stopLerp);
                MoveWithConstraints(velocity * dt, maze, collisionRadius);
                return;
            }

            var toTarget = CurrentTarget - Position;
            if (toTarget.Length <= WaypointReachDistance)
            {
                CurrentTarget = PickNewTarget(maze);
                idleTimer = IdleDurationMin + ((float)random.NextDouble() * (IdleDurationMax - IdleDurationMin));
                stuckTimer = 0f;
                return;
            }

            var desiredVelocity = toTarget.Normalized() * speed;
            var steeringLerp = MathF.Min(1f, dt * SteeringStrength);
            velocity = Vec2.Lerp(velocity, desiredVelocity, steeringLerp);

            var prevPos = Position;
            MoveWithConstraints(velocity * dt, maze, collisionRadius);

            // Если враг практически не сдвинулся, хотя должен был — упёрся в стену, выбираем новую цель
            var actualMovement = Vec2.Distance(prevPos, Position);
            if (actualMovement < StuckMovementThreshold * dt)
            {
                stuckTimer += dt;
                if (stuckTimer >= StuckResetSeconds)
                {
                    CurrentTarget = PickNewTarget(maze);
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        private void UpdateChasing(float dt, float speed, Maze maze, Vec2 playerPosition, float collisionRadius)
        {
            var hasTarget = false;
            var targetPoint = Position;
            var movementSpeed = speed;

            if (followRay != null)
            {
                if (!followRay.IsAlive)
                {
                    followRay = null;
                }
                else
                {
                    var ray = followRay;
                    var nextDistance = followDistance - (speed * dt);
                    if (nextDistance < 0f) nextDistance = 0f;
                    followDistance = nextDistance;

                    targetPoint = GetPointOnPathByDistance(ray.Path, followDistance);
                    var staleOffset = playerPosition - targetPoint;
                    if (followDistance <= LivePlayerSnapDistanceOnRay)
                    {
                        targetPoint = playerPosition;
                    }
                    else if (staleOffset.Length > RayTargetStaleDistance)
                    {
                        targetPoint = Vec2.Lerp(targetPoint, playerPosition, LivePlayerTargetBlend);
                    }

                    var activeDirection = targetPoint - Position;
                    if (activeDirection.Length > 0.001f)
                    {
                        rememberedDirection = activeDirection.Normalized();
                    }
                    hasTarget = true;

                    rememberedPath = BuildRevealedPath(ray);
                    rememberedDistance = followDistance;
                    var rememberedTotal = GetPathTotalLength(rememberedPath);
                    if (rememberedDistance > rememberedTotal)
                    {
                        rememberedDistance = rememberedTotal;
                    }
                    UpdateRememberedContinuationDirection();
                    rememberedTimeLeft = FollowMemorySeconds;
                }
            }

            if (!hasTarget && rememberedTimeLeft > 0f)
            {
                rememberedTimeLeft -= dt;
                if (rememberedTimeLeft < 0f) rememberedTimeLeft = 0f;

                movementSpeed = speed * FollowMemorySpeedFactor;
                if (rememberedPath != null && rememberedPath.Count >= 2 && rememberedDistance > 0.001f)
                {
                    rememberedDistance -= movementSpeed * dt;
                    if (rememberedDistance < 0f) rememberedDistance = 0f;

                    targetPoint = GetPointOnPathByDistance(rememberedPath, rememberedDistance);
                    var memoryDirection = targetPoint - Position;
                    if (memoryDirection.Length > 0.001f)
                    {
                        rememberedDirection = memoryDirection.Normalized();
                    }
                    hasTarget = true;
                }
                else if (rememberedContinuationDirection.Length > 0.001f || rememberedDirection.Length > 0.001f)
                {
                    var continuationDirection = rememberedContinuationDirection.Length > 0.001f
                        ? rememberedContinuationDirection
                        : rememberedDirection;
                    var memoryDesiredVelocity = continuationDirection * movementSpeed;
                    var memorySteeringLerp = MathF.Min(1f, dt * ChaseSteeringStrength);
                    velocity = Vec2.Lerp(velocity, memoryDesiredVelocity, memorySteeringLerp);
                    MoveWithConstraints(velocity * dt, maze, collisionRadius);
                    return;
                }
            }

            if (!hasTarget)
            {
                var stopLerp = MathF.Min(1f, dt * ChaseStopDamping);
                velocity = Vec2.Lerp(velocity, Vec2.Zero(), stopLerp);
                MoveWithConstraints(velocity * dt, maze, collisionRadius);
                return;
            }

            var toTarget = targetPoint - Position;
            var distanceToTarget = toTarget.Length;
            if (distanceToTarget <= 0.001f)
            {
                var stopLerp = MathF.Min(1f, dt * ChaseStopDamping);
                velocity = Vec2.Lerp(velocity, Vec2.Zero(), stopLerp);
                return;
            }

            var desiredVelocity = toTarget.Normalized() * movementSpeed;
            var steeringLerp = MathF.Min(1f, dt * ChaseSteeringStrength);
            velocity = Vec2.Lerp(velocity, desiredVelocity, steeringLerp);
            MoveWithConstraints(velocity * dt, maze, collisionRadius);
        }

        private Vec2 PickNewTarget(Maze maze)
        {
            for (var i = 0; i < TargetPickAttempts; i++)
            {
                var x = PatrolArea.Left + ((float)random.NextDouble() * PatrolArea.Width);
                var y = PatrolArea.Top + ((float)random.NextDouble() * PatrolArea.Height);
                if (!maze.IsWallAtPixel(x, y))
                {
                    return new Vec2(x, y);
                }
            }
            return Position;
        }

        private void MoveWithConstraints(Vec2 delta, Maze maze, float collisionRadius)
        {
            var nextPosX = new Vec2(Position.X + delta.X, Position.Y);
            if (!CollidesWithWall(nextPosX, maze, collisionRadius) && IsWithinPatrolArea(nextPosX))
            {
                Position = new Vec2(nextPosX.X, Position.Y);
            }

            var nextPosY = new Vec2(Position.X, Position.Y + delta.Y);
            if (!CollidesWithWall(nextPosY, maze, collisionRadius) && IsWithinPatrolArea(nextPosY))
            {
                Position = new Vec2(Position.X, nextPosY.Y);
            }
        }

        private bool IsWithinPatrolArea(Vec2 pos)
        {
            return pos.X >= PatrolArea.Left
                && pos.X <= PatrolArea.Right
                && pos.Y >= PatrolArea.Top
                && pos.Y <= PatrolArea.Bottom;
        }

        private static bool CollidesWithWall(Vec2 pos, Maze maze, float collisionRadius)
        {
            var gridX = (int)(pos.X / maze.TileSize);
            var gridY = (int)(pos.Y / maze.TileSize);

            for (var x = gridX - 1; x <= gridX + 1; x++)
            {
                for (var y = gridY - 1; y <= gridY + 1; y++)
                {
                    if (!maze.IsWall(x, y)) continue;

                    var wallLeft = x * maze.TileSize;
                    var wallRight = wallLeft + maze.TileSize;
                    var wallTop = y * maze.TileSize;
                    var wallBottom = wallTop + maze.TileSize;

                    var closestX = pos.X;
                    if (closestX < wallLeft) closestX = wallLeft;
                    else if (closestX > wallRight) closestX = wallRight;

                    var closestY = pos.Y;
                    if (closestY < wallTop) closestY = wallTop;
                    else if (closestY > wallBottom) closestY = wallBottom;

                    var dx = pos.X - closestX;
                    var dy = pos.Y - closestY;
                    var distSq = (dx * dx) + (dy * dy);
                    if (distSq < (collisionRadius * collisionRadius))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void UpdateRememberedContinuationDirection()
        {
            if (rememberedPath == null || rememberedPath.Count < 2) return;

            var continuation = rememberedPath[0] - rememberedPath[1];
            if (continuation.Length <= 0.001f) return;
            rememberedContinuationDirection = continuation.Normalized();
        }

        private static List<Vec2> BuildRevealedPath(Ray ray)
        {
            var result = new List<Vec2>();
            if (ray.Path.Count == 0) return result;

            result.Add(ray.Path[0]);
            var revealed = ray.RevealedLength;
            if (revealed > ray.TotalLength) revealed = ray.TotalLength;
            if (revealed <= 0f) return result;

            var traversed = 0f;
            for (var i = 0; i < ray.Path.Count - 1; i++)
            {
                var p1 = ray.Path[i];
                var p2 = ray.Path[i + 1];
                var segmentLength = Vec2.Distance(p1, p2);
                if (segmentLength <= 0f) continue;

                if (traversed + segmentLength <= revealed)
                {
                    result.Add(p2);
                    traversed += segmentLength;
                    continue;
                }

                var remaining = revealed - traversed;
                if (remaining > 0f)
                {
                    result.Add(Vec2.Lerp(p1, p2, remaining / segmentLength));
                }
                break;
            }

            return result;
        }

        private static float GetPathTotalLength(List<Vec2>? path)
        {
            if (path == null || path.Count < 2) return 0f;

            var total = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                total += Vec2.Distance(path[i], path[i + 1]);
            }
            return total;
        }

        private static Vec2 GetPointOnPathByDistance(List<Vec2> path, float distance)
        {
            if (path.Count == 0) return new Vec2(0, 0);
            if (distance <= 0f) return path[0];

            var left = distance;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var p1 = path[i];
                var p2 = path[i + 1];
                var segmentLength = Vec2.Distance(p1, p2);
                if (segmentLength <= 0f) continue;

                if (left <= segmentLength)
                {
                    var t = left / segmentLength;
                    return Vec2.Lerp(p1, p2, t);
                }

                left -= segmentLength;
            }

            return path[path.Count - 1];
        }
    }
}
