using System;
using System.Globalization;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

public static class NumberUtils
{
    private static readonly string[] UnitsMap = {
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
        "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN"
    };

    private static readonly string[] TensMap = {
        "ZERO", "TEN", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY"
    };

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
}