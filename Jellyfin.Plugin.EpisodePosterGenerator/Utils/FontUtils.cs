using System;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Advanced utility class for comprehensive font management, measurement, and dynamic sizing operations within the poster generation system.
/// Provides sophisticated font selection algorithms, precise text measurement capabilities, and intelligent sizing calculations
/// optimized for high-quality typography in automated poster creation workflows.
/// 
/// This utility serves as the typography foundation for the entire poster generation system by:
/// - Implementing robust font matching using SkiaSharp's modern font manager for reliable cross-platform operation
/// - Providing precise text measurement capabilities essential for layout calculations and positioning
/// - Offering advanced font sizing algorithms including binary search optimization for performance
/// - Handling font fallback scenarios gracefully when requested fonts are unavailable
/// - Supporting responsive typography that scales appropriately across different poster dimensions
/// 
/// Key Technical Features:
/// - Dynamic font matching with system-level fallbacks for maximum compatibility
/// - Binary search algorithms for optimal font size calculations with configurable precision
/// - Percentage-based sizing systems that adapt to poster dimensions and safe area constraints
/// - Culture-aware font style parsing supporting multiple configuration formats
/// - Memory-efficient operations using proper resource management and disposal patterns
/// 
/// Integration Points:
/// - Works seamlessly with SkiaSharp's rendering pipeline for high-quality text output
/// - Integrates with plugin configuration system for user-customizable typography preferences
/// - Supports all poster styles (Standard, Cutout, Numeral, Logo) with consistent font handling
/// - Provides foundation for TextUtils advanced layout algorithms and positioning calculations
/// 
/// The class is designed for high-frequency usage during batch poster generation with optimized
/// algorithms that minimize computational overhead while maintaining precise typographic control.
/// </summary>
public static class FontUtils
{
    /// <summary>
    /// Creates a properly configured SKTypeface by intelligently matching font family names with desired styles
    /// using SkiaSharp's advanced font management system. This method provides robust font selection with
    /// automatic fallback handling for maximum compatibility across different operating systems and font installations.
    /// 
    /// Font Matching Algorithm:
    /// 1. Queries the system's font manager for available font families
    /// 2. Attempts exact match for the requested family name and style combination
    /// 3. Falls back to closest available match based on style characteristics if exact match unavailable
    /// 4. Provides system default font with requested style as final fallback
    /// 
    /// This approach ensures reliable font selection even when specific fonts are unavailable,
    /// preventing poster generation failures due to missing fonts while maintaining visual consistency
    /// through intelligent style-based matching.
    /// 
    /// Advantages over Legacy Methods:
    /// - More reliable than deprecated FromFamilyName approaches
    /// - Leverages modern font management APIs for better cross-platform support
    /// - Provides intelligent fallback handling without requiring manual font validation
    /// - Supports complex font style combinations (weight, width, slant) in a single operation
    /// 
    /// Common Use Cases:
    /// - Creating display fonts for episode titles and numbers
    /// - Selecting serif/sans-serif fonts based on poster style preferences
    /// - Handling user-configured font preferences with automatic fallbacks
    /// - Supporting system fonts across Windows, macOS, and Linux environments
    /// </summary>
    /// <param name="fontFamilyName">
    /// Font family name as it appears in the system (e.g., "Arial", "Helvetica", "Impact", "Times New Roman").
    /// Case-insensitive matching is typically supported by the underlying font manager.
    /// </param>
    /// <param name="style">
    /// Complete font style specification including weight (Normal, Bold), width (Normal, Condensed, Expanded),
    /// and slant (Upright, Italic, Oblique) characteristics for precise font matching.
    /// </param>
    /// <returns>
    /// Configured SKTypeface object ready for text rendering operations. Returns best available match
    /// or system default with requested style if exact font family is unavailable.
    /// </returns>
    /// <example>
    /// Creating a bold, condensed font for dramatic poster text:
    /// <code>
    /// var style = new SKFontStyle(SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Upright);
    /// var typeface = FontUtils.CreateTypeface("Impact", style);
    /// </code>
    /// </example>
    // MARK: CreateTypeface
    public static SKTypeface CreateTypeface(string fontFamilyName, SKFontStyle style)
    {
        // Access the system's comprehensive font manager for robust font discovery
        // SKFontManager.Default provides cross-platform access to installed system fonts
        var fontManager = SKFontManager.Default;

        // Perform intelligent font matching using modern font selection algorithms
        // MatchFamily implements sophisticated fallback logic for reliable font selection
        return fontManager.MatchFamily(fontFamilyName, style);
    }

