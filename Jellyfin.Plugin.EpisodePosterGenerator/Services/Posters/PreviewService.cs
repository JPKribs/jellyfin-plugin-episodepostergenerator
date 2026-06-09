using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Renders a single poster against bundled demo artwork so the configuration UI can show
    /// a live preview of the current settings. Runs the exact crop + generator pipeline the
    /// scheduled task uses, but sourced from embedded sample images rather than a real episode,
    /// so it requires no media, no IMediaEncoder, and no library access.
    /// </summary>
    public class PreviewService
    {
        // Fixed sample metadata so previews read like a real episode card regardless of library state.
        private const string ShowName = "TV Show";
        private const string EpisodeName = "Episode Name";
        private const int SeasonNumber = 12;
        private const int EpisodeNumber = 7;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<PreviewService> _logger;
        private readonly CroppingService _croppingService;
        private readonly object _assetLock = new object();
        private string? _assetDir;

        public PreviewService(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<PreviewService>();
            _croppingService = new CroppingService(_loggerFactory.CreateLogger<CroppingService>());
        }

        // GeneratePreview
        // Renders the supplied settings against the demo artwork and returns JPEG bytes, or null on failure.
        public byte[]? GeneratePreview(PosterSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var assetDir = EnsureAssetsExtracted();
            var basePath = Path.Combine(assetDir, "demo-base.png");

            using var baseImage = SKBitmap.Decode(basePath);
            if (baseImage == null)
            {
                _logger.LogError("Failed to decode preview base image at {Path}", basePath);
                return null;
            }

            var videoMetadata = new VideoMetadata
            {
                VideoWidth = baseImage.Width,
                VideoHeight = baseImage.Height
            };

            // The Logo and Split styles pull artwork from the video metadata paths; point them at
            // the bundled demo art so those styles preview faithfully instead of falling back to text.
            if (settings.PosterStyle == PosterStyle.Logo)
            {
                videoMetadata.SeriesLogoFilePath = Path.Combine(assetDir, "demo-logo.png");
            }

            if (settings.PosterStyle == PosterStyle.Split)
            {
                videoMetadata.SeriesPosterFilePath = Path.Combine(assetDir, "demo-poster.jpg");
            }

            // GraphicPath is left as the user configured it — RenderGraphics null/exists-checks
            // the path and simply skips the layer if it isn't reachable, so the preview reflects
            // the real static graphic the user provided.

            var metadata = new EpisodeMetadata
            {
                EpisodeName = EpisodeName,
                SeriesName = ShowName,
                SeasonNumber = SeasonNumber,
                EpisodeNumberStart = EpisodeNumber,
                VideoMetadata = videoMetadata
            };

            var outputPath = Path.Combine(Path.GetTempPath(), $"epg-preview-{Guid.NewGuid():N}.jpg");

            try
            {
                var result = settings.CanvasSource == CanvasSource.None
                    ? RenderTransparentPoster(baseImage.Width, baseImage.Height, metadata, settings, outputPath)
                    : RenderPoster(baseImage, metadata, settings, outputPath);
                if (result == null || !File.Exists(outputPath))
                {
                    _logger.LogWarning("Preview generation returned no output for style {Style}", settings.PosterStyle);
                    return null;
                }

                return File.ReadAllBytes(outputPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(ex, "Failed to delete temporary preview file {Path}", outputPath);
                }
            }
        }

        // RenderPoster
        // Shared render path: crops the base image, selects the style's generator, and renders to
        // outputPath. Returns the generator's result path (or null). This is the single copy of the
        // crop + generate pipeline — both the live preview and the offline demo image generator call it.
        public string? RenderPoster(SKBitmap baseImage, EpisodeMetadata metadata, PosterSettings settings, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(baseImage);
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(settings);

            var canvas = _croppingService.CropPoster(baseImage, metadata.VideoMetadata, settings);
            try
            {
                var generator = CreateGenerator(settings.PosterStyle, _loggerFactory);
                return generator.Generate(canvas, metadata, settings, outputPath);
            }
            finally
            {
                if (!ReferenceEquals(canvas, baseImage))
                {
                    canvas.Dispose();
                }
            }
        }

        // RenderTransparentPoster
        // Mirrors the runtime CanvasSource.None path: renders the style generator over a blank
        // transparent canvas (no crop, since cropping a transparent bitmap would trip letterbox
        // detection) so the preview honestly reflects a poster with no background image.
        private string? RenderTransparentPoster(int width, int height, EpisodeMetadata metadata, PosterSettings settings, string outputPath)
        {
            using var canvas = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var skCanvas = new SKCanvas(canvas))
            {
                skCanvas.Clear(SKColors.Transparent);
            }

            var generator = CreateGenerator(settings.PosterStyle, _loggerFactory);
            return generator.Generate(canvas, metadata, settings, outputPath);
        }

        // CreateGenerator
        // Maps a poster style to its generator implementation. Single source of truth for style
        // selection, shared by RenderPoster (preview + demos) and the runtime PosterService.
        public static IPosterGenerator CreateGenerator(PosterStyle style, ILoggerFactory loggerFactory)
        {
            return style switch
            {
                PosterStyle.Logo => new LogoPosterGenerator(loggerFactory.CreateLogger<LogoPosterGenerator>()),
                PosterStyle.Numeral => new NumeralPosterGenerator(loggerFactory.CreateLogger<NumeralPosterGenerator>()),
                PosterStyle.Cutout => new CutoutPosterGenerator(loggerFactory.CreateLogger<CutoutPosterGenerator>()),
                PosterStyle.Standard => new StandardPosterGenerator(loggerFactory.CreateLogger<StandardPosterGenerator>()),
                PosterStyle.Frame => new FramePosterGenerator(loggerFactory.CreateLogger<FramePosterGenerator>()),
                PosterStyle.Brush => new BrushPosterGenerator(loggerFactory.CreateLogger<BrushPosterGenerator>()),
                PosterStyle.Split => new SplitPosterGenerator(loggerFactory.CreateLogger<SplitPosterGenerator>()),
                _ => new StandardPosterGenerator(loggerFactory.CreateLogger<StandardPosterGenerator>())
            };
        }

        // GetStyleCatalog
        // Returns one generator per poster style so the configuration UI can read each style's own
        // description instead of keeping a duplicate copy. Built from the same CreateGenerator mapping.
        public static IReadOnlyList<IPosterGenerator> GetStyleCatalog()
        {
            return Enum.GetValues<PosterStyle>()
                .Select(style => CreateGenerator(style, NullLoggerFactory.Instance))
                .ToList();
        }

        // GetComponentImage
        // Returns the raw bytes (and content type) of a single demo input component so the config
        // UI can show the user what feeds the preview. Returns null for an unknown component.
        public (byte[] Bytes, string ContentType)? GetComponentImage(string component)
        {
            var fileName = component switch
            {
                "canvas" => "demo-base.png",
                "poster" => "demo-poster.jpg",
                "logo" => "demo-logo.png",
                "graphic" => "demo-graphic.png",
                _ => null
            };

            if (fileName == null)
            {
                return null;
            }

            var resource = $"{typeof(Plugin).Namespace}.Assets.Demo.{fileName}";
            using var stream = typeof(PreviewService).Assembly.GetManifestResourceStream(resource);
            if (stream == null)
            {
                _logger.LogWarning("Embedded preview component not found: {Resource}", resource);
                return null;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var contentType = fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/png";
            return (ms.ToArray(), contentType);
        }

        // GetDemoAssetDirectory
        // Extracts (once) and returns the directory holding the bundled demo artwork:
        // demo-base.png, demo-logo.png, demo-graphic.png, demo-poster.jpg. This is the single
        // source of the demo art — the offline example generator reads from here so it stays in
        // lockstep with the live preview instead of keeping its own copy on disk.
        public string GetDemoAssetDirectory()
        {
            return EnsureAssetsExtracted();
        }

        // EnsureAssetsExtracted
        // Materializes the embedded demo artwork to a temp folder once, since the crop and generator
        // pipeline reads logo/poster/graphic art from file paths rather than streams.
        private string EnsureAssetsExtracted()
        {
            if (_assetDir != null)
            {
                return _assetDir;
            }

            lock (_assetLock)
            {
                if (_assetDir != null)
                {
                    return _assetDir;
                }

                var dir = Path.Combine(Path.GetTempPath(), "epg-preview-assets");
                Directory.CreateDirectory(dir);

                ExtractAsset("demo-base.png", dir);
                ExtractAsset("demo-logo.png", dir);
                ExtractAsset("demo-graphic.png", dir);
                ExtractAsset("demo-poster.jpg", dir);

                _assetDir = dir;
                return dir;
            }
        }

        // ExtractAsset
        // Copies a single embedded demo asset to disk, overwriting any stale copy from a prior version.
        private void ExtractAsset(string fileName, string dir)
        {
            var resource = $"{typeof(Plugin).Namespace}.Assets.Demo.{fileName}";
            using var stream = typeof(PreviewService).Assembly.GetManifestResourceStream(resource);
            if (stream == null)
            {
                _logger.LogWarning("Embedded preview asset not found: {Resource}", resource);
                return;
            }

            var dest = Path.Combine(dir, fileName);
            using var file = File.Create(dest);
            stream.CopyTo(file);
        }
    }
}
