using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Comprehensive utility class for rendering text on poster canvases with advanced positioning, alignment, wrapping, and visual effects.
/// Provides sophisticated text fitting algorithms and layout calculations to ensure optimal text display within specified boundaries
/// while maintaining visual consistency across different poster styles and content lengths.
/// 
/// The TextUtils class handles the complex mathematics of text layout including:
/// - Automatic text wrapping with optimal line breaking algorithms
/// - Safe area calculations respecting poster margins and layout constraints
/// - Font metrics and baseline calculations for precise positioning
/// - Shadow effects and visual enhancements for improved readability
/// - Multi-line text balancing to create visually appealing layouts
/// 
/// This utility is designed to work seamlessly with SkiaSharp's rendering pipeline and integrates
/// with the plugin's configuration system to provide consistent text styling across all poster types.
/// All methods are optimized for performance and memory efficiency during batch poster generation.
/// </summary>
public static class TextUtils
{
    /// <summary>
    /// Pixel offset for drop shadow effects applied to all text rendering operations.
    /// This constant provides a subtle shadow that enhances text readability against various background images
    /// without being overly prominent. The 2-pixel offset provides good visibility while maintaining subtlety.
    /// </summary>
    private const float ShadowOffset = 2f;

    /// <summary>
    /// Alpha transparency value for drop shadow effects, providing semi-transparent black shadows.
    /// Value of 180 (approximately 70% opacity) creates visible shadows that enhance text readability
    /// without overpowering the main text or creating visual noise. This balance works well across
    /// different background image types and color schemes.
    /// </summary>
    private const byte ShadowAlpha = 180;

    /// <summary>
    /// Line spacing multiplier for multi-line text rendering, controlling vertical space between text lines.
    /// A value of 1.2 provides 20% additional space beyond the font height, creating comfortable reading
    /// spacing that prevents lines from appearing cramped while maintaining compact layouts suitable
    /// for poster designs with limited vertical space.
    /// </summary>
    private const float LineSpacingMultiplier = 1.2f;

    /// <summary>
    /// Renders title text on a canvas with comprehensive layout management including automatic text wrapping,
    /// precise positioning, and professional shadow effects. This method serves as the primary entry point
    /// for all text rendering operations within the poster generation system.
    /// 
    /// The rendering process includes:
    /// 1. Font configuration and typeface creation based on user preferences
    /// 2. Safe area calculation to respect poster margins and layout constraints
    /// 3. Intelligent text wrapping with optimal line breaking for visual balance
    /// 4. Precise positioning calculations using font metrics and baseline alignment
    /// 5. Professional drop shadow rendering for enhanced readability
    /// 6. Multi-line text layout with proper line spacing and alignment
    /// 
    /// Text is automatically constrained to a maximum of two lines with intelligent ellipsis truncation
    /// when content exceeds available space. The wrapping algorithm attempts to create balanced line
    /// lengths while respecting word boundaries for optimal readability.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas object for text rendering operations.</param>
    /// <param name="title">The text content to render, which will be automatically wrapped if necessary.</param>
    /// <param name="position">Vertical positioning strategy (Top, Center, Bottom) within the safe area boundaries.</param>
    /// <param name="alignment">Horizontal alignment preference (Left, Center, Right) for text positioning.</param>
    /// <param name="config">Plugin configuration containing font preferences, sizes, colors, and layout settings.</param>
    /// <param name="canvasWidth">Total canvas width in pixels for positioning and constraint calculations.</param>
    /// <param name="canvasHeight">Total canvas height in pixels for font sizing and safe area calculations.</param>
    // MARK: DrawTitle
    public static void DrawTitle(
        SKCanvas canvas,
        string title,
        Position position,
        Alignment alignment,
        PosterSettings settings,
        float canvasWidth,
        float canvasHeight)
    {
        // Early exit for empty or whitespace-only content to avoid unnecessary processing
        if (string.IsNullOrWhiteSpace(title))
            return;

        // Calculate responsive font size based on canvas height and safe area constraints
        // This ensures text scales appropriately across different poster dimensions
        var fontSize = FontUtils.CalculateFontSizeFromPercentage(settings.TitleFontSize, canvasHeight, settings.PosterSafeArea);
        
        // Create typeface with user-configured font family and style preferences
        var typeface = FontUtils.CreateTypeface(settings.TitleFontFamily, FontUtils.GetFontStyle(settings.TitleFontStyle));

        // Set up paint objects for main text and shadow rendering with proper resource management
        using var titlePaint = CreateTextPaint(settings.TitleFontColor, fontSize, typeface, alignment);
        using var shadowPaint = CreateShadowPaint(fontSize, typeface, alignment);

        // Calculate safe drawing area that respects poster margins and layout constraints
        var safeArea = CalculateSafeArea(canvasWidth, canvasHeight, settings);
        
        // Determine maximum text width considering horizontal padding within the safe area
        // This ensures text doesn't extend too close to the safe area boundaries
        var safeAreaMargin = settings.PosterSafeArea / 100f;
        var horizontalPadding = 1.0f - (2 * safeAreaMargin);
        var maxTextWidth = safeArea.Width * horizontalPadding;
        
        // Perform intelligent text wrapping with optimal line breaking algorithms
        var lines = FitTextToWidth(title, titlePaint, maxTextWidth);
        
        // Calculate total text block dimensions for precise positioning
        var textBounds = CalculateTextBounds(lines, titlePaint, fontSize);
        
        // Determine horizontal and vertical positioning based on alignment preferences
        var alignmentX = CalculateAlignmentX(alignment, canvasWidth, safeArea);
        var baseY = CalculateBaseY(position, safeArea, textBounds.Height, fontSize, titlePaint);
        
        // Render the text with shadow effects and proper line spacing
        DrawTextLines(canvas, lines, alignmentX, baseY, fontSize, titlePaint, shadowPaint);
    }

