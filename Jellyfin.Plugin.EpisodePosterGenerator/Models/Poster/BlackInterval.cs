using System;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models;

public class BlackInterval
{
    /// <summary>
    /// Starting time point of the black frames in an episode.
    /// </summary>
    public TimeSpan Start { get; set; }

    /// <summary>
    /// Ending time point of the black frames in an episode.
    /// </summary>
    public TimeSpan End { get; set; }

    /// <summary>
    /// Full duration of the black frames in an episode.
    /// </summary>
    public TimeSpan Duration { get; set; }
}