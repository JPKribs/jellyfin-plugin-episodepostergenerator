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
    // MARK: Generate
    string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config);
}

/// <summary>
/// Provides shared functionality for poster generators, including safe area calculations.
/// </summary>
public abstract class BasePosterGenerator
{
    /// <summary>
    /// Calculates the safe area margin from configuration as a percentage of image dimensions.
    /// </summary>
    /// <param name="config">Plugin configuration containing the PosterSafeArea setting.</param>
    /// <returns>Safe area margin as a decimal percentage (e.g., 5.0 becomes 0.05).</returns>
    // MARK: GetSafeAreaMargin
    protected static float GetSafeAreaMargin(PluginConfiguration config)
    {
        return config.PosterSafeArea / 100f;
    }

    /// <summary>
    /// Calculates the dimensions and position of the safe area within an image using configuration.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="config">Plugin configuration containing safe area settings.</param>
    /// <param name="safeWidth">The resulting width of the safe area.</param>
    /// <param name="safeHeight">The resulting height of the safe area.</param>
    /// <param name="safeLeft">The left offset of the safe area.</param>
    /// <param name="safeTop">The top offset of the safe area.</param>
    // MARK: ApplySafeAreaConstraints
    protected static void ApplySafeAreaConstraints(int width, int height, PluginConfiguration config, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop)
    {
        var safeAreaMargin = GetSafeAreaMargin(config);
        safeLeft = width * safeAreaMargin;
        safeTop = height * safeAreaMargin;
        safeWidth = width * (1 - 2 * safeAreaMargin);
        safeHeight = height * (1 - 2 * safeAreaMargin);
    }

    /// <summary>
    /// Determines whether the given coordinates are within the calculated safe area of an image.
    /// </summary>
    /// <param name="x">The x-coordinate to test.</param>
    /// <param name="y">The y-coordinate to test.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="config">Plugin configuration containing safe area settings.</param>
    /// <returns><c>true</c> if the point lies within the safe area; otherwise, <c>false</c>.</returns>
    // MARK: IsWithinSafeArea
    protected static bool IsWithinSafeArea(float x, float y, int width, int height, PluginConfiguration config)
    {
        ApplySafeAreaConstraints(width, height, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);
        return x >= safeLeft && x <= safeLeft + safeWidth && y >= safeTop && y <= safeTop + safeHeight;
    }
}