    /// <summary>
    /// Intelligently fits text within specified width constraints using advanced wrapping algorithms and ellipsis truncation.
    /// This method implements a sophisticated text layout system that attempts to create visually balanced line breaks
    /// while respecting word boundaries and maintaining readability.
    /// 
    /// The fitting algorithm follows this priority order:
    /// 1. Single line rendering if text fits within width constraints
    /// 2. Optimal two-line wrapping with balanced line lengths
    /// 3. Ellipsis truncation for lines that exceed width after wrapping
    /// 4. Emergency fallback to ellipsis-only if no characters fit
    /// 
    /// For multi-word content, the algorithm finds the optimal split point that minimizes the visual
    /// difference between line lengths while ensuring both lines fit within the specified width.
    /// This creates more visually appealing layouts compared to simple word-wrapping approaches.
    /// </summary>
    /// <param name="text">The input text content to fit within width constraints.</param>
    /// <param name="paint">Configured SKPaint object used for accurate text measurement and rendering.</param>
    /// <param name="maxWidth">Maximum allowed width in pixels for each line of text.</param>
    /// <returns>A read-only list of text lines optimized for the specified width constraints.</returns>
    // MARK: FitTextToWidth
    public static IReadOnlyList<string> FitTextToWidth(string text, SKPaint paint, float maxWidth)
    {
        var lines = new List<string>();
        
        // Optimization: if text fits on one line, return immediately without processing
        if (paint.MeasureText(text) <= maxWidth)
        {
            lines.Add(text);
            return lines;
        }

        // Split text into words for intelligent wrapping (handles multiple whitespace types)
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Handle single long words that exceed width by truncating with ellipsis
        if (words.Length == 1)
        {
            lines.Add(TruncateWithEllipsis(text, paint, maxWidth));
            return lines;
        }

        // Initialize line variables for two-line layout processing
        string line1 = "";
        string line2 = "";
        
        // Find the optimal point to split words between two lines for visual balance
        int splitPoint = FindOptimalSplitPoint(words, paint, maxWidth);
        
        // Distribute words between the two lines based on calculated split point
        for (int i = 0; i < words.Length; i++)
        {
            if (i < splitPoint)
            {
                // Build first line with proper spacing between words
                line1 += (i > 0 ? " " : "") + words[i];
            }
            else
            {
                // Build second line with proper spacing between words
                line2 += (i > splitPoint ? " " : "") + words[i];
            }
        }

        // Clean up any extra whitespace from line construction
        line1 = line1.Trim();
        line2 = line2.Trim();

        // Apply ellipsis truncation to first line if it still exceeds width after wrapping
        if (paint.MeasureText(line1) > maxWidth)
        {
            line1 = TruncateWithEllipsis(line1, paint, maxWidth);
        }
        
        // Handle second line: add if it exists and fits, otherwise truncate
        if (!string.IsNullOrWhiteSpace(line2))
        {
            // Apply ellipsis truncation to second line if necessary
            if (paint.MeasureText(line2) > maxWidth)
            {
                line2 = TruncateWithEllipsis(line2, paint, maxWidth);
            }
            lines.Add(line1);
            lines.Add(line2);
        }
        else
        {
            // Only one line needed after processing
            lines.Add(line1);
        }

        return lines;
    }

