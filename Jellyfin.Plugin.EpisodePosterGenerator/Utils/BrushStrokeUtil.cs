using System;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils
{
    /// <summary>
    /// Generates realistic paint brush stroke paths programmatically.
    /// Creates organic, rough-edged strokes that mimic real paint brush textures.
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

            // Expand text area with buffer zone
            var textBuffer = bounds.Height * 0.08f;
            var textKeepClear = new SKRect(
                textArea.Left - textBuffer,
                textArea.Top - textBuffer,
                textArea.Right + textBuffer,
                textArea.Bottom + textBuffer
            );

            // Create 2-3 major brush strokes
            int strokeCount = 2 + _random.Next(2);

            for (int i = 0; i < strokeCount; i++)
            {
                var stroke = CreateBrushStroke(bounds, textKeepClear, i, strokeCount);
                if (stroke != null)
                {
                    combinedPath.AddPath(stroke);
                    stroke.Dispose();
                }
            }

            // Add edge texture - smaller strokes at the main stroke edges
            int edgeStrokeCount = 5 + _random.Next(10);
            for (int i = 0; i < edgeStrokeCount; i++)
            {
                var edgeStroke = CreateEdgeStroke(bounds, textKeepClear);
                if (edgeStroke != null)
                {
                    combinedPath.AddPath(edgeStroke);
                    edgeStroke.Dispose();
                }
            }

            // Add paint splatters
            int splatCount = 20 + _random.Next(30);
            for (int i = 0; i < splatCount; i++)
            {
                var splat = CreatePaintSplatter(bounds, textKeepClear);
                if (splat != null)
                {
                    combinedPath.AddPath(splat);
                    splat.Dispose();
                }
            }

            return combinedPath;
        }

        // MARK: CreateBrushStroke
        private SKPath CreateBrushStroke(SKRect bounds, SKRect textArea, int index, int total)
        {
            var path = new SKPath();

            // Position stroke vertically, staggered
            float baseY = bounds.Top + (bounds.Height * 0.25f) + (index / (float)total) * (bounds.Height * 0.2f);

            // Vary position randomly
            baseY += (float)(_random.NextDouble() - 0.5) * bounds.Height * 0.15f;

            // Avoid text area
            if (baseY + bounds.Height * 0.12f > textArea.Top)
            {
                baseY = textArea.Top - bounds.Height * 0.15f;
            }

            // Stroke extends beyond bounds for natural look
            float startX = bounds.Left - bounds.Width * 0.05f;
            float endX = bounds.Right + bounds.Width * 0.05f;

            // Create stroke using bezier curves for smoothness
            int segments = 40;
            var topPoints = new SKPoint[segments + 1];
            var bottomPoints = new SKPoint[segments + 1];

            // Random stroke characteristics
            float verticalWave = bounds.Height * (0.02f + (float)_random.NextDouble() * 0.04f);
            float baseHeight = bounds.Height * (0.06f + (float)_random.NextDouble() * 0.04f);

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = startX + (endX - startX) * t;

                // Smooth wave using multiple sine waves
                float wave = (float)(
                    Math.Sin(t * Math.PI * 2.3) * 0.5 +
                    Math.Sin(t * Math.PI * 4.7) * 0.3 +
                    Math.Sin(t * Math.PI * 1.1) * 0.2
                );
                float y = baseY + wave * verticalWave;

                // Width variation with natural tapering
                float widthMult = CalculateNaturalWidth(t);
                float halfHeight = baseHeight * widthMult * 0.5f;

                // Add organic edge variation using smooth noise
                float topEdge = PerlinNoise(t * 10f + index) * halfHeight * 0.3f;
                float bottomEdge = PerlinNoise(t * 10f + index + 100) * halfHeight * 0.3f;

                topPoints[i] = new SKPoint(x, y - halfHeight + topEdge);
                bottomPoints[i] = new SKPoint(x, y + halfHeight + bottomEdge);
            }

            // Build path with smooth curves
            path.MoveTo(topPoints[0]);

            // Use quadratic bezier for smoother curves
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

            // Add bottom edge in reverse
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

        // MARK: CreateEdgeStroke
        private SKPath? CreateEdgeStroke(SKRect bounds, SKRect textArea)
        {
            // Small jagged pieces at the edges for texture
            float x = bounds.Left + (float)_random.NextDouble() * bounds.Width;
            float y = bounds.Top + (float)_random.NextDouble() * (textArea.Top - bounds.Top);

            if (y > textArea.Top - bounds.Height * 0.05f)
                return null;

            var path = new SKPath();

            float length = bounds.Width * (0.02f + (float)_random.NextDouble() * 0.08f);
            float height = bounds.Height * (0.005f + (float)_random.NextDouble() * 0.02f);
            float angle = (float)_random.NextDouble() * 360f;

            // Create small irregular shape
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

        // MARK: CreatePaintSplatter
        private SKPath? CreatePaintSplatter(SKRect bounds, SKRect textArea)
        {
            float x = bounds.Left + (float)_random.NextDouble() * bounds.Width;
            float y = bounds.Top + (float)_random.NextDouble() * (textArea.Top - bounds.Top - bounds.Height * 0.05f);

            if (y > textArea.Top - bounds.Height * 0.08f)
                return null;

            // Most splatters are tiny
            float baseSize = bounds.Height * (0.002f + (float)_random.NextDouble() * 0.006f);

            // Occasional larger splatters
            if (_random.NextDouble() < 0.15)
                baseSize *= 2f;

            var path = new SKPath();

            // Irregular blob shape
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

            // Add tiny satellite drops
            if (_random.NextDouble() < 0.25)
            {
                float dropX = x + (float)(_random.NextDouble() - 0.5) * baseSize * 4f;
                float dropY = y + (float)(_random.NextDouble() - 0.5) * baseSize * 4f;
                path.AddCircle(dropX, dropY, baseSize * 0.25f);
            }

            return path;
        }

        // MARK: CalculateNaturalWidth
        private float CalculateNaturalWidth(float t)
        {
            // More natural tapering - gradual at start, sharper at end
            if (t < 0.08f)
            {
                // Gradual fade in
                return (float)Math.Pow(t / 0.08f, 0.7) * 0.5f;
            }
            else if (t > 0.92f)
            {
                // Sharp fade out like brush lift
                return (float)Math.Pow((1f - t) / 0.08f, 1.3) * 0.6f;
            }
            else
            {
                // Middle section with subtle variation
                float variation = (float)Math.Sin(t * Math.PI * 3.7) * 0.12f;
                return 1f + variation;
            }
        }

        // MARK: PerlinNoise
        private float PerlinNoise(float x)
        {
            // Simplified Perlin-like noise for smooth organic variation
            int xi = (int)Math.Floor(x);
            float xf = x - xi;

            // Smoothstep interpolation
            float u = xf * xf * (3f - 2f * xf);

            // Hash function for pseudo-random values
            float a = Hash(xi);
            float b = Hash(xi + 1);

            return a * (1f - u) + b * u;
        }

        // MARK: Hash
        private float Hash(int x)
        {
            // Simple hash for consistent pseudo-random values
            x = (x << 13) ^ x;
            return ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f - 1f;
        }
    }
}
