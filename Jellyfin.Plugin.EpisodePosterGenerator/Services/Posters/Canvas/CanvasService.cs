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
    // CanvasService
    // Creates poster canvases using SkiaSharp through an extract, crop, brighten, and encode pipeline.
    public class CanvasService
    {
        private readonly ILogger<CanvasService> _logger;
        private readonly FFmpegService _ffmpegService;
        private readonly CroppingService _croppingService;
        private readonly BrightnessService _brightnessService;

        // CanvasService
        // Initializes a new instance with required service dependencies.
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

        // GenerateCanvasAsync
        // Generates a poster canvas by extracting a frame, cropping, brightening, and encoding it.
        public async Task<byte[]?> GenerateCanvasAsync(EpisodeMetadata metadata, PosterSettings config)
        {
            if (metadata?.VideoMetadata == null)
            {
                _logger.LogError("Invalid metadata provided to CanvasService");
                return null;
            }

            var videoMeta = metadata.VideoMetadata;
            SKBitmap? canvasBitmap = null;
            string? ffmpegOutputPath = null;

            try
            {
                // Branch: Extract poster from video or create transparent canvas
                if (config.ExtractPoster)
                {
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

                    var croppedBitmap = _croppingService.CropPoster(canvasBitmap, metadata.VideoMetadata, config);
                    if (croppedBitmap != canvasBitmap)
                    {
                        canvasBitmap.Dispose();
                        canvasBitmap = croppedBitmap;
                    }

                    if (config.BrightenHDR > 0)
                    {
                        _logger.LogDebug("Applying HDR brightening: +{Brightness}%", config.BrightenHDR);
                        _brightnessService.BrightenBitmap(canvasBitmap, config.BrightenHDR);
                    }
                }
                else
                {
                    // Branch: Create transparent canvas when not extracting poster
                    canvasBitmap = CreateTransparentCanvas(
                        videoMeta.VideoWidth,
                        videoMeta.VideoHeight,
                        config.PosterFileType
                    ) is byte[] data
                        ? SKBitmap.Decode(data) ?? CreateFallbackCanvas(videoMeta.VideoWidth, videoMeta.VideoHeight)
                        : CreateFallbackCanvas(videoMeta.VideoWidth, videoMeta.VideoHeight);
                }

                return EncodeImage(canvasBitmap, config.PosterFileType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating poster for {SeriesName} - {EpisodeName}",
                    metadata.SeriesName, metadata.EpisodeName);
                return null;
            }
            finally
            {
                canvasBitmap?.Dispose();

                if (!string.IsNullOrEmpty(ffmpegOutputPath) && File.Exists(ffmpegOutputPath))
                {
                    try
                    {
                        File.Delete(ffmpegOutputPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temporary file: {FilePath}", ffmpegOutputPath);
                    }
                }
            }
        }

        // CreateFallbackCanvas
        // Creates an empty bitmap canvas with the specified dimensions.
        private SKBitmap CreateFallbackCanvas(int width, int height)
        {
            _logger.LogDebug("Creating fallback canvas {Width}x{Height}", width, height);
            return new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        }

        // CreateTransparentCanvas
        // Creates a transparent canvas encoded in the specified file format.
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

        // EncodeImage
        // Encodes a bitmap to a byte array in the specified image format.
        private byte[]? EncodeImage(SKBitmap bitmap, PosterFileType fileType)
        {
            var format = fileType switch
            {
                PosterFileType.JPEG => SKEncodedImageFormat.Jpeg,
                PosterFileType.PNG => SKEncodedImageFormat.Png,
                PosterFileType.WEBP => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, 100);

            if (data == null)
            {
                _logger.LogError("Failed to encode image as {FileType}", fileType);
                return null;
            }

            return data.ToArray();
        }
    }
}