    /// <summary>
    /// Creates a properly configured SKPaint object for main text rendering with specified visual properties.
    /// This method centralizes paint object creation to ensure consistent text rendering across all poster styles
    /// and provides proper resource management through the using pattern in calling methods.
    /// 
    /// The paint object is configured with anti-aliasing for smooth text rendering and includes
    /// color parsing from hex color codes, font configuration, and text alignment settings.
    /// </summary>
    /// <param name="hexColor">Hex color code string (e.g., "#FFFFFF") for text color specification.</param>
    /// <param name="fontSize">Font size in pixels for text rendering.</param>
    /// <param name="typeface">Configured SKTypeface object containing font family and style information.</param>
    /// <param name="alignment">Text alignment setting that controls horizontal text positioning.</param>
    /// <returns>A fully configured SKPaint object ready for text rendering operations.</returns>
    // MARK: CreateTextPaint
    private static SKPaint CreateTextPaint(string hexColor, int fontSize, SKTypeface typeface, Alignment alignment)
    {
        return new SKPaint
        {
            Color = ColorUtils.ParseHexColor(hexColor),
            TextSize = fontSize,
            IsAntialias = true,  // Enable anti-aliasing for smooth text rendering
            Typeface = typeface,
            TextAlign = GetSKTextAlign(alignment)
        };
    }

    /// <summary>
    /// Creates a specialized SKPaint object for drop shadow text rendering with semi-transparent black coloring.
    /// Shadow paint objects use the same font and size settings as the main text but with reduced opacity
    /// to create subtle depth effects that enhance text readability against complex backgrounds.
    /// 
    /// The shadow paint is designed to be rendered first (underneath the main text) with a slight offset
    /// to create the visual impression of depth without overwhelming the primary text content.
    /// </summary>
    /// <param name="fontSize">Font size in pixels, matching the main text for consistent shadow appearance.</param>
    /// <param name="typeface">Font typeface matching the main text for consistent character shapes.</param>
    /// <param name="alignment">Text alignment matching the main text for proper shadow positioning.</param>
    /// <returns>A configured SKPaint object optimized for drop shadow text rendering.</returns>
    // MARK: CreateShadowPaint
    private static SKPaint CreateShadowPaint(int fontSize, SKTypeface typeface, Alignment alignment)
    {
        return new SKPaint
        {
            Color = SKColors.Black.WithAlpha(ShadowAlpha),  // Semi-transparent black for subtle shadows
            TextSize = fontSize,
            IsAntialias = true,  // Enable anti-aliasing for smooth shadow rendering
            Typeface = typeface,
            TextAlign = GetSKTextAlign(alignment)
        };
    }

    /// <summary>
    /// Converts the plugin's TextAlignment enumeration values to corresponding SkiaSharp text alignment constants.
    /// This translation layer allows the plugin to use its own alignment terminology while properly interfacing
    /// with SkiaSharp's rendering system. The method provides a centralized mapping that ensures consistent
    /// alignment behavior across all text rendering operations.
    /// </summary>
    /// <param name="alignment">Plugin-defined text alignment enumeration value.</param>
    /// <returns>Corresponding SkiaSharp SKTextAlign enumeration value for rendering operations.</returns>
    // MARK: GetSKTextAlign
    private static SKTextAlign GetSKTextAlign(Alignment alignment)
    {
        return alignment switch
        {
            Alignment.Left => SKTextAlign.Left,
            Alignment.Center => SKTextAlign.Center,
            Alignment.Right => SKTextAlign.Right,
            _ => SKTextAlign.Center  // Default to center alignment for unknown values
        };
    }

