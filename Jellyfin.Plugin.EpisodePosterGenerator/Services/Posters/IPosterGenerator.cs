using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

public interface IPosterGenerator
{
    string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config);
}

public interface ICutoutPosterGenerator
{
    string? Generate(string inputImagePath, string outputPath, int seasonNumber, int episodeNumber, string episodeTitle, PluginConfiguration config);
}

public interface INumeralPosterGenerator
{
    string? Generate(string inputImagePath, string outputPath, int episodeNumber, string episodeTitle, PluginConfiguration config);
}

public abstract class BasePosterGenerator
{
    protected const float SafeAreaMargin = 0.05f;
    
    protected static void ApplySafeAreaConstraints(int width, int height, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop)
    {
        safeLeft = width * SafeAreaMargin;
        safeTop = height * SafeAreaMargin;
        safeWidth = width * (1 - 2 * SafeAreaMargin);
        safeHeight = height * (1 - 2 * SafeAreaMargin);
    }
    
    protected static bool IsWithinSafeArea(float x, float y, int width, int height)
    {
        ApplySafeAreaConstraints(width, height, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);
        return x >= safeLeft && x <= safeLeft + safeWidth && y >= safeTop && y <= safeTop + safeHeight;
    }
}