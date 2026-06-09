using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;
using System.Drawing;

namespace Echo
{
    public struct Vec2
    {
        public float X;
        public float Y;

        public float Length { get { return MathF.Sqrt(X * X + Y * Y); } }

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vec2 Normalized()
        {
            var len = Length;
            if (len == 0) return new Vec2(0, 0);
            return new Vec2(X / len, Y / len);
        }

        public static Vec2 Zero()
        {
            return new Vec2(0, 0);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b)
        {
            return new Vec2(a.X + b.X, a.Y + b.Y);
        }

        public static Vec2 operator -(Vec2 a, Vec2 b)
        {
            return new Vec2(a.X - b.X, a.Y - b.Y);
        }
        public static Vec2 operator *(Vec2 v, float s)
        {
            return new Vec2(v.X * s, v.Y * s);
        }

        public static float Dot(Vec2 a, Vec2 b)
        {
            return a.X*b.X + a.Y*b.Y;
        }

        public static float Distance(Vec2 a, Vec2 b)
        {
            return MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
        }

        // Отражение вектора dir от поверхности с нормалью normal
        public static Vec2 Reflect(Vec2 dir, Vec2 normal)
        {
            return dir - normal * (Dot(dir, normal) / Dot(normal, normal)) * 2 ;
        }

        // Линейная интерполяция между a и b, t = 0..1
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            var LerpX = a.X + (b.X - a.X) * t;
            var LerpY = a.Y + (b.Y - a.Y) * t;
            return new Vec2(LerpX, LerpY);
        }

        // Вектор направления из угла в градусах
        public static Vec2 FromAngleDeg(float degrees)
        {
            var radians = degrees * (MathF.PI / 180f);
            return new Vec2(MathF.Cos(radians), MathF.Sin(radians));
        }

        public PointF ToPointF()
        {
            return new PointF(X, Y);
        }
    }
}
