using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
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
}
