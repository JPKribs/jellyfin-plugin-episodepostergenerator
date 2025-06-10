using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Defines the contract for generating poster images for TV episodes.
/// </summary>
public interface IPosterGenerator
{
    /// <summary>
    /// Generates a poster image based on the specified input image and configuration.
    /// </summary>
    /// <param name="inputImagePath">The path to the input source image.</param>
    /// <param name="outputPath">The desired path for the output image.</param>
    /// <param name="episode">The episode for which the poster is being generated.</param>
    /// <param name="config">The plugin configuration used to guide the generation process.</param>
    /// <returns>The path to the generated image, or <c>null</c> if generation failed.</returns>
    string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config);
}

/// <summary>
/// Provides shared functionality for poster generators, including safe area calculations.
/// </summary>
public abstract class BasePosterGenerator
{
    /// <summary>
    /// Defines the margin used to calculate the safe area as a percentage of the image dimensions.
    /// </summary>
    protected const float SafeAreaMargin = 0.05f;

    /// <summary>
    /// Calculates the dimensions and position of the safe area within an image.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="safeWidth">The resulting width of the safe area.</param>
    /// <param name="safeHeight">The resulting height of the safe area.</param>
    /// <param name="safeLeft">The left offset of the safe area.</param>
    /// <param name="safeTop">The top offset of the safe area.</param>
    protected static void ApplySafeAreaConstraints(int width, int height, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop)
    {
        safeLeft = width * SafeAreaMargin;
        safeTop = height * SafeAreaMargin;
        safeWidth = width * (1 - 2 * SafeAreaMargin);
        safeHeight = height * (1 - 2 * SafeAreaMargin);
    }

    /// <summary>
    /// Determines whether the given coordinates are within the calculated safe area of an image.
    /// </summary>
    /// <param name="x">The x-coordinate to test.</param>
    /// <param name="y">The y-coordinate to test.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns><c>true</c> if the point lies within the safe area; otherwise, <c>false</c>.</returns>
    protected static bool IsWithinSafeArea(float x, float y, int width, int height)
    {
        ApplySafeAreaConstraints(width, height, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);
        return x >= safeLeft && x <= safeLeft + safeWidth && y >= safeTop && y <= safeTop + safeHeight;
    }
}