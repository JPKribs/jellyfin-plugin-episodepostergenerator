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
    /// Canvas service for creating poster canvases using SkiaSharp.
    /// Pipeline: Extract -> Crop -> Brighten -> Encode
    /// </summary>
    public class CanvasService
    {
        private readonly ILogger<CanvasService> _logger;
        private readonly FFmpegService _ffmpegService;
        private readonly CroppingService _croppingService;
        private readonly BrightnessService _brightnessService;

        // MARK: Constructor
        public CanvasService(
            ILogger<CanvasService> logger,
            FFmpegService ffmpegService,
            CroppingService croppingService,
            BrightnessService brightnessService
        )
        {
            _logger = logger;
            _ffmpegService = ffmpegService;
            _croppingService = croppingService;
            _brightnessService = brightnessService;
        }

        // MARK: GenerateCanvasAsync
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

                SKBitmap canvasBitmap;
                string? ffmpegOutputPath = null;

                if (config.ExtractPoster)
                {
                    // Step 1: Extract frame (dark, uncropped)
                    ffmpegOutputPath = await _ffmpegService.ExtractSceneAsync(
                        metadata,
                        config,
                        CancellationToken.None
                    ).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(ffmpegOutputPath) || !File.Exists(ffmpegOutputPath))
                    {
                        _logger.LogWarning("FFmpeg did not produce a valid poster file");
                        return null;
                    }

                    using var bitmap = SKBitmap.Decode(ffmpegOutputPath);
                    if (bitmap == null)
                    {
                        _logger.LogWarning("Failed to decode FFmpeg output");
                        return null;
                    }

                    canvasBitmap = bitmap.Copy();

                    // Step 2: Apply cropping on dark image (better letterbox detection)
                    var croppedBitmap = _croppingService.CropPoster(canvasBitmap, metadata.VideoMetadata, config);
                    if (croppedBitmap != canvasBitmap)
                    {
                        canvasBitmap.Dispose();
                        canvasBitmap = croppedBitmap;
                    }

                    // Step 3: Check brightness and apply brightening if needed
                    if (config.BrightenHDR > 0)
                    {
                        if (_brightnessService.IsFrameBrightEnough(canvasBitmap))
                        {
                            _logger.LogDebug("Frame is already bright enough, skipping HDR brightening");
                        }
                        else
                        {
                            _logger.LogDebug("Applying HDR brightening: +{Brightness}%", config.BrightenHDR);
                            _brightnessService.BrightenBitmap(canvasBitmap, config.BrightenHDR);
                        }
                    }
                }
                else
                {
                    // Create transparent canvas
                    canvasBitmap = CreateTransparentCanvas(
                        videoMeta.VideoWidth,
                        videoMeta.VideoHeight,
                        config.PosterFileType
                    ) is byte[] data
                        ? SKBitmap.Decode(data) ?? new SKBitmap(videoMeta.VideoWidth, videoMeta.VideoHeight)
                        : new SKBitmap(videoMeta.VideoWidth, videoMeta.VideoHeight);
                }

                // Cleanup FFmpeg temporary file
                if (!string.IsNullOrEmpty(ffmpegOutputPath) && File.Exists(ffmpegOutputPath))
                {
                    try
                    {
                        File.Delete(ffmpegOutputPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary FFmpeg output file: {Path}", ffmpegOutputPath);
                    }
                }

                // Step 4: Encode and return final canvas
                return EncodeImage(canvasBitmap, config.PosterFileType);
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