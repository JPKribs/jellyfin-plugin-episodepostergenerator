using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Canvas service for creating poster canvases using SkiaSharp
    /// </summary>
    public class CanvasService
    {
        private readonly ILogger<CanvasService> _logger;
        private readonly FFmpegService _ffmpegService;

        // MARK: Constructor
        public CanvasService(
            ILogger<CanvasService> logger,
            FFmpegService ffmpegService
        )
        {
            _logger = logger;
            _ffmpegService = ffmpegService;
        }

        // MARK: GenerateCanvas
        public async Task<byte[]?> GenerateCanvasAsync(EpisodeMetadata metadata, PluginConfiguration config)
        {
            if (metadata?.VideoMetadata == null)
            {
                _logger.LogError("Invalid metadata provided to CanvasService");
                return null;
            }

            try
            {
                var videoMeta = metadata.VideoMetadata;
                _logger.LogDebug("Generating canvas for {Width}x{Height} video", videoMeta.VideoWidth, videoMeta.VideoHeight);

                if (config.ExtractPoster)
                {
                    var outputPath = await _ffmpegService.ExtractSceneAsync(
                        metadata,
                        config,
                        CancellationToken.None
                    ).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(outputPath) || !File.Exists(outputPath))
                    {
                        _logger.LogWarning("FFmpeg did not produce a valid poster file");
                        return null;
                    }

                    return await File.ReadAllBytesAsync(outputPath, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    return CreateTransparentCanvas(
                        videoMeta.VideoWidth,
                        videoMeta.VideoHeight,
                        config.PosterFileType
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate canvas for episode: {EpisodeName}", metadata.EpisodeName);
                return null;
            }
        }

        // MARK: CreateTransparentCanvas
        private byte[]? CreateTransparentCanvas(int width, int height, PosterFileType fileType)
        {
            try
            {
                _logger.LogDebug("Creating transparent canvas {Width}x{Height} as {FileType}", width, height, fileType);

                using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(bitmap);

                canvas.Clear(SKColors.Transparent);

                var imageData = EncodeImage(bitmap, fileType);

                if (imageData != null)
                {
                    _logger.LogDebug("Successfully created transparent canvas: {Size} bytes", imageData.Length);
                }

                return imageData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create transparent canvas");
                return null;
            }
        }

        // MARK: EncodeImage
        private byte[]? EncodeImage(SKBitmap bitmap, PosterFileType fileType)
        {
            var format = GetSkiaFormat(fileType);
            var quality = GetQualityForFormat(fileType);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, quality);

            if (data == null)
            {
                _logger.LogError("Failed to encode image as {FileType}", fileType);
                return null;
            }

            return data.ToArray();
        }

        // MARK: GetSkiaFormat
        private SKEncodedImageFormat GetSkiaFormat(PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.PNG => SKEncodedImageFormat.Png,
                PosterFileType.WEBP => SKEncodedImageFormat.Webp,
                PosterFileType.GIF => SKEncodedImageFormat.Gif,
                PosterFileType.JPEG => SKEncodedImageFormat.Jpeg,
                _ => SKEncodedImageFormat.Jpeg
            };
        }

        // MARK: GetQualityForFormat
        private int GetQualityForFormat(PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.JPEG => 85,
                PosterFileType.WEBP => 80,
                _ => 100
            };
        }
    }
}