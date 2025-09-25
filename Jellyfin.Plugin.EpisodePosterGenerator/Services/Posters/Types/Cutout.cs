using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Generates a poster with a "cutout" style overlay for a given TV episode.
/// Creates dramatic effect by drawing episode information as transparent text cut out from a background overlay,
/// revealing the underlying image through the text shapes. Supports both text and code cutout types
/// with automatic multi-line formatting for longer episode descriptions.
/// Uses inherited safe area calculations for consistent margins across all poster generators.
/// </summary>
public class CutoutPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    /// <summary>
    /// Word separators used to split episode text into multiple lines for better formatting.
    /// Includes space and hyphen characters for natural text breaking points.
    /// </summary>
    private static readonly char[] WordSeparators = { ' ', '-' };

    /// <summary>
    /// Generates a poster image using a cutout text overlay and saves it to the specified output path.
    /// Creates a layered composition with background overlay, transparent cutout text, and optional title.
    /// The cutout effect allows the underlying image to show through the text shapes.
    /// Uses BasePosterGenerator safe area constraints for consistent positioning with other poster styles.
    /// </summary>
    /// <param name="inputImagePath">Path to the base input image to use as background.</param>
    /// <param name="outputPath">Path where the generated poster will be saved.</param>
    /// <param name="episode">Episode metadata object containing season, episode, and title information.</param>
    /// <param name="config">Plugin configuration used to control visual settings and cutout type.</param>
    /// <returns>Path to the saved poster image, or null if generation fails.</returns>
    // MARK: Generate
    public string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            var seasonNumber = episode.ParentIndexNumber ?? 0;
            var episodeNumber = episode.IndexNumber ?? 0;
            var episodeTitle = episode.Name ?? "-";

            // Generate cutout text using centralized utility for consistent formatting
            var episodeText = EpisodeCodeUtil.FormatEpisodeText(config.CutoutType, seasonNumber, episodeNumber);
            var episodeWords = episodeText.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

            // Decode the input image
            using var original = SKBitmap.Decode(inputImagePath);
            if (original == null)
                return null;

            var width = original.Width;
            var height = original.Height;
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            // Start with transparent canvas
            canvas.Clear(SKColors.Transparent);

            // Draw background color overlay first
            var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Src
            };
            canvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            // Draw cutout text using clear blend mode to create transparency
            DrawCutoutText(canvas, episodeWords, width, height, config.ShowTitle, config, overlayColor);

            // Draw original image behind the overlay using destination-over blending
            // This makes the image visible through the transparent cutout areas
            using var originalPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstOver
            };
            canvas.DrawBitmap(original, 0, 0, originalPaint);

            // Add episode title if enabled using TextUtils for consistency
            if (config.ShowTitle)
            {
                TextUtils.DrawTitle(canvas, episodeTitle, Position.Bottom, Alignment.Center, config, width, height);
            }

            // Encode and save the final composite image
            using var finalImage = surface.Snapshot();
            using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 95);
            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Poster generation failed: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Draws episode text (cutout style) on the canvas using transparent blend mode for cutout effect.
    /// Adds a subtle contrasting border around the cutout text for better visibility against any background.
    /// Calculates optimal positioning and font sizing based on title presence and available space.
    /// Uses title-aware layout to prevent overlapping and maximize font size within constraints.
    /// </summary>
    /// <param name="canvas">Canvas to draw the cutout text on.</param>
    /// <param name="episodeWords">Array of words to render as cutout text.</param>
    /// <param name="canvasWidth">Total canvas width for positioning calculations.</param>
    /// <param name="canvasHeight">Total canvas height for positioning calculations.</param>
    /// <param name="hasTitle">Whether episode title will be displayed, affecting text positioning.</param>
    /// <param name="config">Plugin configuration with font and styling settings.</param>
    /// <param name="overlayColor">The overlay background color used to determine contrasting border color.</param>
    // MARK: DrawCutoutText
    private void DrawCutoutText(SKCanvas canvas, string[] episodeWords, float canvasWidth, float canvasHeight, bool hasTitle, PluginConfiguration config, SKColor overlayColor)
    {
        if (episodeWords.Length == 0)
            return;

        // Use same safe area calculation as other poster generators
        ApplySafeAreaConstraints((int)canvasWidth, (int)canvasHeight, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

        var cutoutArea = CalculateCutoutArea(canvasWidth, canvasHeight, hasTitle, config, safeLeft, safeTop, safeWidth, safeHeight);

        var fontStyle = FontUtils.GetFontStyle(config.EpisodeFontStyle);
        using var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, fontStyle);

        float optimalFontSize = CalculateOptimalCutoutFontSize(episodeWords, typeface, cutoutArea);

        if (config.CutoutBorder)
        {
            // Calculate contrasting border color based on overlay color
            var borderColor = GetContrastingBorderColor(overlayColor);

            // Calculate contrasting border width
            var borderWidth = Math.Max(1f, optimalFontSize * 0.015f);

            // First, draw the border (stroke) around the text
            using var borderPaint = new SKPaint
            {
                Color = borderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth,
                IsAntialias = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center,
                TextSize = optimalFontSize
            };

            DrawCutoutTextCentered(canvas, episodeWords, borderPaint, cutoutArea);
        }

        // Then draw the cutout text using clear blend mode to create transparency
        using var cutoutPaint = new SKPaint
        {
            Color = SKColors.Transparent,
            BlendMode = SKBlendMode.Clear,
            IsAntialias = true,
            Typeface = typeface,
            TextAlign = SKTextAlign.Center,
            TextSize = optimalFontSize
        };

        DrawCutoutTextCentered(canvas, episodeWords, cutoutPaint, cutoutArea);
    }

    /// <summary>
    /// Determines the contrasting border color based on the overlay color luminance.
    /// Uses black for light overlays and white for dark overlays to ensure maximum visibility.
    /// </summary>
    /// <param name="overlayColor">The overlay background color.</param>
    /// <returns>Contrasting border color (black, white, or gray).</returns>
    // MARK: GetContrastingBorderColor
    private SKColor GetContrastingBorderColor(SKColor overlayColor)
    {
        // Calculate relative luminance using standard formula
        float r = overlayColor.Red / 255f;
        float g = overlayColor.Green / 255f;
        float b = overlayColor.Blue / 255f;

        // Apply gamma correction
        r = r <= 0.03928f ? r / 12.92f : (float)Math.Pow((r + 0.055f) / 1.055f, 2.4f);
        g = g <= 0.03928f ? g / 12.92f : (float)Math.Pow((g + 0.055f) / 1.055f, 2.4f);
        b = b <= 0.03928f ? b / 12.92f : (float)Math.Pow((b + 0.055f) / 1.055f, 2.4f);

        float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;

        // Use black border for light backgrounds, white for dark backgrounds
        if (luminance > 0.5f)
        {
            return SKColors.Black;
        }
        else if (luminance < 0.2f)
        {
            return SKColors.White;
        }
        else
        {
            // For medium luminance, use dark gray for better subtlety
            return new SKColor(64, 64, 64); // Dark gray
        }
    }

    /// <summary>
    /// Calculates the available rectangular area for cutout text based on title presence and safe areas.
    /// When title is present, reserves space at bottom plus safe area buffer to prevent overlapping.
    /// </summary>
    /// <param name="canvasWidth">Total canvas width.</param>
    /// <param name="canvasHeight">Total canvas height.</param>
    /// <param name="hasTitle">Whether title will be displayed at bottom.</param>
    /// <param name="config">Plugin configuration for safe area and font calculations.</param>
    /// <param name="safeLeft">Left boundary of safe area.</param>
    /// <param name="safeTop">Top boundary of safe area.</param>
    /// <param name="safeWidth">Width of safe area.</param>
    /// <param name="safeHeight">Height of safe area.</param>
    /// <returns>Rectangle defining available area for cutout text.</returns>
    // MARK: CalculateCutoutArea
    private SKRect CalculateCutoutArea(float canvasWidth, float canvasHeight, bool hasTitle, PluginConfiguration config, float safeLeft, float safeTop, float safeWidth, float safeHeight)
    {
        if (!hasTitle)
        {
            return new SKRect(safeLeft, safeTop, safeLeft + safeWidth, safeTop + safeHeight);
        }

        // Calculate title space requirements
        var titleFontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight, (100f * GetSafeAreaMargin(config)));
        var titleLineHeight = titleFontSize * 1.2f;
        var titleTotalHeight = titleLineHeight * 2; // Max 2 lines

        // Reserve space for title at bottom
        var titleSpaceFromBottom = (canvasHeight * GetSafeAreaMargin(config)) + titleTotalHeight;
        var cutoutTitleBuffer = canvasHeight * 0.05f; // 5% buffer

        var availableHeight = canvasHeight - safeTop - titleSpaceFromBottom - cutoutTitleBuffer;
        availableHeight = Math.Max(availableHeight, canvasHeight * 0.3f);

        var cutoutBottom = safeTop + availableHeight;

        return new SKRect(safeLeft, safeTop, safeLeft + safeWidth, cutoutBottom);
    }

    /// <summary>
    /// Calculates the optimal font size for cutout text to fit within the available area.
    /// Ensures all words use the same font size and fit together within the specified bounds.
    /// </summary>
    /// <param name="episodeWords">Array of words to size.</param>
    /// <param name="typeface">Typeface to use for measurements.</param>
    /// <param name="availableArea">Rectangle defining available space for text.</param>
    /// <returns>Optimal font size that fits all words within the area.</returns>
    // MARK: CalculateOptimalCutoutFontSize
    private float CalculateOptimalCutoutFontSize(string[] episodeWords, SKTypeface typeface, SKRect availableArea)
    {
        var maxWidth = availableArea.Width;
        var maxHeight = availableArea.Height;

        if (episodeWords.Length == 1)
        {
            // Single word: use maximum font size that fits
            return FontUtils.CalculateOptimalFontSize(episodeWords[0], typeface, maxWidth, maxHeight, 50f);
        }
        else
        {
            // Multiple words: find largest font where all words fit with stacking
            float lineSpacing = 1.1f;
            float maxFontSize = maxHeight / (episodeWords.Length * lineSpacing);
            const float minFontSize = 30f;

            // Binary search for optimal font size
            float low = minFontSize;
            float high = maxFontSize;
            float optimalSize = minFontSize;

            while (high - low > 1f)
            {
                float testSize = (low + high) / 2f;

                if (DoAllWordsFit(episodeWords, typeface, testSize, maxWidth, maxHeight, lineSpacing))
                {
                    optimalSize = testSize;
                    low = testSize;
                }
                else
                {
                    high = testSize;
                }
            }

            return optimalSize;
        }
    }

    /// <summary>
    /// Tests whether all words fit within the specified dimensions at a given font size.
    /// </summary>
    /// <param name="words">Words to test.</param>
    /// <param name="typeface">Typeface for measurements.</param>
    /// <param name="fontSize">Font size to test.</param>
    /// <param name="maxWidth">Maximum allowed width.</param>
    /// <param name="maxHeight">Maximum allowed height.</param>
    /// <param name="lineSpacing">Line spacing multiplier.</param>
    /// <returns>True if all words fit within the dimensions.</returns>
    // MARK: DoAllWordsFit
    private bool DoAllWordsFit(string[] words, SKTypeface typeface, float fontSize, float maxWidth, float maxHeight, float lineSpacing)
    {
        float maxWordWidth = 0;
        
        foreach (var word in words)
        {
            var bounds = FontUtils.MeasureTextDimensions(word, typeface, fontSize);
            if (bounds.Width > maxWordWidth)
                maxWordWidth = bounds.Width;
        }

        float totalHeight = words.Length * fontSize * lineSpacing;
        
        return maxWordWidth <= maxWidth && totalHeight <= maxHeight;
    }

    /// <summary>
    /// Draws cutout text centered within the specified area.
    /// Handles both single and multiple words with appropriate vertical centering.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="episodeWords">Words to render.</param>
    /// <param name="paint">Paint configured for rendering (either border or cutout).</param>
    /// <param name="availableArea">Area to center text within.</param>
    // MARK: DrawCutoutTextCentered
    private void DrawCutoutTextCentered(SKCanvas canvas, string[] episodeWords, SKPaint paint, SKRect availableArea)
    {
        var centerX = availableArea.MidX;
        var centerY = availableArea.MidY;

        if (episodeWords.Length == 1)
        {
            // Single word: center directly
            var bounds = FontUtils.MeasureTextDimensions(episodeWords[0], paint.Typeface!, paint.TextSize);
            var textY = centerY + (bounds.Height / 2f);
            canvas.DrawText(episodeWords[0], centerX, textY, paint);
        }
        else
        {
            // Multiple words: stack vertically and center the block
            float lineSpacing = 1.1f;
            float lineHeight = paint.TextSize * lineSpacing;
            float totalTextHeight = episodeWords.Length * lineHeight - (lineHeight - paint.TextSize);
            
            float startY = centerY - (totalTextHeight / 2f) + paint.TextSize;
            
            foreach (var word in episodeWords)
            {
                canvas.DrawText(word, centerX, startY, paint);
                startY += lineHeight;
            }
        }
    }
}