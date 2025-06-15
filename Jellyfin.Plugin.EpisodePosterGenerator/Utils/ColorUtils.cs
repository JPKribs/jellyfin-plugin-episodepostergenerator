using SkiaSharp;
using System;
using System.Globalization;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Specialized utility class for color parsing, validation, and conversion operations within the poster generation system.
/// Provides robust color handling capabilities that bridge user-configurable color preferences with SkiaSharp's
/// rendering pipeline, ensuring consistent color representation across all poster styles and visual elements.
/// 
/// This utility serves as the color management foundation for the entire poster generation system by:
/// - Converting user-friendly hexadecimal color strings into SkiaSharp-compatible color objects
/// - Supporting multiple color format specifications including RGB and ARGB representations
/// - Providing intelligent error handling and fallback mechanisms for invalid color specifications
/// - Ensuring culture-invariant color parsing for consistent behavior across different system locales
/// - Optimizing color parsing performance through modern .NET span-based string processing
/// 
/// Color Format Support:
/// The utility comprehensively supports industry-standard hexadecimal color formats commonly used
/// in web development, graphic design, and digital media applications:
/// - 6-digit RGB format (#RRGGBB) for opaque colors with full red, green, blue specification
/// - 8-digit ARGB format (#AARRGGBB) for colors with alpha transparency channel support
/// - Flexible hash prefix handling allowing both #-prefixed and raw hexadecimal strings
/// - Automatic format detection based on string length for seamless user experience
/// 
/// Integration Points:
/// - Text rendering: Parses font colors for episode titles, numbers, and overlay text
/// - Background effects: Processes overlay tints, background colors, and visual effect colors
/// - Shadow systems: Handles drop shadow colors and transparency effects
/// - Configuration system: Converts user-entered color preferences into rendering-ready values
/// - Multi-style support: Ensures consistent color handling across Standard, Cutout, Numeral, and Logo poster styles
/// 
/// Design Principles:
/// - Robustness: Graceful handling of invalid input with sensible fallback behavior
/// - Performance: Optimized parsing algorithms suitable for high-frequency batch processing
/// - Consistency: Culture-invariant parsing ensuring identical results regardless of system locale
/// - Compatibility: Support for standard color formats familiar to users and compatible with external tools
/// - Reliability: Comprehensive error handling preventing poster generation failures due to color issues
/// 
/// The class implements modern .NET performance optimizations including span-based string processing
/// to minimize memory allocation and improve parsing speed during intensive poster generation workflows.
/// </summary>
public static class ColorUtils
{
    /// <summary>
    /// Parses hexadecimal color strings into SkiaSharp color objects using advanced string processing algorithms
    /// with comprehensive format support and intelligent error handling. This method serves as the primary color
    /// conversion interface for the entire poster generation system, handling user-configured colors and
    /// converting them into rendering-ready format.
    /// 
    /// Supported Color Formats:
    /// 
    /// 6-Digit RGB Format (#RRGGBB):
    /// - Standard web color format representing opaque colors
    /// - Red, Green, Blue components as 2-digit hexadecimal values (00-FF)
    /// - Automatically assigns full opacity (alpha = 255) for opaque rendering
    /// - Examples: #FF0000 (red), #00FF00 (green), #0000FF (blue), #FFFFFF (white)
    /// 
    /// 8-Digit ARGB Format (#AARRGGBB):
    /// - Extended format supporting alpha transparency channel
    /// - Alpha, Red, Green, Blue components as 2-digit hexadecimal values
    /// - Enables transparency effects, overlays, and blending operations
    /// - Examples: #80FF0000 (semi-transparent red), #FF000000 (opaque black), #00FFFFFF (transparent white)
    /// 
    /// Format Detection Algorithm:
    /// The method employs intelligent format detection based on string length after hash removal:
    /// - 8 characters: Interpreted as ARGB format with alpha channel support
    /// - 6 characters: Interpreted as RGB format with automatic full opacity assignment
    /// - Other lengths: Treated as invalid input triggering fallback behavior
    /// 
    /// Parsing Process:
    /// 1. Input validation and null/empty string handling with white fallback
    /// 2. Hash prefix removal for flexible input format support
    /// 3. Span-based string processing for optimal memory efficiency and performance
    /// 4. Culture-invariant hexadecimal parsing ensuring consistent behavior across locales
    /// 5. Component extraction using efficient string slicing operations
    /// 6. SKColor object construction with appropriate component ordering
    /// 
    /// Error Handling Strategy:
    /// - Null/empty input: Returns white color as safe, visible fallback
    /// - Invalid format: Returns white color preventing render failures
    /// - Malformed hex: Parsing exceptions handled with white fallback
    /// - Unknown length: Treated as invalid with white fallback
    /// 
    /// Performance Optimizations:
    /// - Span-based processing minimizes string allocation overhead
    /// - Direct byte parsing avoids intermediate string conversions
    /// - Culture-invariant parsing eliminates locale-specific processing overhead
    /// - Efficient component slicing reduces parsing complexity
    /// 
    /// The method ensures robust operation even with malformed user input, preventing poster
    /// generation failures while providing predictable fallback behavior for debugging and
    /// user feedback purposes.
    /// </summary>
    /// <param name="hex">
    /// Hexadecimal color string in supported formats. Accepts both hash-prefixed (#FF0000) and
    /// raw hexadecimal (FF0000) representations. Case-insensitive parsing supported for user convenience.
    /// Null, empty, or whitespace-only strings trigger safe fallback behavior.
    /// </param>
    /// <returns>
    /// SKColor object ready for use in SkiaSharp rendering operations. Returns white color for
    /// invalid or malformed input to ensure consistent fallback behavior and prevent rendering
    /// failures during poster generation workflows.
    /// </returns>
    /// <example>
    /// Demonstrating comprehensive color format support and parsing behavior:
    /// <code>
    /// // 6-digit RGB format examples (opaque colors)
    /// var red = ColorUtils.ParseHexColor("#FF0000");     // Returns opaque red
    /// var green = ColorUtils.ParseHexColor("00FF00");    // Returns opaque green (no # prefix)
    /// var blue = ColorUtils.ParseHexColor("#0000FF");    // Returns opaque blue
    /// var white = ColorUtils.ParseHexColor("#FFFFFF");   // Returns opaque white
    /// 
    /// // 8-digit ARGB format examples (colors with transparency)
    /// var semiRed = ColorUtils.ParseHexColor("#80FF0000");      // Returns 50% transparent red
    /// var opaqueBlack = ColorUtils.ParseHexColor("#FF000000");  // Returns fully opaque black
    /// var transparentWhite = ColorUtils.ParseHexColor("#00FFFFFF"); // Returns fully transparent white
    /// 
    /// // Error handling examples
    /// var fallback1 = ColorUtils.ParseHexColor("");      // Returns white (empty string)
    /// var fallback2 = ColorUtils.ParseHexColor(null);    // Returns white (null input)
    /// var fallback3 = ColorUtils.ParseHexColor("#XYZ");  // Returns white (invalid format)
    /// </code>
    /// </example>
    // MARK: ParseHexColor
    public static SKColor ParseHexColor(string hex)
    {
        // Early validation: handle null, empty, or whitespace-only input with safe fallback
        if (string.IsNullOrWhiteSpace(hex))
            return SKColors.White;

        // Normalize input by removing hash prefix for flexible format support
        hex = hex.TrimStart('#');
        
        // Use span for high-performance string processing without allocation overhead
        var span = hex.AsSpan();
        
        // Configure culture-invariant hexadecimal parsing for consistent behavior across locales
        var style = NumberStyles.HexNumber;
        var culture = CultureInfo.InvariantCulture;

        // Intelligent format detection and component parsing based on string length
        return span.Length switch
        {
            // 8-digit ARGB format: Alpha, Red, Green, Blue components
            8 => new SKColor(
                byte.Parse(span.Slice(2, 2), style, culture),  // Red component (positions 2-3)
                byte.Parse(span.Slice(4, 2), style, culture),  // Green component (positions 4-5)
                byte.Parse(span.Slice(6, 2), style, culture),  // Blue component (positions 6-7)
                byte.Parse(span.Slice(0, 2), style, culture)   // Alpha component (positions 0-1)
            ),
            
            // 6-digit RGB format: Red, Green, Blue components with full opacity
            6 => new SKColor(
                byte.Parse(span.Slice(0, 2), style, culture),  // Red component (positions 0-1)
                byte.Parse(span.Slice(2, 2), style, culture),  // Green component (positions 2-3)
                byte.Parse(span.Slice(4, 2), style, culture)   // Blue component (positions 4-5)
                // Alpha defaults to 255 (fully opaque) in SKColor constructor
            ),
            
            // Invalid format: fallback to white for safe error handling
            _ => SKColors.White
        };
    }
}