    /// <summary>
    /// Calculates the precise horizontal pixel position for text rendering based on alignment preferences and safe area boundaries.
    /// This method handles the mathematical conversion from abstract alignment concepts (left, center, right) to specific
    /// pixel coordinates that SkiaSharp can use for text positioning.
    /// 
    /// The calculation considers both the total canvas width and the safe area boundaries to ensure text
    /// is positioned appropriately within the layout constraints while respecting the user's alignment preferences.
    /// </summary>
    /// <param name="alignment">Desired horizontal text alignment within the canvas.</param>
    /// <param name="canvasWidth">Total canvas width in pixels for center alignment calculations.</param>
    /// <param name="safeArea">Safe drawing area rectangle defining layout boundaries and constraints.</param>
    /// <returns>Horizontal X coordinate in pixels for text rendering operations.</returns>
    // MARK: CalculateAlignmentX
    private static float CalculateAlignmentX(Alignment alignment, float canvasWidth, SKRect safeArea)
    {
        return alignment switch
        {
            Alignment.Left => safeArea.Left,     // Align to left edge of safe area
            Alignment.Center => canvasWidth / 2f, // Center on full canvas width
            Alignment.Right => safeArea.Right,   // Align to right edge of safe area
            _ => canvasWidth / 2f                    // Default to center for unknown alignments
        };
    }

    /// <summary>
    /// Calculates the precise vertical baseline position for text rendering using font metrics and positioning preferences.
    /// This method performs complex calculations involving font ascent/descent values and safe area boundaries
    /// to determine the exact pixel position where text baselines should be placed for optimal visual appearance.
    /// 
    /// The calculation accounts for:
    /// - Font metrics (ascent, descent) for accurate baseline positioning
    /// - Safe area boundaries for proper layout constraint adherence
    /// - Text block height for multi-line content positioning
    /// - Bottom padding for visual breathing room in bottom-aligned text
    /// 
    /// Baseline positioning is critical for text rendering as it determines where SkiaSharp places
    /// the character glyphs relative to the drawing coordinate system.
    /// </summary>
    /// <param name="position">Desired vertical text position (Top, Center, Bottom) within safe area.</param>
    /// <param name="safeArea">Safe drawing area rectangle defining vertical layout boundaries.</param>
    /// <param name="textHeight">Total height of the text block including line spacing for multi-line content.</param>
    /// <param name="fontSize">Font size in pixels for padding calculations.</param>
    /// <param name="paint">Paint object containing font metrics for precise baseline calculations.</param>
    /// <returns>Vertical Y coordinate in pixels for text baseline positioning in SkiaSharp rendering.</returns>
    // MARK: CalculateBaseY
    private static float CalculateBaseY(Position position, SKRect safeArea, float textHeight, int fontSize, SKPaint paint)
    {
        var fontMetrics = paint.FontMetrics;
        float bottomPadding = fontSize * 0.5f;  // Add breathing room for bottom-aligned text
        
        return position switch
        {
            // Top position: align text to top of safe area, accounting for font ascent
            Position.Top => safeArea.Top - fontMetrics.Ascent,
            
            // Center position: position text block center at safe area center
            Position.Center => safeArea.MidY - (textHeight / 2f) - fontMetrics.Ascent,
            
            // Bottom position: align text above safe area bottom with padding
            Position.Bottom => safeArea.Bottom - textHeight - bottomPadding - fontMetrics.Ascent,
            
            // Default to bottom positioning for unknown position values
            _ => safeArea.Bottom - textHeight - bottomPadding - fontMetrics.Ascent
        };
    }

    /// <summary>
    /// Calculates the safe drawing area rectangle within the canvas boundaries, accounting for margin padding configuration.
    /// The safe area represents the region where text and other visual elements should be placed to ensure they
    /// don't appear too close to canvas edges, maintaining professional poster layouts with appropriate visual breathing room.
    /// 
    /// This calculation is fundamental to the poster layout system as it:
    /// - Prevents text from being cut off or appearing cramped at canvas edges
    /// - Ensures consistent margins across different poster sizes and aspect ratios
    /// - Provides a standardized coordinate system for all text positioning operations
    /// - Respects user-configured margin preferences from the plugin settings
    /// 
    /// The safe area is calculated as a percentage of the total canvas dimensions, with equal margins
    /// applied to all four sides for symmetrical layouts.
    /// </summary>
    /// <param name="canvasWidth">Total canvas width in pixels for margin calculations.</param>
    /// <param name="canvasHeight">Total canvas height in pixels for margin calculations.</param>
    /// <param name="config">Plugin configuration containing the PosterSafeArea percentage setting.</param>
    /// <returns>SKRect defining the safe drawing area with appropriate margin padding applied.</returns>
    // MARK: CalculateSafeArea
    private static SKRect CalculateSafeArea(float canvasWidth, float canvasHeight, PosterSettings settings)
    {
        // Convert percentage to decimal for calculations (e.g., 5% becomes 0.05)
        var safeAreaMargin = settings.PosterSafeArea / 100f;
        
        // Calculate pixel margins for horizontal and vertical edges
        var marginX = canvasWidth * safeAreaMargin;
        var marginY = canvasHeight * safeAreaMargin;
        
        // Create rectangle with margins applied to all four sides
        return new SKRect(
            marginX,                    // Left edge with margin
            marginY,                    // Top edge with margin
            canvasWidth - marginX,      // Right edge with margin
            canvasHeight - marginY      // Bottom edge with margin
        );
    }

