using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Echo
{
    public class Ray
    {
        private struct BoundsHit
        {
            public Vec2 Point;
            public Vec2 Normal;
        }

        // Список всех точек излома луча
        public List<Vec2> Path { get; private set; }
        public float TotalLength { get; private set; }
        public float RevealedLength { get; private set; } // сколько пикселей луч пролетел с момента выстрела
        public float MaxDistance { get; private set; }
        private float Speed;
        // Возвращает true, пока RevealedLength меньше MaxDistance
        public bool IsAlive => RevealedLength < MaxDistance;
        // Возвращает процент от 1.0 (только вылетел) до 0.0 (достиг MaxDistance)
        public float FadeAlpha
        {
            get
            {
                var alpha = 1f - (RevealedLength / MaxDistance);
                if (alpha < 0) return 0;
                return alpha;
            }
        }

        private Ray(List<Vec2> path, float totalLength, float maxDistance, float speed)
        {
            Path = path;
            Speed = speed;
            TotalLength = totalLength;
            MaxDistance = maxDistance;
        }

        // Прибавляет к RevealedLength скорость, умноженную на dt
        public void Update(float dt)
        {
            RevealedLength += Speed * dt;
        }

        // Должен в цикле пускать maze.Raycast, считать отскоки, 
        // собирать точки в Path, считать общую длину
        public static Ray Create(
            Vec2 origin,
            Vec2 direction,
            Maze maze,
            int maxBounces = 3,
            float maxDist = 400f,
            float speed = 350f)
        {
            var tempPath = new List<Vec2>();
            var currentPos = origin;
            var currentDir = direction.Normalized();
            var currentDist = maxDist;
            tempPath.Add(currentPos);

            for (var i = 0; i < maxBounces; i++)
            {
                if (currentDist <= 0) break;
                var hit = maze.Raycast(currentPos, currentDir, currentDist);
                if (hit != null)
                {
                    tempPath.Add(hit.Point);
                    currentDist -= (hit.Point - currentPos).Length;
                    currentDir = Vec2.Reflect(currentDir, hit.Normal);
                    currentPos = hit.Point + hit.Normal * 0.1f;
                }
                else
                {
                    tempPath.Add(currentPos + currentDir * currentDist);
                    break;
                }
            }

            var totalLength = 0f;
            for (var i = 0;i < tempPath.Count - 1 ;i++)
            {
                totalLength += (tempPath[i] - tempPath[i+1]).Length;
            }

            return new Ray(tempPath, totalLength, maxDist, speed);
        }

        public static Ray CreateInBounds(
            Vec2 origin,
            Vec2 direction,
            RectangleF bounds,
            int maxBounces = 7,
            float maxDist = 320f,
            float speed = 40f,
            bool stopOnBoundsHit = false)
        {
            var tempPath = new List<Vec2>();
            var currentPos = origin;
            var currentDir = direction.Normalized();
            var currentDist = maxDist;
            tempPath.Add(currentPos);

            for (var i = 0; i < maxBounces; i++)
            {
                if (currentDist <= 0) break;
                var hit = RaycastBounds(currentPos, currentDir, bounds, currentDist);
                if (hit != null)
                {
                    var hitInfo = hit.Value;
                    tempPath.Add(hitInfo.Point);
                    currentDist -= (hitInfo.Point - currentPos).Length;
                    if (stopOnBoundsHit)
                    {
                        break;
                    }
                    currentDir = Vec2.Reflect(currentDir, hitInfo.Normal);
                    currentPos = hitInfo.Point + hitInfo.Normal * 0.1f;
                }
                else
                {
                    tempPath.Add(currentPos + currentDir * currentDist);
                    break;
                }
            }

            var totalLength = 0f;
            for (var i = 0; i < tempPath.Count - 1; i++)
            {
                totalLength += (tempPath[i] - tempPath[i + 1]).Length;
            }

            return new Ray(tempPath, totalLength, maxDist, speed);
        }

        private static BoundsHit? RaycastBounds(Vec2 origin, Vec2 direction, RectangleF bounds, float maxDist)
        {
            var dir = direction.Normalized();
            if (dir.Length == 0) return null;

            var tx = float.MaxValue;
            var ty = float.MaxValue;
            var normalX = new Vec2(0, 0);
            var normalY = new Vec2(0, 0);

            if (dir.X > 0)
            {
                tx = (bounds.Right - origin.X) / dir.X;
                normalX = new Vec2(-1, 0);
            }
            else if (dir.X < 0)
            {
                tx = (bounds.Left - origin.X) / dir.X;
                normalX = new Vec2(1, 0);
            }

            if (dir.Y > 0)
            {
                ty = (bounds.Bottom - origin.Y) / dir.Y;
                normalY = new Vec2(0, -1);
            }
            else if (dir.Y < 0)
            {
                ty = (bounds.Top - origin.Y) / dir.Y;
                normalY = new Vec2(0, 1);
            }

            if (tx <= 0) tx = float.MaxValue;
            if (ty <= 0) ty = float.MaxValue;

            var t = MathF.Min(tx, ty);
            if (t == float.MaxValue || float.IsNaN(t) || t > maxDist) return null;

            var normal = tx < ty ? normalX : normalY;
            if (MathF.Abs(tx - ty) < 0.0001f)
            {
                normal = new Vec2(normalX.X + normalY.X, normalX.Y + normalY.Y);
            }

            return new BoundsHit
            {
                Point = origin + dir * t,
                Normal = normal
            };
        }
    }
}
