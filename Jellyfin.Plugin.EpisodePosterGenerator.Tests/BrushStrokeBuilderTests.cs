using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using SkiaSharp;
using Xunit;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Tests;

/// <summary>
/// Tests for <see cref="BrushStrokeBuilder"/>, which is seeded so the same seed always produces the
/// same stroke path.
/// </summary>
public class BrushStrokeBuilderTests
{
    private static readonly SKRect Bounds = SKRect.Create(0, 0, 1000, 1500);
    private static readonly SKRect TextArea = SKRect.Create(100, 1200, 800, 200);

    [Fact]
    public void BuildStrokePath_SameSeed_ProducesIdenticalPath()
    {
        using var first = new BrushStrokeBuilder(42).BuildStrokePath(Bounds, TextArea, 1500f);
        using var second = new BrushStrokeBuilder(42).BuildStrokePath(Bounds, TextArea, 1500f);

        Assert.True(first.PointCount > 0);
        Assert.Equal(first.PointCount, second.PointCount);
        Assert.Equal(first.Bounds, second.Bounds);
    }

    [Fact]
    public void BuildStrokePath_DifferentSeed_ProducesDifferentPath()
    {
        using var first = new BrushStrokeBuilder(1).BuildStrokePath(Bounds, TextArea, 1500f);
        using var second = new BrushStrokeBuilder(2).BuildStrokePath(Bounds, TextArea, 1500f);

        Assert.NotEqual(first.Bounds, second.Bounds);
    }
}
