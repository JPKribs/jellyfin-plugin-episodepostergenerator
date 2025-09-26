using System;
using System.IO;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Defines the contract for generating poster images from a Canvas/bitmap and episode metadata.
    /// </summary>
    public interface IPosterGenerator
    {
        /// <summary>
        /// Generates a poster from a provided canvas and episode metadata.
        /// </summary>
        /// <param name="canvas">The pre-rendered canvas/bitmap.</param>
        /// <param name="episodeMetadata">Metadata for the episode.</param>
        /// <param name="config">Plugin configuration guiding the generation.</param>
        /// <param name="outputPath">
        /// Optional. The path to save the generated poster. 
        /// If null or empty, a temporary path will be generated using the configured PosterFileType.
        /// </param>
        /// <returns>The path to the generated poster, or <c>null</c> if generation failed.</returns>
        string? Generate(
            SKBitmap canvas,
            EpisodeMetadata episodeMetadata,
            PluginConfiguration config,
            string? outputPath = null);
    }

    /// <summary>
    /// Base class for poster generators providing safe area and single-point encoding logic.
    /// </summary>
    public abstract class BasePosterGenerator
    {
        protected static float GetSafeAreaMargin(PluginConfiguration config) => config.PosterSafeArea / 100f;

        protected static void ApplySafeAreaConstraints(
            int width, int height, PluginConfiguration config,
            out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop)
        {
            var safeAreaMargin = GetSafeAreaMargin(config);
            safeLeft = width * safeAreaMargin;
            safeTop = height * safeAreaMargin;
            safeWidth = width * (1 - 2 * safeAreaMargin);
            safeHeight = height * (1 - 2 * safeAreaMargin);
        }

        /// <summary>
        /// Centralized poster generation and encoding logic.
        /// </summary>
        protected string? GenerateBase(
            SKBitmap canvas,
            EpisodeMetadata episodeMetadata,
            PluginConfiguration config,
            Func<SKBitmap, SKBitmap> drawCustom,
            string? outputPath = null)
        {
            try
            {
                // Apply generator-specific drawing/cropping
                var processedBitmap = drawCustom(canvas);

                // Determine final output path
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    var extension = config.PosterFileType.ToString().ToLowerInvariant();
                    outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{extension}");
                }

                // Encode final image
                SKEncodedImageFormat format = config.PosterFileType switch
                {
                    PosterFileType.JPEG => SKEncodedImageFormat.Jpeg,
                    PosterFileType.PNG => SKEncodedImageFormat.Png,
                    PosterFileType.WEBP => SKEncodedImageFormat.Webp,
                    PosterFileType.GIF => SKEncodedImageFormat.Gif,
                    _ => SKEncodedImageFormat.Png
                };

                using var image = SKImage.FromBitmap(processedBitmap);
                using var data = image.Encode(format, 95);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using var outputStream = File.OpenWrite(outputPath);
                data.SaveTo(outputStream);

                return outputPath;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Example concrete poster generator using CroppingService.
    /// </summary>
    public class CanvasPosterGenerator : BasePosterGenerator, IPosterGenerator
    {
        private readonly CroppingService _croppingService;
        private readonly ILogger<CanvasPosterGenerator> _logger;

        public CanvasPosterGenerator(CroppingService croppingService, ILogger<CanvasPosterGenerator> logger)
        {
            _croppingService = croppingService;
            _logger = logger;
        }

        public string? Generate(
            SKBitmap canvas,
            EpisodeMetadata episodeMetadata,
            PluginConfiguration config,
            string? outputPath = null)
        {
            try
            {
                return GenerateBase(canvas, episodeMetadata, config, bmp =>
                {
                    // Custom drawing logic: crop using CroppingService
                    var cropped = _croppingService.CropPoster(bmp, episodeMetadata.VideoMetadata, config);

                    // Additional overlays / safe area logic could go here
                    return cropped;
                }, outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate poster for episode {EpisodeName}", episodeMetadata.EpisodeName);
                return null;
            }
        }
    }
}