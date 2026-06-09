using System;
using System.Collections.Generic;

namespace Echo
{
    public class Enemy
    {
        public Vec2 Position { get; private set; }

        private Ray? followRay;
        private float followDistance;
        private Vec2 velocity;
        private List<Vec2>? rememberedPath;
        private float rememberedDistance;
        private float rememberedTimeLeft;
        private Vec2 rememberedDirection;
        private Vec2 rememberedContinuationDirection;

        private const float FollowMemorySeconds = 1f; // Сколько секунд враг помнит путь после пропажи луча. Диапазон: >= 0.
        private const float FollowMemorySpeedFactor = 0.9f; // Множитель скорости во время движения по памяти. Диапазон: > 0 (обычно 0..1.5).
        private const float SteeringStrength = 6f; // Сила подруливания к цели (плавность/резкость поворота). Диапазон: > 0.
        private const float StopDamping = 9f; // Скорость затухания скорости при остановке. Диапазон: > 0.
        private const float LivePlayerTargetBlend = 0.23f; // Доля подмешивания текущей позиции игрока в цель на луче. Диапазон: [0..1].
        private const float LivePlayerSnapDistanceOnRay = 8f; // На каком расстоянии до начала луча цель сразу переключается на игрока (px). Диапазон: >= 0.
        private const float RayTargetStaleDistance = 42f; // Порог "устаревания" точки луча относительно игрока, после которого включается blend (px). Диапазон: >= 0.

        public bool IsEngaged => followRay != null || rememberedTimeLeft > 0f;

        public Enemy(Vec2 position)
        {
            Position = position;
            followDistance = 0f;
            velocity = Vec2.Zero();
            rememberedDirection = Vec2.Zero();
            rememberedContinuationDirection = Vec2.Zero();
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

        public void UpdateFollowing(float dt, float speed, Maze maze, Vec2 playerPosition, float collisionRadius)
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

                    targetPoint = GetPointOnRayByDistance(ray, followDistance);
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
                    var memorySteeringLerp = MathF.Min(1f, dt * SteeringStrength);
                    velocity = Vec2.Lerp(velocity, memoryDesiredVelocity, memorySteeringLerp);
                    MoveWithWallCollision(velocity * dt, maze, collisionRadius);
                    return;
                }
            }

            if (!hasTarget)
            {
                var stopLerp = MathF.Min(1f, dt * StopDamping);
                velocity = Vec2.Lerp(velocity, Vec2.Zero(), stopLerp);
                MoveWithWallCollision(velocity * dt, maze, collisionRadius);
                return;
            }

            var toTarget = targetPoint - Position;
            var distanceToTarget = toTarget.Length;
            if (distanceToTarget <= 0.001f)
            {
                var stopLerp = MathF.Min(1f, dt * StopDamping);
                velocity = Vec2.Lerp(velocity, Vec2.Zero(), stopLerp);
                return;
            }

            var desiredVelocity = toTarget.Normalized() * movementSpeed;
            var steeringLerp = MathF.Min(1f, dt * SteeringStrength);
            velocity = Vec2.Lerp(velocity, desiredVelocity, steeringLerp);
            MoveWithWallCollision(velocity * dt, maze, collisionRadius);

            var afterToTarget = targetPoint - Position;
            if (Vec2.Dot(toTarget, afterToTarget) < 0f)
            {
                Position = targetPoint;
            }
        }

        private void MoveWithWallCollision(Vec2 delta, Maze maze, float collisionRadius)
        {
            var nextPosX = new Vec2(Position.X + delta.X, Position.Y);
            if (!CollidesWithWall(nextPosX, maze, collisionRadius))
            {
                Position = new Vec2(nextPosX.X, Position.Y);
            }

            var nextPosY = new Vec2(Position.X, Position.Y + delta.Y);
            if (!CollidesWithWall(nextPosY, maze, collisionRadius))
            {
                Position = new Vec2(Position.X, nextPosY.Y);
            }
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

        public static bool TryGetTouchDistanceOnRevealedRay(
            Ray ray,
            Vec2 point,
            float touchRadius,
            out float touchDistance,
            out float bestDistanceSq)
        {
            touchDistance = 0f;
            bestDistanceSq = float.MaxValue;
            if (ray.Path.Count < 2) return false;
            if (ray.RevealedLength <= 0f) return false;

            var revealed = ray.RevealedLength;
            if (revealed > ray.TotalLength) revealed = ray.TotalLength;
            var radiusSq = touchRadius * touchRadius;
            var traversed = 0f;
            var found = false;

            for (var i = 0; i < ray.Path.Count - 1; i++)
            {
                var p1 = ray.Path[i];
                var p2 = ray.Path[i + 1];
                var segmentLength = Vec2.Distance(p1, p2);
                if (segmentLength <= 0f) continue;

                var visibleLength = segmentLength;
                if (traversed + visibleLength > revealed)
                {
                    visibleLength = revealed - traversed;
                }
                if (visibleLength <= 0f) break;

                var segmentEnd = visibleLength >= segmentLength
                    ? p2
                    : Vec2.Lerp(p1, p2, visibleLength / segmentLength);

                var segment = segmentEnd - p1;
                var segmentDot = Vec2.Dot(segment, segment);
                if (segmentDot <= 0f)
                {
                    traversed += visibleLength;
                    continue;
                }

                var toPoint = point - p1;
                var t = Vec2.Dot(toPoint, segment) / segmentDot;
                if (t < 0f) t = 0f;
                else if (t > 1f) t = 1f;

                var closest = p1 + (segment * t);
                var dx = point.X - closest.X;
                var dy = point.Y - closest.Y;
                var distanceSq = (dx * dx) + (dy * dy);
                if (distanceSq <= radiusSq && distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    touchDistance = traversed + (visibleLength * t);
                    found = true;
                }

                traversed += visibleLength;
                if (traversed >= revealed) break;
            }

            return found;
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

        public static Vec2 GetPointOnRayDistance(Ray ray, float distance)
        {
            return GetPointOnRayByDistance(ray, distance);
        }

        private static Vec2 GetPointOnRayByDistance(Ray ray, float distance)
        {
            return GetPointOnPathByDistance(ray.Path, distance);
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
