using System;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils
{
    public class BrushStrokeUtil
    {
        private readonly Random _random;

        public BrushStrokeUtil(int seed)
        {
            _random = new Random(seed);
        }

        // BuildStrokePath
        // Builds a complete brush stroke path with multiple strokes, edge textures, and splatters.
        public SKPath BuildStrokePath(SKRect bounds, SKRect textArea, float baseWidth)
        {
            var combinedPath = new SKPath();

            var textBuffer = bounds.Height * 0.08f;
            var textKeepClear = new SKRect(
                textArea.Left - textBuffer,
                textArea.Top - textBuffer,
                textArea.Right + textBuffer,
                textArea.Bottom + textBuffer
            );

            int strokeCount = 2 + _random.Next(2);

            for (int i = 0; i < strokeCount; i++)
            {
                using var stroke = CreateBrushStroke(bounds, textKeepClear, i, strokeCount);
                combinedPath.AddPath(stroke);
            }

            int edgeStrokeCount = 5 + _random.Next(10);
            for (int i = 0; i < edgeStrokeCount; i++)
            {
                using var edgeStroke = CreateEdgeStroke(bounds, textKeepClear);
                if (edgeStroke != null)
                {
                    combinedPath.AddPath(edgeStroke);
                }
            }

            int splatCount = 20 + _random.Next(30);
            for (int i = 0; i < splatCount; i++)
            {
                using var splat = CreatePaintSplatter(bounds, textKeepClear);
                if (splat != null)
                {
                    combinedPath.AddPath(splat);
                }
            }

            return combinedPath;
        }

        // CreateBrushStroke
        // Creates a single organic brush stroke path using bezier curves and wave modulation.
        private SKPath CreateBrushStroke(SKRect bounds, SKRect textArea, int index, int total)
        {
            var path = new SKPath();

            float baseY = bounds.Top + (bounds.Height * 0.25f) + (index / (float)total) * (bounds.Height * 0.2f);
            baseY += (float)(_random.NextDouble() - 0.5) * bounds.Height * 0.15f;

            if (baseY + bounds.Height * 0.12f > textArea.Top)
            {
                baseY = textArea.Top - bounds.Height * 0.15f;
            }

            float startX = bounds.Left - bounds.Width * 0.05f;
            float endX = bounds.Right + bounds.Width * 0.05f;

            int segments = 40;
            var topPoints = new SKPoint[segments + 1];
            var bottomPoints = new SKPoint[segments + 1];

            float verticalWave = bounds.Height * (0.02f + (float)_random.NextDouble() * 0.04f);
            float baseHeight = bounds.Height * (0.06f + (float)_random.NextDouble() * 0.04f);

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = startX + (endX - startX) * t;

                // Create smooth wave using multiple sine waves for organic variation
                float wave = (float)(
                    Math.Sin(t * Math.PI * 2.3) * 0.5 +
                    Math.Sin(t * Math.PI * 4.7) * 0.3 +
                    Math.Sin(t * Math.PI * 1.1) * 0.2
                );
                float y = baseY + wave * verticalWave;

                float widthMult = CalculateNaturalWidth(t);
                float halfHeight = baseHeight * widthMult * 0.5f;

                // Add organic edge variation using perlin noise for natural brush texture
                float topEdge = PerlinNoise(t * 10f + index) * halfHeight * 0.3f;
                float bottomEdge = PerlinNoise(t * 10f + index + 100) * halfHeight * 0.3f;

                topPoints[i] = new SKPoint(x, y - halfHeight + topEdge);
                bottomPoints[i] = new SKPoint(x, y + halfHeight + bottomEdge);
            }

            path.MoveTo(topPoints[0]);

            for (int i = 1; i < topPoints.Length; i++)
            {
                if (i % 3 == 0 && i < topPoints.Length - 1)
                {
                    path.QuadTo(topPoints[i], topPoints[i + 1]);
                }
                else if (i % 3 != 1)
                {
                    path.LineTo(topPoints[i]);
                }
            }

            for (int i = bottomPoints.Length - 1; i >= 0; i--)
            {
                if (i % 3 == 0 && i > 0)
                {
                    path.QuadTo(bottomPoints[i], bottomPoints[i - 1]);
                }
                else if (i % 3 != 2)
                {
                    path.LineTo(bottomPoints[i]);
                }
            }

            path.Close();
            return path;
        }

        // CreateEdgeStroke
        // Creates a small irregular shape for edge texture effects.
        private SKPath? CreateEdgeStroke(SKRect bounds, SKRect textArea)
        {
            float x = bounds.Left + (float)_random.NextDouble() * bounds.Width;
            float y = bounds.Top + (float)_random.NextDouble() * (textArea.Top - bounds.Top);

            if (y > textArea.Top - bounds.Height * 0.05f)
                return null;

            var path = new SKPath();

            float length = bounds.Width * (0.02f + (float)_random.NextDouble() * 0.08f);
            float height = bounds.Height * (0.005f + (float)_random.NextDouble() * 0.02f);
            float angle = (float)_random.NextDouble() * 360f;

            int points = 6 + _random.Next(6);
            for (int i = 0; i < points; i++)
            {
                float a = angle + (i / (float)points) * 360f;
                float r = (i % 2 == 0 ? length : length * 0.6f) * (0.7f + (float)_random.NextDouble() * 0.3f);

                float px = x + MathF.Cos(a * MathF.PI / 180f) * r;
                float py = y + MathF.Sin(a * MathF.PI / 180f) * height;

                if (i == 0)
                    path.MoveTo(px, py);
                else
                    path.LineTo(px, py);
            }
            path.Close();

            return path;
        }

        // CreatePaintSplatter
        // Creates an irregular paint splatter blob with optional satellite drops.
        private SKPath? CreatePaintSplatter(SKRect bounds, SKRect textArea)
        {
            float x = bounds.Left + (float)_random.NextDouble() * bounds.Width;
            float y = bounds.Top + (float)_random.NextDouble() * (textArea.Top - bounds.Top - bounds.Height * 0.05f);

            if (y > textArea.Top - bounds.Height * 0.08f)
                return null;

            float baseSize = bounds.Height * (0.002f + (float)_random.NextDouble() * 0.006f);

            if (_random.NextDouble() < 0.15)
                baseSize *= 2f;

            var path = new SKPath();

            int points = 5 + _random.Next(4);
            for (int i = 0; i < points; i++)
            {
                float angle = (i / (float)points) * 2f * MathF.PI + (float)(_random.NextDouble() - 0.5) * 1f;
                float radius = baseSize * (0.6f + (float)_random.NextDouble() * 0.8f);

                float px = x + MathF.Cos(angle) * radius;
                float py = y + MathF.Sin(angle) * radius;

                if (i == 0)
                    path.MoveTo(px, py);
                else
                    path.LineTo(px, py);
            }
            path.Close();

            if (_random.NextDouble() < 0.25)
            {
                float dropX = x + (float)(_random.NextDouble() - 0.5) * baseSize * 4f;
                float dropY = y + (float)(_random.NextDouble() - 0.5) * baseSize * 4f;
                path.AddCircle(dropX, dropY, baseSize * 0.25f);
            }

            return path;
        }

        // CalculateNaturalWidth
        // Calculates natural brush width tapering based on stroke position.
        private float CalculateNaturalWidth(float t)
        {
            // Gradual fade in at stroke start, sharper fade out at end to simulate brush lift
            if (t < 0.08f)
            {
                return (float)Math.Pow(t / 0.08f, 0.7) * 0.5f;
            }
            else if (t > 0.92f)
            {
                return (float)Math.Pow((1f - t) / 0.08f, 1.3) * 0.6f;
            }
            else
            {
                // Middle section with subtle sinusoidal variation for natural brush texture
                float variation = (float)Math.Sin(t * Math.PI * 3.7) * 0.12f;
                return 1f + variation;
            }
        }

        // PerlinNoise
        // Generates simplified Perlin-like noise for smooth organic variation.
        private float PerlinNoise(float x)
        {
            int xi = (int)Math.Floor(x);
            float xf = x - xi;

            // Smoothstep interpolation for natural transitions
            float u = xf * xf * (3f - 2f * xf);

            float a = Hash(xi);
            float b = Hash(xi + 1);

            return a * (1f - u) + b * u;
        }

        // Hash
        // Generates a pseudo-random hash value for consistent noise generation.
        private float Hash(int x)
        {
            // Integer hash function for deterministic pseudo-random values
            x = (x << 13) ^ x;
            return ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f - 1f;
        }
    }
}
