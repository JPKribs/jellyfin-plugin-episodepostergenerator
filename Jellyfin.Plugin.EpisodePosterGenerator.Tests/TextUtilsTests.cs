using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using SkiaSharp;
using Xunit;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Tests;

/// <summary>
/// Tests for the long title helpers in <see cref="TextUtils"/>.
/// </summary>
public class TextUtilsTests
{
    [Theory]
    [InlineData("Ancient History - The Harley Passed Down by Elbaph", "Ancient History")]
    [InlineData("Spider-Man: No Way Home", "Spider-Man")]
    [InlineData("The Power That Burns Fire - Akainu's Final Move", "The Power That Burns Fire")]
    [InlineData("A Title With No Divider", null)]
    [InlineData("Well-Meaning Hyphenated Words", null)]
    public void LeftOfSeparator_ReturnsTextBeforeTheDivider(string title, string? expected)
    {
        Assert.Equal(expected, TextUtils.LeftOfSeparator(title));
    }

    [Theory]
    [InlineData("I'm Luffy! The Man Who's Gonna Be King of the Pirates!", "I'm Luffy!")]
    [InlineData("Who Are You? I Am Me.", "Who Are You?")]
    [InlineData("A Single Sentence Title", null)]
    [InlineData("Mr. Smith Goes to Washington", null)]
    public void FirstSentence_ReturnsTheFirstSentence(string title, string? expected)
    {
        Assert.Equal(expected, TextUtils.FirstSentence(title));
    }

    [Fact]
    public void FitAbbreviation_DropsMiddleInitialsThenGivesUp()
    {
        using var paint = new SkiaSharp.SKPaint { TextSize = 20 };

        var full = "T.P.T.B.F. - A.F.M.";
        Assert.Equal(full, TextUtils.FitAbbreviation(full, paint, paint.MeasureText(full)));

        var reduced = TextUtils.FitAbbreviation(full, paint, paint.MeasureText("T.M."));
        Assert.Equal("T.M.", reduced);

        Assert.Null(TextUtils.FitAbbreviation(full, paint, 1f));
    }

    [Theory]
    [InlineData("Lord of the Ring", "L.O.T.R.")]
    [InlineData("The Power that Burns Fire - Akainu's Final Move", "T.P.T.B.F. - A.F.M.")]
    [InlineData("Ancient History: The Harley", "A.H.: T.H.")]
    [InlineData("all lowercase words", "A.L.W.")]
    [InlineData("some show - all lowercase everywhere", "S.S. - A.L.E.")]
    [InlineData("Hone Your Moving Fastball", "H.Y.M.F.")]
    public void AbbreviateTitle_UsesEveryWordWithPeriodsAndKeepsDividers(string title, string expected)
    {
        Assert.Equal(expected, TextUtils.AbbreviateTitle(title));
    }

    /// <summary>
    /// The width-only fit can return two lines for a slot that only has room for one, which is why
    /// styles used to reserve a fixed two lines regardless. The height-aware overload is what lets
    /// them reserve what is actually drawn.
    /// </summary>
    [Fact]
    public void FitTitleLines_RespectsTheHeightItIsGiven()
    {
        using var paint = new SKPaint { TextSize = 20f };
        const string longTitle = "A Fairly Long Episode Title That Wraps";

        var twoLines = TextUtils.FitTitleLines(longTitle, paint, 150f, LongTitleHandling.Ellipsis);
        Assert.True(twoLines.Count > 1, "precondition: this title should wrap at that width");

        // Room for one line only.
        var oneLine = TextUtils.FitTitleLines(longTitle, paint, 150f, 24f, 24f, LongTitleHandling.Ellipsis);
        Assert.Single(oneLine);

        // Room for two.
        var fits = TextUtils.FitTitleLines(longTitle, paint, 150f, 60f, 24f, LongTitleHandling.Ellipsis);
        Assert.Equal(twoLines.Count, fits.Count);
    }

    [Fact]
    public void FitTitleLines_HeightAware_NeverExceedsTheAllowedLineCount()
    {
        using var paint = new SKPaint { TextSize = 20f };
        const string longTitle = "An Extremely Long Episode Title That Will Wrap Several Times Over";

        foreach (var handling in new[] { LongTitleHandling.Ellipsis, LongTitleHandling.Abbreviate, LongTitleHandling.DropName })
        {
            var lines = TextUtils.FitTitleLines(longTitle, paint, 120f, 24f, 24f, handling);
            Assert.True(lines.Count <= 1, $"{handling} returned {lines.Count} lines for a one line slot");
        }
    }

    [Fact]
    public void FitTitleLines_HeightAware_IsAPassThroughWhenHeightIsUnconstrained()
    {
        using var paint = new SKPaint { TextSize = 20f };
        const string title = "Short Title";

        var plain = TextUtils.FitTitleLines(title, paint, 500f, LongTitleHandling.Ellipsis);
        var sized = TextUtils.FitTitleLines(title, paint, 500f, 0f, 0f, LongTitleHandling.Ellipsis);

        Assert.Equal(plain, sized);
    }

    /// <summary>
    /// A run of n lines occupies fontSize + (n-1) * lineHeight. Dividing the block height by
    /// lineHeight undercounts, which silently collapsed two line titles down to one.
    /// </summary>
    [Theory]
    [InlineData(1.0f, 1)]    // room for exactly one line
    [InlineData(2.2f, 2)]    // the styles' fixed "two line" reservation
    [InlineData(3.4f, 3)]
    public void FitTitleLines_CountsLinesTheWayTheStylesDrawThem(float zoneInFontSizes, int expectedMaxLines)
    {
        using var paint = new SKPaint { TextSize = 20f };
        var fontSize = paint.TextSize;
        var lineHeight = fontSize * 1.2f;
        const string longTitle = "One Two Three Four Five Six Seven Eight Nine Ten Eleven Twelve";

        var lines = TextUtils.FitTitleLines(
            longTitle, paint, 90f, fontSize * zoneInFontSizes, lineHeight, LongTitleHandling.Ellipsis);

        Assert.True(
            lines.Count <= expectedMaxLines,
            $"zone of {zoneInFontSizes}x fontSize produced {lines.Count} lines, expected at most {expectedMaxLines}");
    }
}
