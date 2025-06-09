using SkiaSharp;
using System;
using System.Globalization;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

public static class ColorUtils
{
    public static SKColor ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return SKColors.White;

        hex = hex.TrimStart('#');
        var span = hex.AsSpan();
        var style = NumberStyles.HexNumber;
        var culture = CultureInfo.InvariantCulture;

        return span.Length switch
        {
            8 => new SKColor(
                byte.Parse(span.Slice(2, 2), style, culture),
                byte.Parse(span.Slice(4, 2), style, culture),
                byte.Parse(span.Slice(6, 2), style, culture),
                byte.Parse(span.Slice(0, 2), style, culture)
            ),
            6 => new SKColor(
                byte.Parse(span.Slice(0, 2), style, culture),
                byte.Parse(span.Slice(2, 2), style, culture),
                byte.Parse(span.Slice(4, 2), style, culture)
            ),
            _ => SKColors.White
        };
    }
}