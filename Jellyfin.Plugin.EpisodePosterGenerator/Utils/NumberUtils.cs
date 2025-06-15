using System;
using System.Globalization;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Specialized utility class for converting numeric values into formatted text representations for poster generation.
/// Provides comprehensive number-to-text conversion capabilities supporting both English word representations
/// and classical Roman numeral formatting, specifically optimized for episode numbering in media poster designs.
/// 
/// This utility serves the poster generation system by converting episode numbers into visually appealing
/// text formats for different poster styles:
/// - Cutout Style: Uses English words (e.g., "THREE") for dramatic text cutout effects
/// - Numeral Style: Uses Roman numerals (e.g., "III") for classical, elegant episode identification
/// - Standard/Logo Styles: Falls back to numeric strings for conventional episode numbering
/// 
/// Key Design Principles:
/// - Culture-invariant number formatting to ensure consistent output across different system locales
/// - Uppercase text output optimized for poster typography and visual impact
/// - Graceful degradation for numbers outside supported ranges (falls back to numeric strings)
/// - Performance optimization through pre-computed lookup arrays for fast conversion
/// - Memory-efficient static implementation suitable for high-frequency batch processing
/// 
/// The class handles edge cases including negative numbers and values outside supported ranges,
/// ensuring robust operation during automated poster generation workflows.
/// </summary>
public static class NumberUtils
{
    /// <summary>
    /// Pre-computed lookup array for numbers 0-19 in uppercase English word format.
    /// This array covers all single-digit numbers and the irregular English number names
    /// from ten through nineteen that don't follow standard tens+units patterns.
    /// 
    /// The array is designed for optimal performance during repeated conversions by eliminating
    /// the need for string concatenation or complex logic for these commonly-used values.
    /// Uppercase formatting ensures consistency with poster typography requirements where
    /// bold, prominent text is preferred for visual impact.
    /// 
    /// Index corresponds directly to numeric value: UnitsMap[5] returns "FIVE".
    /// </summary>
    private static readonly string[] UnitsMap = {
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
        "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN"
    };

    /// <summary>
    /// Pre-computed lookup array for tens values (0, 10, 20, ..., 90) in uppercase English word format.
    /// This array enables efficient conversion of the tens portion of two-digit numbers by providing
    /// direct lookup access to tens values without requiring mathematical calculation or string manipulation.
    /// 
    /// The array structure accommodates the mathematical approach used in NumberToWords where
    /// two-digit numbers are decomposed into tens and units components. The tens component is
    /// accessed by integer division (number / 10), while units are handled separately.
    /// 
    /// Index represents the tens digit: TensMap[3] returns "THIRTY" for numbers 30-39.
    /// Index 0 contains "ZERO" but is not used in normal tens processing.
    /// </summary>
    private static readonly string[] TensMap = {
        "ZERO", "TEN", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY"
    };

