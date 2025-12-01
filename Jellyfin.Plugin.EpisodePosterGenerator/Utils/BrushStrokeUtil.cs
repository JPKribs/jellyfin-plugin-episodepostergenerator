using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils
{
    /// <summary>
    /// Builds organic paint smear paths with smooth edges and proper boundary respect.
    /// </summary>
    public class BrushStrokeUtil
    {
        private readonly Random _random;

        public BrushStrokeUtil(int seed)
        {
            _random = new Random(seed);
        }

        // MARK: BuildStrokePath
        public SKPath BuildStrokePath(SKRect bounds, SKRect textArea, float baseWidth)
        {
            var combinedPath = new SKPath();

            float centerX = bounds.MidX + (float)(_random.NextDouble() - 0.5) * bounds.Width * 0.08f;
            float centerY = bounds.Top + bounds.Height * 0.4f;

            int strokeCount = 6 + _random.Next(4);
            for (int i = 0; i < strokeCount; i++)
            {
                var stroke = CreateSmoothStroke(bounds, textArea, centerX, centerY, i);
                combinedPath.AddPath(stroke);
                stroke.Dispose();
            }

            int splatCount = 12 + _random.Next(8);
            for (int i = 0; i < splatCount; i++)
            {
                var splat = CreateSmoothSplatter(bounds, textArea, centerX, centerY);
                if (splat != null)
                {
                    combinedPath.AddPath(splat);
                    splat.Dispose();
                }
            }

            return combinedPath;
        }

        // MARK: CreateSmoothStroke
        private SKPath CreateSmoothStroke(SKRect bounds, SKRect textArea, float centerX, float centerY, int index)
        {
            float angle = -12f + (float)(_random.NextDouble() - 0.5) * 20f;
            float angleRad = angle * (float)Math.PI / 180f;

            float maxLength = bounds.Width * 0.85f;
            float length = maxLength * (0.6f + (float)_random.NextDouble() * 0.4f);
            float width = bounds.Height * (0.15f + (float)_random.NextDouble() * 0.2f);

            float offsetX = (float)(_random.NextDouble() - 0.5) * bounds.Width * 0.1f;
            float offsetY = (float)(_random.NextDouble() - 0.5) * bounds.Height * 0.15f;
            float strokeCenterX = centerX + offsetX;
            float strokeCenterY = centerY + offsetY;

            var topPoints = new List<SKPoint>();
            var bottomPoints = new List<SKPoint>();
            int segments = 40;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = -length / 2 + t * length;

                float taper = CalculateTaper(t);

                float topWave = GenerateSmoothNoise(t, 3, 0.15f) * width * taper;
                float bottomWave = GenerateSmoothNoise(t + 0.5f, 3, 0.15f) * width * taper;

                float yTop = -width / 2 * taper + topWave;
                float yBottom = width / 2 * taper + bottomWave;

                var topRot = RotatePoint(x, yTop, angleRad);
                var bottomRot = RotatePoint(x, yBottom, angleRad);

                var topFinal = new SKPoint(strokeCenterX + topRot.X, strokeCenterY + topRot.Y);
                var bottomFinal = new SKPoint(strokeCenterX + bottomRot.X, strokeCenterY + bottomRot.Y);

                topFinal = ClampToBounds(topFinal, bounds, textArea);
                bottomFinal = ClampToBounds(bottomFinal, bounds, textArea);

                topPoints.Add(topFinal);
                bottomPoints.Add(bottomFinal);
            }

            return BuildSmoothClosedPath(topPoints, bottomPoints);
        }

        // MARK: CalculateTaper
        private float CalculateTaper(float t)
        {
            float taper = 1f;
            if (t < 0.12f)
                taper = t / 0.12f;
            else if (t > 0.88f)
                taper = (1f - t) / 0.12f;

            return taper * taper * (3f - 2f * taper);
        }

        // MARK: GenerateSmoothNoise
        private float GenerateSmoothNoise(float t, int octaves, float persistence)
        {
            float total = 0f;
            float frequency = 4f;
            float amplitude = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += InterpolatedNoise(t * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }

            return total / maxValue;
        }

        // MARK: InterpolatedNoise
        private float InterpolatedNoise(float x)
        {
            int intX = (int)Math.Floor(x);
            float fracX = x - intX;

            float v1 = SeededRandom(intX);
            float v2 = SeededRandom(intX + 1);

            float smoothT = fracX * fracX * (3f - 2f * fracX);
            return v1 + (v2 - v1) * smoothT;
        }

        // MARK: SeededRandom
        private float SeededRandom(int x)
        {
            int n = x * 374761393 + _random.Next(1000);
            n = (n << 13) ^ n;
            return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
        }

        // MARK: RotatePoint
        private SKPoint RotatePoint(float x, float y, float angleRad)
        {
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            return new SKPoint(x * cos - y * sin, x * sin + y * cos);
        }

        // MARK: ClampToBounds
        private SKPoint ClampToBounds(SKPoint point, SKRect bounds, SKRect textArea)
        {
            float x = Math.Max(bounds.Left, Math.Min(bounds.Right, point.X));
            float y = Math.Max(bounds.Top, Math.Min(bounds.Bottom, point.Y));

            if (x < textArea.Right && y > textArea.Top)
            {
                if (point.X < textArea.Right)
                    y = Math.Min(y, textArea.Top);
            }

            return new SKPoint(x, y);
        }

        // MARK: BuildSmoothClosedPath
        private SKPath BuildSmoothClosedPath(List<SKPoint> topPoints, List<SKPoint> bottomPoints)
        {
            var path = new SKPath();

            if (topPoints.Count < 2) return path;

            path.MoveTo(topPoints[0]);

            for (int i = 0; i < topPoints.Count - 1; i++)
            {
                var p0 = i > 0 ? topPoints[i - 1] : topPoints[i];
                var p1 = topPoints[i];
                var p2 = topPoints[i + 1];
                var p3 = i < topPoints.Count - 2 ? topPoints[i + 2] : topPoints[i + 1];

                var cp1 = new SKPoint(
                    p1.X + (p2.X - p0.X) / 6f,
                    p1.Y + (p2.Y - p0.Y) / 6f);
                var cp2 = new SKPoint(
                    p2.X - (p3.X - p1.X) / 6f,
                    p2.Y - (p3.Y - p1.Y) / 6f);

                path.CubicTo(cp1, cp2, p2);
            }

            for (int i = bottomPoints.Count - 1; i > 0; i--)
            {
                var p0 = i < bottomPoints.Count - 1 ? bottomPoints[i + 1] : bottomPoints[i];
                var p1 = bottomPoints[i];
                var p2 = bottomPoints[i - 1];
                var p3 = i > 1 ? bottomPoints[i - 2] : bottomPoints[i - 1];

                var cp1 = new SKPoint(
                    p1.X + (p2.X - p0.X) / 6f,
                    p1.Y + (p2.Y - p0.Y) / 6f);
                var cp2 = new SKPoint(
                    p2.X - (p3.X - p1.X) / 6f,
                    p2.Y - (p3.Y - p1.Y) / 6f);

                path.CubicTo(cp1, cp2, p2);
            }

            path.Close();
            return path;
        }

        // MARK: CreateSmoothSplatter
        private SKPath? CreateSmoothSplatter(SKRect bounds, SKRect textArea, float centerX, float centerY)
        {
            float angle = (float)_random.NextDouble() * 360f;
            float distance = bounds.Height * (0.2f + (float)_random.NextDouble() * 0.35f);

            float splatX = centerX + (float)Math.Cos(angle * Math.PI / 180) * distance * 1.8f;
            float splatY = centerY + (float)Math.Sin(angle * Math.PI / 180) * distance * 0.7f;

            splatX = Math.Max(bounds.Left + 10, Math.Min(bounds.Right - 10, splatX));
            splatY = Math.Max(bounds.Top + 10, Math.Min(bounds.Bottom - 10, splatY));

            if (splatX < textArea.Right && splatY > textArea.Top)
                return null;

            var path = new SKPath();
            float size = 6f + (float)_random.NextDouble() * 20f;

            path.AddCircle(splatX, splatY, size * (0.8f + (float)_random.NextDouble() * 0.4f));

            if (_random.NextDouble() > 0.6)
            {
                float offsetX = (float)(_random.NextDouble() - 0.5) * size;
                float offsetY = (float)(_random.NextDouble() - 0.5) * size;
                path.AddCircle(splatX + offsetX, splatY + offsetY, size * 0.6f);
            }

            return path;
        }
    }
}