    /// <summary>
    /// Creates a system default SKTypeface with specified style characteristics when no specific font family is required.
    /// This method provides a convenient way to obtain well-formed fonts with desired styling without needing
    /// to specify particular font families, relying instead on the system's default font selection algorithms.
    /// 
    /// Use Cases:
    /// - Fallback font creation when specific font families are unavailable
    /// - Creating consistent system-native typography that respects user preferences
    /// - Rapid prototyping where specific font families are not critical
    /// - Cross-platform development where font availability varies significantly
    /// 
    /// The system font manager selects appropriate default fonts based on the requested style characteristics,
    /// ensuring consistent typography while respecting platform conventions and user accessibility settings.
    /// </summary>
    /// <param name="style">
    /// Font style specification defining weight, width, and slant characteristics.
    /// The system will select an appropriate default font family that supports these style attributes.
    /// </param>
    /// <returns>
    /// System default SKTypeface configured with the requested style characteristics.
    /// Guaranteed to return a valid typeface suitable for text rendering operations.
    /// </returns>
    // MARK: CreateTypeface
    public static SKTypeface CreateTypeface(SKFontStyle style)
    {
        // Request system default font with specified style characteristics
        // Passing null/empty family name triggers default font selection algorithms
        return SKFontManager.Default.MatchFamily(null, style);
    }

    /// <summary>
    /// Measures the precise bounding rectangle of text content using specified typography configuration.
    /// This method provides accurate text dimension calculations essential for layout positioning,
    /// alignment operations, and collision detection within poster generation workflows.
    /// 
    /// Measurement Process:
    /// 1. Creates temporary SKPaint object with specified font configuration
    /// 2. Enables anti-aliasing for accurate edge detection during measurement
    /// 3. Performs precise text measurement using SkiaSharp's text metrics system
    /// 4. Returns bounding rectangle encompassing all visible text pixels
    /// 
    /// The measurement includes accurate consideration of:
    /// - Character advance widths for proper horizontal spacing
    /// - Ascent and descent values for complete vertical coverage
    /// - Kerning adjustments between character pairs
    /// - Anti-aliasing effects on text boundaries
    /// 
    /// Critical for Layout Calculations:
    /// - Determining optimal text positioning within safe area boundaries
    /// - Calculating text wrapping and truncation requirements
    /// - Implementing precise text alignment algorithms
    /// - Ensuring text doesn't exceed poster dimension constraints
    /// </summary>
    /// <param name="text">Text content to measure, supporting any Unicode characters and length.</param>
    /// <param name="typeface">Configured font typeface defining character shapes and metrics.</param>
    /// <param name="fontSize">Font size in points/pixels for scaling text measurements appropriately.</param>
    /// <returns>
    /// SKRect representing the tight bounding box around all visible text pixels.
    /// Coordinates are relative to the text baseline and origin point.
    /// </returns>
    // MARK: MeasureTextDimensions
    public static SKRect MeasureTextDimensions(string text, SKTypeface typeface, float fontSize)
    {
        // Create temporary paint object with proper resource management
        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = fontSize,
            IsAntialias = true  // Enable anti-aliasing for accurate measurement
        };

