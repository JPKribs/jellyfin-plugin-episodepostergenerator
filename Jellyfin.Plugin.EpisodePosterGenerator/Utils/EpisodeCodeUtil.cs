using System.Globalization;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Utility class for formatting episode and season codes in standardized formats.
/// Provides methods for generating consistent episode identifiers with appropriate zero-padding.
/// </summary>
public static class EpisodeCodeUtil
{
    /// <summary>
    /// Formats episode and season numbers into S##E## format with appropriate zero-padding.
    /// Uses 2-digit padding by default, but expands to accommodate larger numbers up to 4 digits.
    /// Ensures consistent formatting across different episode numbering schemes.
    /// </summary>
    /// <param name="seasonNumber">Season number to format (e.g., 1, 12, 100, 1000).</param>
    /// <param name="episodeNumber">Episode number to format (e.g., 5, 25, 150, 1500).</param>
    /// <returns>Formatted episode code string in S##E## format.</returns>
    /// <example>
    /// <code>
    /// EpisodeCodeUtil.FormatEpisodeCode(1, 5)     // Returns "S01E05"
    /// EpisodeCodeUtil.FormatEpisodeCode(10, 25)   // Returns "S10E25"
    /// EpisodeCodeUtil.FormatEpisodeCode(1, 100)   // Returns "S01E100"
    /// EpisodeCodeUtil.FormatEpisodeCode(100, 5)   // Returns "S100E05"
    /// EpisodeCodeUtil.FormatEpisodeCode(1000, 5)  // Returns "S1000E05"
    /// EpisodeCodeUtil.FormatEpisodeCode(1, 1500)  // Returns "S01E1500"
    /// </code>
    /// </example>
    // MARK: FormatEpisodeCode
    public static string FormatEpisodeCode(int seasonNumber, int episodeNumber)
    {
        // Determine padding based on number size - use minimum required digits
        string seasonFormat = GetNumberFormat(seasonNumber);
        string episodeFormat = GetNumberFormat(episodeNumber);
        
        return $"S{seasonNumber.ToString(seasonFormat, CultureInfo.InvariantCulture)}E{episodeNumber.ToString(episodeFormat, CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Formats episode text for cutout display according to the selected CutoutType.
    /// Text type converts numbers to words (e.g., "THREE"), while Code type uses episode codes (e.g., "S01E03").
    /// For episodes 100 and above, always uses numeric representation regardless of type for Text format.
    /// </summary>
    /// <param name="cutoutType">Type of cutout text formatting to apply.</param>
    /// <param name="seasonNumber">Season number for code formatting.</param>
    /// <param name="episodeNumber">Episode number to convert.</param>
    /// <returns>Formatted episode text string for cutout display.</returns>
    /// <example>
    /// <code>
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Text, 1, 3)    // Returns "THREE"
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Code, 1, 3)    // Returns "S01E03"
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Text, 1, 100)  // Returns "100"
    /// EpisodeCodeUtil.FormatEpisodeText(CutoutType.Code, 1, 1500) // Returns "S01E1500"
    /// </code>
    /// </example>
    // MARK: FormatEpisodeText
    public static string FormatEpisodeText(CutoutType cutoutType, int seasonNumber, int episodeNumber)
    {
        return cutoutType switch
        {
            // Convert episode number to English words, or use digits for 100+
            CutoutType.Text => episodeNumber >= 100 
                ? episodeNumber.ToString(CultureInfo.InvariantCulture) 
                : NumberUtils.NumberToWords(episodeNumber),
            
            // Format as season/episode code using consistent formatting
            CutoutType.Code => FormatEpisodeCode(seasonNumber, episodeNumber),
            
            // Default fallback to text format
            _ => episodeNumber >= 100 
                ? episodeNumber.ToString(CultureInfo.InvariantCulture) 
                : NumberUtils.NumberToWords(episodeNumber)
        };
    }

    /// <summary>
    /// Determines the appropriate number format string based on the magnitude of the number.
    /// Uses minimum required digits with zero-padding: 2 digits for 1-99, 3 digits for 100-999, 4 digits for 1000+.
    /// </summary>
    /// <param name="number">Number to determine formatting for.</param>
    /// <returns>Format string for use with ToString() method.</returns>
    /// <example>
    /// <code>
    /// GetNumberFormat(5)    // Returns "D2" (for 05)
    /// GetNumberFormat(150)  // Returns "D3" (for 150)
    /// GetNumberFormat(2500) // Returns "D4" (for 2500)
    /// </code>
    /// </example>
    // MARK: GetNumberFormat
    private static string GetNumberFormat(int number)
    {
        return number switch
        {
            >= 1000 => "D4",  // 4 digits for 1000 and above
            >= 100 => "D3",   // 3 digits for 100-999
            _ => "D2"         // 2 digits for 1-99
        };
    }
}