    /// <summary>
    /// Converts integer values into their English word representation using optimized lookup algorithms.
    /// This method provides comprehensive number-to-text conversion specifically designed for episode
    /// numbering in poster generation, with special handling for negative values and large numbers.
    /// 
    /// Conversion Algorithm:
    /// 1. Negative numbers: Recursively processes absolute value with "MINUS" prefix
    /// 2. Numbers 0-19: Direct lookup from UnitsMap for optimal performance
    /// 3. Numbers 20-99: Mathematical decomposition into tens and units components
    /// 4. Numbers ≥100: Falls back to culture-invariant numeric string representation
    /// 
    /// The algorithm is optimized for the typical episode numbering range (1-99) while providing
    /// graceful handling of edge cases. For poster generation, numbers above 99 are rare enough
    /// that the performance impact of ToString() conversion is negligible.
    /// 
    /// Examples:
    /// - NumberToWords(5) returns "FIVE"
    /// - NumberToWords(23) returns "TWENTY THREE"
    /// - NumberToWords(-7) returns "MINUS SEVEN"
    /// - NumberToWords(150) returns "150"
    /// </summary>
    /// <param name="number">Integer value to convert, supporting negative values and any magnitude.</param>
    /// <returns>
    /// Uppercase English word representation for numbers 0-99, with "MINUS" prefix for negatives.
    /// Numbers ≥100 return culture-invariant numeric string representation.
    /// </returns>
    // MARK: NumberToWords
    public static string NumberToWords(int number)
    {
        // Handle negative numbers by recursively processing absolute value with prefix
        if (number < 0)
            return "MINUS " + NumberToWords(Math.Abs(number));

        // Direct lookup for numbers 0-19 using pre-computed array for optimal performance
        if (number < 20)
            return UnitsMap[number];

        // Handle two-digit numbers (20-99) using mathematical decomposition
        if (number < 100)
        {
            // Extract tens component using integer division
            string tens = TensMap[number / 10];
            
            // Extract units component using modulo operation, with conditional spacing
            string units = number % 10 > 0 ? " " + UnitsMap[number % 10] : "";
            
            // Combine tens and units with appropriate spacing
            return tens + units;
        }

        // Fallback for large numbers: return culture-invariant numeric representation
        // Uses InvariantCulture to ensure consistent formatting across different system locales
        return number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts integer values into classical Roman numeral representation for elegant poster typography.
    /// This method implements the traditional Roman numeral system using subtractive notation
    /// for values within the practical range of 1-3999, providing sophisticated episode numbering
    /// suitable for classical or premium poster designs.
    /// 
    /// Roman Numeral Algorithm:
    /// The conversion uses a positional decomposition approach where each decimal place
    /// (thousands, hundreds, tens, units) is processed independently using pre-computed
    /// lookup arrays containing the appropriate Roman numeral representations.
    /// 
    /// Mathematical Process:
    /// 1. Thousands place: number / 1000 → {nothing, M, MM, MMM}
    /// 2. Hundreds place: (number % 1000) / 100 → {nothing, C, CC, CCC, CD, D, DC, DCC, DCCC, CM}
    /// 3. Tens place: (number % 100) / 10 → {nothing, X, XX, XXX, XL, L, LX, LXX, LXXX, XC}
    /// 4. Units place: number % 10 → {nothing, I, II, III, IV, V, VI, VII, VIII, IX}
    /// 
    /// The arrays include subtractive notation combinations (IV, IX, XL, XC, CD, CM) that represent
    /// standard Roman numeral shortcuts, ensuring authentic and readable output.
    /// 
    /// Range Limitations:
    /// - Supports 1-3999 (practical limit for traditional Roman numerals)
    /// - Values ≤0 or >3999 fall back to numeric string representation
    /// - This range covers virtually all realistic episode numbering scenarios
    /// 
    /// Examples:
    /// - NumberToRomanNumeral(4) returns "IV"
    /// - NumberToRomanNumeral(23) returns "XXIII"
    /// - NumberToRomanNumeral(1994) returns "MCMXCIV"
    /// - NumberToRomanNumeral(5000) returns "5000"
    /// </summary>
    /// <param name="number">Integer value to convert to Roman numerals, typically an episode number.</param>
    /// <returns>
    /// Classical Roman numeral representation for values 1-3999 using traditional subtractive notation.
    /// Values outside this range return culture-invariant numeric string representation.
    /// </returns>
    // MARK: NumberToRomanNumeral
    public static string NumberToRomanNumeral(int number)
    {
        // Validate input range: Roman numerals are traditionally limited to 1-3999
        if (number <= 0 || number > 3999)
            return number.ToString(CultureInfo.InvariantCulture);

        // Pre-computed lookup arrays for each decimal place using authentic Roman numeral notation
        // Each array contains all possible values for that decimal position, including subtractive forms
        
        // Thousands place: covers 0-3000 (empty, M, MM, MMM)
        string[] thousands = { "", "M", "MM", "MMM" };
        
        // Hundreds place: covers 0-900 including subtractive notation (CD=400, CM=900)
        string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
        
        // Tens place: covers 0-90 including subtractive notation (XL=40, XC=90)
        string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
        
        // Units place: covers 0-9 including subtractive notation (IV=4, IX=9)
        string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };

        // Perform positional decomposition and concatenate results from each decimal place
        return thousands[number / 1000] +                  // Extract thousands digit
               hundreds[(number % 1000) / 100] +           // Extract hundreds digit
               tens[(number % 100) / 10] +                 // Extract tens digit
               ones[number % 10];                          // Extract units digit
    }
}