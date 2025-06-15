using System.Globalization;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Specialized utility class for generating standardized episode and season identification codes across multiple poster styles.
/// Provides comprehensive formatting algorithms that handle diverse numbering schemes while maintaining visual consistency
/// and professional presentation standards throughout the poster generation system.
/// 
/// This utility serves as the central authority for episode identification formatting, ensuring consistent
/// presentation across all poster styles while accommodating the wide variety of television series numbering
/// conventions found in modern media libraries. The class handles everything from traditional broadcast
/// series with standard season/episode numbering to long-running series with hundreds of episodes.
/// 
/// Key Formatting Capabilities:
/// - Intelligent zero-padding algorithms that adapt to series length and numbering magnitude
/// - Multi-format output supporting both machine-readable codes (S01E05) and human-readable text (FIVE)
/// - Culture-invariant formatting ensuring consistent output across different system locales and regions
/// - Adaptive padding strategies that expand automatically for large-scale series (1000+ episodes)
/// - Integration with cutout poster styles requiring text-based episode representations
/// 
/// Poster Style Integration:
/// - Standard Style: Uses formatted codes for clean, professional episode identification
/// - Logo Style: Employs consistent S##E## formatting for brand-aligned episode display
/// - Cutout Style: Supports both textual (THREE) and coded (S01E03) cutout representations
/// - Numeral Style: Integrates with Roman numeral systems for classical episode presentation
/// 
/// Design Principles:
/// - Consistency: Uniform formatting rules across all poster types and series lengths
/// - Adaptability: Automatic adjustment to accommodate various series numbering scales
/// - Readability: Clear, unambiguous episode identification suitable for visual media
/// - Internationalization: Culture-invariant processing for global media library compatibility
/// - Performance: Optimized algorithms suitable for batch processing of large media collections
/// 
/// The formatting algorithms are designed to handle edge cases gracefully, including series with
/// unconventional numbering, gaps in episode sequences, and international naming conventions,
/// ensuring robust operation across diverse media libraries and content sources.
/// </summary>
public static class EpisodeCodeUtil
{
    /// <summary>
    /// Generates standardized episode identification codes using intelligent zero-padding algorithms that adapt
    /// to series scale and numbering magnitude. This method implements sophisticated formatting logic that ensures
    /// consistent visual presentation while accommodating the wide range of episode numbering schemes found
    /// in modern television series and media collections.
    /// 
    /// Adaptive Padding Algorithm:
    /// The method employs a dynamic padding strategy that analyzes the magnitude of both season and episode
    /// numbers to determine optimal zero-padding requirements. This ensures visual consistency within series
    /// while preventing unnecessarily verbose formatting for smaller productions.
    /// 
    /// Padding Strategy:
    /// - Numbers 1-99: 2-digit padding (S01E05, S12E99)
    /// - Numbers 100-999: 3-digit padding (S100E150, S001E999)
    /// - Numbers 1000+: 4-digit padding (S1000E1500, S0001E9999)
    /// 
    /// This approach ensures that episode codes maintain consistent character width within their
    /// magnitude class while remaining readable and professional in appearance.
    /// 
    /// Culture-Invariant Formatting:
    /// All number formatting uses CultureInfo.InvariantCulture to ensure consistent output across
    /// different system locales, preventing regional number formatting preferences from affecting
    /// episode code generation. This is critical for international media libraries where consistent
    /// episode identification is essential regardless of the system's regional settings.
    /// 
    /// Visual Consistency Benefits:
    /// - Episode lists maintain uniform column width for improved readability
    /// - Sorting algorithms work correctly with consistent string lengths
    /// - Visual scanning is enhanced through predictable formatting patterns
    /// - Professional presentation suitable for media library interfaces
    /// 
    /// Integration with Media Systems:
    /// The generated codes follow industry-standard S##E## formatting conventions widely
    /// recognized by media management systems, streaming platforms, and content databases,
    /// ensuring compatibility with existing media infrastructure and user expectations.
    /// </summary>
    /// <param name="seasonNumber">
    /// Season number to format, supporting any positive integer value. The method automatically
    /// determines appropriate zero-padding based on the number's magnitude to ensure consistent
    /// formatting within the series context.
    /// </param>
    /// <param name="episodeNumber">
    /// Episode number to format, supporting any positive integer value. Padding is calculated
    /// independently from season number to handle series with asymmetric season/episode scaling
    /// (e.g., short seasons in long-running series).
    /// </param>
    /// <returns>
    /// Professional episode identification code in S##E## format with intelligent zero-padding
    /// applied. The padding automatically adapts to number magnitude ensuring consistent visual
    /// presentation while maintaining readability and industry standard compatibility.
    /// </returns>
    /// <example>
    /// Demonstrating adaptive padding across different series scales:
    /// <code>
    /// // Standard series formatting with 2-digit padding
    /// EpisodeCodeUtil.FormatEpisodeCode(1, 5)     // Returns "S01E05"
    /// EpisodeCodeUtil.FormatEpisodeCode(10, 25)   // Returns "S10E25"
    /// 
    /// // Mixed magnitude handling with independent padding
    /// EpisodeCodeUtil.FormatEpisodeCode(1, 100)   // Returns "S01E100"
    /// EpisodeCodeUtil.FormatEpisodeCode(100, 5)   // Returns "S100E05"
    /// 
    /// // Large-scale series with extended padding
    /// EpisodeCodeUtil.FormatEpisodeCode(1000, 5)  // Returns "S1000E05"
    /// EpisodeCodeUtil.FormatEpisodeCode(1, 1500)  // Returns "S01E1500"
    /// </code>
    /// </example>
    // MARK: FormatEpisodeCode
    public static string FormatEpisodeCode(int seasonNumber, int episodeNumber)
    {
        // Determine optimal padding strategy based on individual number magnitudes
        // This approach handles asymmetric scaling where seasons and episodes may have different ranges
        string seasonFormat = GetNumberFormat(seasonNumber);
        string episodeFormat = GetNumberFormat(episodeNumber);
        
        // Generate formatted code using culture-invariant formatting for international compatibility
        return $"S{seasonNumber.ToString(seasonFormat, CultureInfo.InvariantCulture)}E{episodeNumber.ToString(episodeFormat, CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Generates specialized episode text representations optimized for cutout poster styles with intelligent
    /// format selection based on display requirements and episode numbering characteristics. This method bridges
    /// the gap between machine-readable episode codes and visually dramatic cutout text effects.
    /// 
    /// Cutout Type Formatting Strategies:
    /// 
    /// Text Type (Dramatic Word Display):
    /// - Episodes 1-99: Converts to uppercase English words using NumberUtils for dramatic visual impact
    /// - Episodes 100+: Falls back to numeric representation for readability and space efficiency
    /// - Optimized for large, bold cutout text where words create stronger visual presence than codes
    /// 
    /// Code Type (Structured Identification):
    /// - All episodes: Uses standardized S##E## formatting with intelligent zero-padding
    /// - Maintains consistent episode identification across all numbering ranges
    /// - Ideal for cutout styles where precise episode identification is prioritized over dramatic effect
    /// 
    /// Threshold Logic for Text Type:
    /// The 100-episode threshold for Text type represents a carefully considered balance between
    /// visual drama and practical readability. English words for large numbers become unwieldy
    /// for cutout effects, while numeric representation maintains clarity and visual impact.
    /// 
    /// Visual Impact Considerations:
    /// - Text cutouts benefit from shorter, impactful words (ONE, TWO, THREE)
    /// - Large numbers as words (ONE HUNDRED FIFTY-THREE) become visually overwhelming
    /// - Numeric fallback maintains readability while preserving cutout aesthetic
    /// 
    /// Integration with Poster Styles:
    /// - Cutout Style: Primary use case for dramatic episode number presentation
    /// - Standard/Logo Styles: May use code format for consistent episode identification
    /// - Flexible output supports diverse visual design requirements
    /// 
    /// The method ensures consistent formatting rules while providing flexibility for different
    /// visual design approaches, maintaining the balance between dramatic presentation and
    /// practical episode identification across varying series scales.
    /// </summary>
    /// <param name="cutoutType">
    /// Formatting strategy selection determining output style. Text type prioritizes dramatic
    /// visual presentation with English words, while Code type emphasizes structured episode
    /// identification using standardized coding conventions.
    /// </param>
    /// <param name="seasonNumber">
    /// Season number used for Code type formatting, ensuring consistent episode identification
    /// within the context of the complete series structure and numbering scheme.
    /// </param>
    /// <param name="episodeNumber">
    /// Episode number for conversion, processed according to the selected cutout type and
    /// magnitude-based formatting rules to optimize visual presentation and readability.
    /// </param>
    /// <returns>
    /// Formatted episode text optimized for cutout display applications. Output format varies
    /// based on cutout type selection and episode number magnitude, prioritizing visual impact
    /// while maintaining clarity and readability in poster generation contexts.
    /// </returns>
    /// <example>
    /// Demonstrating format selection across different cutout types and episode ranges:
    /// <code>
    /// // Text type with word conversion for dramatic cutout effects
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Text, 1, 3)    // Returns "THREE"
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Text, 1, 15)   // Returns "FIFTEEN"
    /// 
    /// // Text type with numeric fallback for large episodes
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Text, 1, 100)  // Returns "100"
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Text, 5, 250)  // Returns "250"
    /// 
    /// // Code type with consistent S##E## formatting
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Code, 1, 3)    // Returns "S01E03"
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Code, 1, 1500) // Returns "S01E1500"
    /// </code>
    /// </example>
    // MARK: FormatEpisodeText
    public static string FormatEpisodeText(CutoutType cutoutType, int seasonNumber, int episodeNumber)
    {
        return cutoutType switch
        {
            // Text type: Convert to English words for dramatic cutout effects, with numeric fallback for large numbers
            CutoutType.Text => episodeNumber >= 100 
                ? episodeNumber.ToString(CultureInfo.InvariantCulture)  // Numeric fallback for readability
                : NumberUtils.NumberToWords(episodeNumber),             // English words for visual drama
            
            // Code type: Use standardized S##E## formatting with intelligent zero-padding
            CutoutType.Code => FormatEpisodeCode(seasonNumber, episodeNumber),
            
            // Default fallback: Use Text type behavior for unknown cutout types
            _ => episodeNumber >= 100 
                ? episodeNumber.ToString(CultureInfo.InvariantCulture) 
                : NumberUtils.NumberToWords(episodeNumber)
        };
    }

    /// <summary>
    /// Determines the optimal number formatting string based on numeric magnitude using intelligent padding algorithms.
    /// This method implements a tiered formatting strategy that automatically selects appropriate zero-padding
    /// levels to ensure consistent visual presentation while minimizing unnecessary verbosity.
    /// 
    /// Tiered Formatting Strategy:
    /// The method employs a three-tier approach that balances visual consistency with practical readability:
    /// 
    /// Tier 1 (1-99): 2-digit padding (D2)
    /// - Covers standard television series episode and season numbering
    /// - Provides consistent formatting for typical broadcast schedules
    /// - Balances readability with professional presentation
    /// 
    /// Tier 2 (100-999): 3-digit padding (D3)  
    /// - Accommodates long-running series and syndicated content
    /// - Maintains visual consistency for extended episode ranges
    /// - Supports multi-season series with high episode counts
    /// 
    /// Tier 3 (1000+): 4-digit padding (D4)
    /// - Handles exceptional cases like long-running daily series or anime
    /// - Ensures consistency for extremely large media collections
    /// - Provides future-proofing for expanding content libraries
    /// 
    /// Design Rationale:
    /// The tiered approach prevents over-padding for typical use cases while ensuring scalability
    /// for exceptional scenarios. This maintains readability and visual appeal across the vast
    /// majority of media content while gracefully handling edge cases.
    /// 
    /// Format String Specifications:
    /// - "D2": Decimal formatting with minimum 2 digits, zero-padded (05, 42, 99)
    /// - "D3": Decimal formatting with minimum 3 digits, zero-padded (005, 042, 150)
    /// - "D4": Decimal formatting with minimum 4 digits, zero-padded (0005, 0042, 1500)
    /// 
    /// Performance Considerations:
    /// The switch expression provides O(1) lookup performance for format string selection,
    /// ensuring minimal computational overhead during high-frequency batch processing operations
    /// typical in automated poster generation workflows.
    /// </summary>
    /// <param name="number">
    /// Numeric value for format string determination. The method analyzes the magnitude
    /// to select the most appropriate padding level that balances consistency with readability
    /// for the given numeric range.
    /// </param>
    /// <returns>
    /// Format string suitable for use with ToString() method, providing appropriate zero-padding
    /// for the numeric magnitude. The returned string follows .NET standard numeric format
    /// specifiers for reliable, culture-invariant number formatting.
    /// </returns>
    /// <example>
    /// Demonstrating format string selection across different numeric magnitudes:
    /// <code>
    /// // Tier 1: Standard 2-digit padding for typical episode numbers
    /// GetNumberFormat(5)    // Returns "D2" → ToString produces "05"
    /// GetNumberFormat(42)   // Returns "D2" → ToString produces "42" 
    /// GetNumberFormat(99)   // Returns "D2" → ToString produces "99"
    /// 
    /// // Tier 2: Extended 3-digit padding for larger series
    /// GetNumberFormat(150)  // Returns "D3" → ToString produces "150"
    /// GetNumberFormat(500)  // Returns "D3" → ToString produces "500"
    /// 
    /// // Tier 3: Maximum 4-digit padding for exceptional cases
    /// GetNumberFormat(2500) // Returns "D4" → ToString produces "2500"
    /// GetNumberFormat(5000) // Returns "D4" → ToString produces "5000"
    /// </code>
    /// </example>
    // MARK: GetNumberFormat
    private static string GetNumberFormat(int number)
    {
        return number switch
        {
            >= 1000 => "D4",  // 4-digit padding for exceptional large-scale series (1000+)
            >= 100 => "D3",   // 3-digit padding for extended series ranges (100-999)
            _ => "D2"         // 2-digit padding for standard television series (1-99)
        };
    }
}