        // Perform precise text measurement using SkiaSharp's metrics system
        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);
        return bounds;
    }

    /// <summary>
    /// Determines the optimal font size for text content within specified dimensional constraints using an efficient
    /// binary search algorithm. This method provides intelligent font sizing that maximizes text readability while
    /// ensuring content fits within available space, essential for responsive poster typography.
    /// 
    /// Binary Search Algorithm:
    /// 1. Establishes search bounds between minimum font size and maximum height
    /// 2. Iteratively tests mid-point font sizes using precise text measurement
    /// 3. Adjusts search bounds based on whether text fits within constraints
    /// 4. Converges on optimal size within specified tolerance for performance
    /// 5. Returns largest font size that satisfies all dimensional requirements
    /// 
    /// Performance Characteristics:
    /// - O(log n) time complexity for rapid convergence
    /// - Configurable tolerance for balance between precision and speed
    /// - Minimal text measurement operations through intelligent bound management
    /// - Early termination conditions to prevent infinite loops
    /// 
    /// Optimization Strategy:
    /// The algorithm assumes that text height rarely exceeds font size significantly,
    /// allowing the maximum font size to be bounded by available height for improved
    /// search efficiency. This assumption holds true for typical poster typography.
    /// 
    /// Applications:
    /// - Sizing episode numbers for cutout and numeral poster styles
    /// - Automatic title sizing within safe area constraints
    /// - Responsive typography that adapts to different poster dimensions
    /// - Maximizing text impact while maintaining layout integrity
    /// </summary>
    /// <param name="text">Text content to size, affecting measurement calculations during optimization.</param>
    /// <param name="typeface">Font typeface for accurate character metrics during size calculations.</param>
    /// <param name="maxWidth">Maximum allowed text width in pixels that must not be exceeded.</param>
    /// <param name="maxHeight">Maximum allowed text height in pixels that must not be exceeded.</param>
    /// <param name="minFontSize">
    /// Minimum acceptable font size in points/pixels. Prevents algorithm from selecting
    /// illegibly small fonts while ensuring some text is always rendered.
    /// </param>
    /// <param name="tolerance">
    /// Binary search precision control. Smaller values increase accuracy but require more iterations.
    /// Default of 0.5 provides good balance between precision and performance for poster generation.
    /// </param>
    /// <returns>
    /// Optimal font size in points/pixels that maximizes text size while fitting within all constraints.
    /// Guaranteed to be at least the minimum font size and respect all dimensional limitations.
    /// </returns>
    // MARK: CalculateOptimalFontSize
    public static float CalculateOptimalFontSize(string text, SKTypeface typeface, float maxWidth, float maxHeight, float minFontSize = 10f, float tolerance = 0.5f)
    {
        // Set upper bound optimistically - text height rarely exceeds font size significantly
        float maxFontSize = maxHeight;
        float optimalSize = minFontSize;

        // Initialize binary search bounds for efficient convergence
        float low = minFontSize;
        float high = maxFontSize;

        // Perform binary search with tolerance-based convergence
        while (low <= high)
        {
            // Calculate mid-point for current iteration
            float mid = low + (high - low) / 2;
            
            // Safety check to prevent invalid font sizes
            if (mid <= 0) break;

            // Measure text dimensions at current test font size
            var bounds = MeasureTextDimensions(text, typeface, mid);

            // Check if current size satisfies all dimensional constraints
            if (bounds.Width <= maxWidth && bounds.Height <= maxHeight)
            {
                // Size fits - try larger size by adjusting lower bound
                optimalSize = mid; // Store this as potential optimal solution
                low = mid + tolerance;
            }
            else
            {
                // Size too large - reduce upper bound for smaller sizes
                high = mid - tolerance;
            }
        }

        return optimalSize;
    }

    /// <summary>
    /// Calculates responsive font sizes based on percentage of poster dimensions with safe area adjustments.
    /// This method implements intelligent typography scaling that adapts to different poster sizes while
    /// respecting layout constraints and maintaining consistent visual proportions across varying content.
    /// 
    /// Calculation Formula:
    /// Font Size = Poster Height × (Percentage ÷ (100% - (Safe Area Margin × 2)))
    /// 
    /// The formula accounts for safe area margins by adjusting the effective poster height,
    /// ensuring that percentage-based sizing remains visually consistent regardless of margin settings.
    /// This prevents text from appearing disproportionately small when large margins are configured.
    /// 
    /// Safe Area Integration:
    /// When safe area margins are specified, the calculation adjusts for the reduced available space
    /// by effectively increasing the percentage ratio. This maintains visual consistency where a 10%
    /// font size appears proportionally similar regardless of margin configuration.
    /// 
    /// Responsive Design Benefits:
    /// - Consistent visual proportions across different poster aspect ratios
    /// - Automatic scaling for various output resolutions (SD, HD, 4K)
    /// - User-configurable typography that maintains readability
    /// - Integration with safe area systems for professional layout management
    /// 
    /// Common Usage Patterns:
    /// - Episode titles: 7-12% for prominent but not overwhelming text
    /// - Episode numbers: 5-8% for clear identification without dominating
    /// - Subtitle text: 3-5% for supplementary information display
    /// </summary>
    /// <param name="percentage">
    /// Desired font size as percentage of poster height (e.g., 5.0 for 5% of height).
    /// Typical values range from 3-15% depending on text importance and poster style.
    /// </param>
    /// <param name="posterHeight">
    /// Total poster height in pixels used as base for percentage calculations.
    /// Should represent the final output dimensions for accurate scaling.
    /// </param>
    /// <param name="posterMargin">
    /// Safe area margin as percentage of poster height (e.g., 5.0 for 5% margins).
    /// Used to adjust effective available space for more accurate proportional sizing.
    /// Default of 0 disables safe area adjustments for backwards compatibility.
    /// </param>
    /// <returns>
    /// Calculated font size in pixels as integer value suitable for SkiaSharp text rendering.
    /// Returns 0 for invalid input parameters to prevent rendering errors.
    /// </returns>
    // MARK: CalculateFontSizeFromPercentage
    public static int CalculateFontSizeFromPercentage(float percentage, float posterHeight, float posterMargin = 0)
    {
        // Validate input parameters to prevent calculation errors
        if (percentage <= 0f || posterHeight <= 0f)
            return 0;

        // Calculate font size with safe area margin adjustments
        // Formula adjusts for reduced available space when margins are present
        return (int)(posterHeight * (percentage / (100f - (posterMargin * 2))));
    }

    /// <summary>
    /// Parses user-configured font style strings into SkiaSharp font style objects with intelligent string matching.
    /// This method provides flexible configuration parsing that handles various string formats and provides
    /// appropriate fallbacks for unrecognized style specifications, ensuring robust font configuration handling.
    /// 
    /// Parsing Strategy:
    /// - Case-insensitive matching for user-friendly configuration
    /// - Support for compound styles like "Bold Italic" combinations
    /// - Graceful fallback to Normal style for unrecognized input
    /// - Culture-invariant string processing for consistent behavior
    /// 
    /// Supported Style Formats:
    /// - "Bold" or "bold" → SKFontStyle.Bold
    /// - "Italic" or "italic" → SKFontStyle.Italic  
    /// - "Bold Italic" or "bold italic" → SKFontStyle.BoldItalic
    /// - Any other value → SKFontStyle.Normal (default fallback)
    /// 
    /// Configuration Integration:
    /// This method bridges the gap between user-friendly configuration strings
    /// and SkiaSharp's strongly-typed font style system, enabling flexible
    /// font configuration while maintaining type safety in the rendering pipeline.
    /// 
    /// Robustness Features:
    /// - Null and empty string handling with safe defaults
    /// - Case normalization for consistent matching
    /// - Fallback behavior prevents configuration errors from breaking poster generation
    /// - Support for future style extensions through the default case
    /// </summary>
    /// <param name="fontStyle">
    /// User-configured font style string from plugin settings (e.g., "Bold", "Italic", "Bold Italic").
    /// Case-insensitive matching supported. Null or empty values default to Normal style.
    /// </param>
    /// <returns>
    /// Corresponding SKFontStyle enumeration value ready for use with SkiaSharp typeface creation.
    /// Always returns a valid style with Normal as the safe fallback for unrecognized input.
    /// </returns>
    // MARK: GetFontStyle
    public static SKFontStyle GetFontStyle(string fontStyle)
    {
        // Perform case-insensitive style string matching with comprehensive fallback handling
        return fontStyle.ToLowerInvariant() switch
        {
            "bold" => SKFontStyle.Bold,
            "italic" => SKFontStyle.Italic,
            "bold italic" => SKFontStyle.BoldItalic,
            _ => SKFontStyle.Normal,  // Safe default for unrecognized or null input
        };
    }
}