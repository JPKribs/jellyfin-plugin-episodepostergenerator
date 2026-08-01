using System.Globalization;
using System.Threading;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Xunit;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Tests;

/// <summary>
/// Tests for aspect ratio parsing in <see cref="CroppingService"/>. The ratio is free text in
/// the configuration page, so it has to parse identically regardless of the server's locale.
/// </summary>
public class CroppingServiceTests
{
    [Theory]
    [InlineData("16:9", 16f / 9f)]
    [InlineData("4:3", 4f / 3f)]
    [InlineData("1:1", 1f)]
    [InlineData("2.35:1", 2.35f)]
    [InlineData("1.85:1", 1.85f)]
    public void ParseAspectRatio_ParsesValidRatios(string ratio, float expected)
    {
        Assert.Equal(expected, CroppingService.ParseAspectRatio(ratio), 4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("16")]
    [InlineData("16:9:3")]
    [InlineData("wide:tall")]
    [InlineData("16:0")]
    [InlineData("0:9")]
    [InlineData("-16:9")]
    public void ParseAspectRatio_FallsBackToSixteenNineOnGarbage(string ratio)
    {
        Assert.Equal(16f / 9f, CroppingService.ParseAspectRatio(ratio), 4);
    }

    /// <summary>
    /// Under a comma-decimal locale, current-culture parsing reads "2.35" as 235, producing a
    /// 235:1 crop. Parsing must be culture-invariant.
    /// </summary>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("pt-BR")]
    public void ParseAspectRatio_IsCultureInvariant(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);

            Assert.Equal(2.35f, CroppingService.ParseAspectRatio("2.35:1"), 4);
            Assert.Equal(16f / 9f, CroppingService.ParseAspectRatio("16:9"), 4);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
