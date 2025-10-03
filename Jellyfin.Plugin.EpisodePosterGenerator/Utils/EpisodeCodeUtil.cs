using System.Globalization;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Episode and season identification code formatting utility.
/// </summary>
public static class EpisodeCodeUtil
{
    // MARK: FormatEpisodeCode
    public static string FormatEpisodeCode(int seasonNumber, int episodeNumber)
    {
        string seasonFormat = GetNumberFormat(seasonNumber);
        string episodeFormat = GetNumberFormat(episodeNumber);
        
        return $"S{seasonNumber.ToString(seasonFormat, CultureInfo.InvariantCulture)}E{episodeNumber.ToString(episodeFormat, CultureInfo.InvariantCulture)}";
    }

    // MARK: FormatEpisodeText
    public static string FormatEpisodeText(CutoutType cutoutType, int seasonNumber, int episodeNumber)
    {
        return cutoutType switch
        {
            CutoutType.Text => episodeNumber >= 100 
                ? episodeNumber.ToString(CultureInfo.InvariantCulture)
                : NumberUtils.NumberToWords(episodeNumber),
            
            CutoutType.Code => FormatEpisodeCode(seasonNumber, episodeNumber),
            
            _ => episodeNumber >= 100 
                ? episodeNumber.ToString(CultureInfo.InvariantCulture) 
                : NumberUtils.NumberToWords(episodeNumber)
        };
    }

    // MARK: FormatFullText
    public static string FormatFullText(int seasonNumber, int episodeNumber, bool includeSeason, bool includeEpisode)
    {
        if (includeSeason && includeEpisode)
        {
            return $"SEASON {seasonNumber} • EPISODE {episodeNumber}";
        }
        
        if (includeSeason)
        {
            return $"SEASON {seasonNumber}";
        }
        
        if (includeEpisode)
        {
            return $"EPISODE {episodeNumber}";
        }
        
        return string.Empty;
    }

    // MARK: GetNumberFormat
    private static string GetNumberFormat(int number)
    {
        return number switch
        {
            >= 1000 => "D4",
            >= 100 => "D3",
            _ => "D2"
        };
    }
}