    /// <summary>
    /// Implements an advanced algorithm to find the optimal word split point for creating visually balanced two-line text layouts.
    /// This method analyzes all possible word break positions and selects the split that minimizes visual imbalance
    /// while ensuring both resulting lines fit within the specified width constraints.
    /// 
    /// The algorithm evaluates each potential split point by:
    /// 1. Measuring the pixel width of text content on each resulting line
    /// 2. Verifying both lines fit within the maximum width constraint
    /// 3. Calculating the visual difference between line lengths
    /// 4. Selecting the split that produces the most balanced appearance
    /// 
    /// This approach creates more professional-looking layouts compared to simple word wrapping,
    /// as it considers the visual harmony of the overall text block rather than just fitting requirements.
    /// The result is text that appears intentionally designed rather than mechanically wrapped.
    /// </summary>
    /// <param name="words">Array of individual words to be distributed across two lines.</param>
    /// <param name="paint">Paint object for accurate text width measurements during evaluation.</param>
    /// <param name="maxWidth">Maximum allowed width in pixels that each line must respect.</param>
    /// <returns>Index position where words should be split between first and second lines for optimal balance.</returns>
    // MARK: FindOptimalSplitPoint
    private static int FindOptimalSplitPoint(string[] words, SKPaint paint, float maxWidth)
    {
        int bestSplit = words.Length / 2;     // Initialize with middle split as fallback
        float bestDifference = float.MaxValue; // Track the smallest width difference found

        // Evaluate each possible split position between words
        for (int i = 1; i < words.Length; i++)
        {
            // Construct the text content for each potential line
            string firstPart = string.Join(" ", words[..i]);   // Words before split point
            string secondPart = string.Join(" ", words[i..]);  // Words after split point
            
            // Measure pixel widths for both lines using the current font settings
            float firstWidth = paint.MeasureText(firstPart);
            float secondWidth = paint.MeasureText(secondPart);
            
            // Only consider splits where both lines fit within width constraints
            if (firstWidth <= maxWidth && secondWidth <= maxWidth)
            {
                // Calculate visual balance by measuring width difference
                float difference = Math.Abs(firstWidth - secondWidth);
                
                // Update best split if this creates better visual balance
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestSplit = i;
                }
            }
        }

