using System;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models;

public class BlackInterval
{
    public TimeSpan Start { get; set; }

    public TimeSpan End { get; set; }

    public TimeSpan Duration { get; set; }
}
