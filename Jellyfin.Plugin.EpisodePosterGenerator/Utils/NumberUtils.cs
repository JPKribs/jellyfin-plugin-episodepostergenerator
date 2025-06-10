using System;
using System.Globalization;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Utility class for converting numbers into their English word representations (limited to 0-99).
/// </summary>
public static class NumberUtils
{
    private static readonly string[] UnitsMap = {
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
        "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN"
    };

    private static readonly string[] TensMap = {
        "ZERO", "TEN", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY"
    };

    /// <summary>
    /// Converts an integer number into its English word representation (uppercase).
    /// Supports numbers from negative values up to 99; numbers 100 and above are returned as digits.
    /// </summary>
    /// <param name="number">Number to convert.</param>
    /// <returns>English word representation of the number in uppercase, or the number as string if >= 100.</returns>
    public static string NumberToWords(int number)
    {
        if (number < 0)
            return "MINUS " + NumberToWords(Math.Abs(number));

        if (number < 20)
            return UnitsMap[number];

        if (number < 100)
        {
            string tens = TensMap[number / 10];
            string units = number % 10 > 0 ? " " + UnitsMap[number % 10] : "";
            return tens + units;
        }

        // For 100 and above, return the number as string
        return number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts an integer to a Roman numeral string.
    /// Returns the number as a string if outside the 1-3999 range.
    /// </summary>
    /// <returns>English word representation of the number in uppercase Roman numerals.</returns>
    public static string NumberToRomanNumeral(int number)
    {
        if (number <= 0 || number > 3999)
            return number.ToString(CultureInfo.InvariantCulture);

        string[] thousands = { "", "M", "MM", "MMM" };
        string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
        string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
        string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };

        return thousands[number / 1000] +
               hundreds[(number % 1000) / 100] +
               tens[(number % 100) / 10] +
               ones[number % 10];
    }
}