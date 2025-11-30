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
                // Phase 1: Extract/Create Canvas
                if (config.ExtractPoster)
                {
                    // Step 1: Extract bright frame (now includes brightness retry logic)
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

                    // Step 2: Apply cropping (letterbox/pillarbox detection)
                    var croppedBitmap = _croppingService.CropPoster(canvasBitmap, metadata.VideoMetadata, config);
                    if (croppedBitmap != canvasBitmap)
                    {
                        canvasBitmap.Dispose();
                        canvasBitmap = croppedBitmap;
                    }

                    // Step 3: Apply HDR brightening if configured (always apply if > 0, no brightness check)
                    if (config.BrightenHDR > 0)
                    {
                        _logger.LogDebug("Applying HDR brightening: +{Brightness}%", config.BrightenHDR);
                        _brightnessService.BrightenBitmap(canvasBitmap, config.BrightenHDR);
                    }
                }
                else
                {
                    // Create transparent canvas when not extracting poster
                    canvasBitmap = CreateTransparentCanvas(
                        videoMeta.VideoWidth,
                        videoMeta.VideoHeight,
                        config.PosterFileType
                    ) is byte[] data
                        ? SKBitmap.Decode(data) ?? CreateFallbackCanvas(videoMeta.VideoWidth, videoMeta.VideoHeight)
                        : CreateFallbackCanvas(videoMeta.VideoWidth, videoMeta.VideoHeight);
                }

                // Phase 2: Apply poster style rendering
                /*var styledBitmap = await _posterGeneratorService.GeneratePosterAsync(canvasBitmap, metadata, config);
                if (styledBitmap == null)
                {
                    _logger.LogError("Failed to generate styled poster");
                    return null;
                }

                if (styledBitmap != canvasBitmap)
                {
                    canvasBitmap?.Dispose();
                    canvasBitmap = styledBitmap;
                }*/

                // Phase 3: Encode final image
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
                // Cleanup
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

        // MARK: CreateFallbackCanvas
        private SKBitmap CreateFallbackCanvas(int width, int height)
        {
            _logger.LogDebug("Creating fallback canvas {Width}x{Height}", width, height);
            return new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
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