        return bestSplit;
    }

    /// <summary>
    /// Truncates text content and appends ellipsis characters to fit within specified width constraints.
    /// This method implements a character-by-character reduction algorithm that progressively removes content
    /// from the end of the text until the remaining content plus ellipsis fits within the available space.
    /// 
    /// The truncation process:
    /// 1. Checks if the original text already fits (optimization for common case)
    /// 2. Reserves space for ellipsis characters ("...") in width calculations
    /// 3. Progressively removes characters from the end until content fits
    /// 4. Provides graceful fallback to ellipsis-only if no characters can fit
    /// 
    /// This approach ensures text content is preserved as much as possible while clearly indicating
    /// to users that additional content exists beyond what is visible. The ellipsis provides
    /// a standard visual cue that content has been truncated.
    /// </summary>
    /// <param name="text">Original text content to be truncated if necessary.</param>
    /// <param name="paint">Paint object for accurate text width measurements during truncation.</param>
    /// <param name="maxWidth">Maximum allowed width in pixels for the final truncated text including ellipsis.</param>
    /// <returns>Truncated text with ellipsis appended, or original text if no truncation was needed.</returns>
    // MARK: TruncateWithEllipsis
    public static string TruncateWithEllipsis(string text, SKPaint paint, float maxWidth)
    {
        const string ellipsis = "...";
        
        // Optimization: return original text if it already fits within constraints
        if (paint.MeasureText(text) <= maxWidth)
            return text;

        // Reserve space for ellipsis in available width calculations
        var ellipsisWidth = paint.MeasureText(ellipsis);
        var availableWidth = maxWidth - ellipsisWidth;

        // Progressively remove characters from the end until content fits
        for (int i = text.Length - 1; i >= 0; i--)
        {
            var substring = text.Substring(0, i);
            if (paint.MeasureText(substring) <= availableWidth)
            {
                return substring + ellipsis;
            }
        }

        // Graceful fallback: return ellipsis if no characters can fit
        return ellipsis;
    }

    /// <summary>
    /// Calculates the bounding rectangle encompassing a collection of text lines for layout and positioning purposes.
    /// This method analyzes multiple lines of text to determine the overall dimensions of the text block,
    /// which is essential for precise positioning and alignment operations within poster layouts.
    /// 
    /// The calculation process:
    /// 1. Measures the pixel width of each individual text line
    /// 2. Determines the maximum width across all lines for block width
    /// 3. Calculates total height including line spacing for multi-line content
    /// 4. Accounts for font size and line spacing multipliers for accurate dimensions
    /// 
    /// This bounding information is crucial for centering text blocks, calculating safe area requirements,
    /// and ensuring text doesn't overlap with other poster elements.
    /// </summary>
    /// <param name="lines">Read-only collection of text lines to measure for bounding calculations.</param>
    /// <param name="paint">Paint object configured with font settings for accurate text measurements.</param>
    /// <param name="fontSize">Font size in pixels for line height and spacing calculations.</param>
    /// <returns>SKRect representing the bounding box that encompasses all text lines with proper spacing.</returns>
    // MARK: CalculateTextBounds
    private static SKRect CalculateTextBounds(IReadOnlyList<string> lines, SKPaint paint, int fontSize)
    {
        float maxWidth = 0;
        
        // Find the widest line to determine the overall text block width
        foreach (var line in lines)
        {
            var width = paint.MeasureText(line);
            if (width > maxWidth)
                maxWidth = width;
        }

        // Calculate total height including line spacing for multi-line content
        float lineHeight = fontSize * LineSpacingMultiplier;
        float totalHeight = (lines.Count - 1) * lineHeight + fontSize;
        
        // Return bounding rectangle with calculated dimensions
        return new SKRect(0, 0, maxWidth, totalHeight);
    }

    /// <summary>
    /// Renders multiple lines of text with professional drop shadow effects on the specified canvas.
    /// This method handles the sequential rendering of text lines with proper vertical spacing and consistent
    /// shadow positioning to create cohesive multi-line text blocks with enhanced visual depth.
    /// 
    /// The rendering process for each line:
    /// 1. Renders the drop shadow at the specified offset position for depth effect
    /// 2. Renders the main text at the precise calculated position
    /// 3. Advances the vertical position by the calculated line height for the next line
    /// 4. Maintains consistent horizontal alignment across all lines
    /// 
    /// Shadow rendering occurs first to ensure shadows appear behind the main text content,
    /// creating the proper depth effect. The consistent offset and spacing create professional
    /// typography that enhances readability while maintaining visual appeal.
    /// </summary>
    /// <param name="canvas">SkiaSharp canvas object for rendering text and shadow elements.</param>
    /// <param name="lines">Collection of text lines to render with proper spacing and alignment.</param>
    /// <param name="alignmentX">Horizontal pixel position for text alignment across all lines.</param>
    /// <param name="baseY">Starting vertical baseline position for the first line of text.</param>
    /// <param name="fontSize">Font size in pixels for calculating line spacing between multiple lines.</param>
    /// <param name="titlePaint">Configured paint object for rendering the main text content.</param>
    /// <param name="shadowPaint">Configured paint object for rendering drop shadow effects.</param>
    // MARK: DrawTextLines
    private static void DrawTextLines(
        SKCanvas canvas,
        IReadOnlyList<string> lines,
        float alignmentX,
        float baseY,
        int fontSize,
        SKPaint titlePaint,
        SKPaint shadowPaint)
    {
        // Calculate line height including spacing for multi-line text layout
        float lineHeight = fontSize * LineSpacingMultiplier;
        float currentY = baseY;

        // Render each line with shadow effects and proper vertical spacing
        foreach (var line in lines)
        {
            // Render drop shadow first with offset positioning for depth effect
            canvas.DrawText(line, alignmentX + ShadowOffset, currentY + ShadowOffset, shadowPaint);
            
            // Render main text content at the precise calculated position
            canvas.DrawText(line, alignmentX, currentY, titlePaint);
            
            // Advance vertical position for next line with consistent spacing
            currentY += lineHeight;
        }
    }
}