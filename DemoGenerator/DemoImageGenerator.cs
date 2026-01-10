using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.DemoGenerator
{
    /// <summary>
    /// Generates demo poster images for all configuration templates using a public domain base image.
    /// </summary>
    public class DemoImageGenerator
    {
        private const string BaseImagePath = "Assets/demo-base.png";
        private const string LogoImagePath = "Assets/demo-logo.png";
        private const string GraphicImagePath = "Assets/demo-graphic.png";
        private const string ExamplesDirectory = "../Examples";

        private const string ShowName = "TV Show";
        private const string EpisodeName = "Episode Name";
        private const int SeasonNumber = 12;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DemoImageGenerator> _logger;
        private readonly CroppingService _croppingService;

        public DemoImageGenerator()
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            _logger = _loggerFactory.CreateLogger<DemoImageGenerator>();
            _croppingService = new CroppingService(_loggerFactory.CreateLogger<CroppingService>());
        }

        public async Task GenerateAllDemosAsync()
        {
            _logger.LogInformation("Starting demo image generation...");

            // Find all template files
            var templateFiles = Directory.GetFiles(ExamplesDirectory, "Template.json", SearchOption.AllDirectories);
            _logger.LogInformation($"Found {templateFiles.Length} template(s) to process");

            foreach (var templateFile in templateFiles)
            {
                try
                {
                    var templateDir = Path.GetDirectoryName(templateFile);
                    var exampleName = Path.GetFileName(templateDir);
                    _logger.LogInformation($"Processing template: {exampleName}");

                    // Generate 3 examples (episodes 1, 2, 3... 10)
                    for (int episodeNumber = 1; episodeNumber <= 10; episodeNumber++)
                    {
                        await GenerateDemoForTemplateAsync(templateFile, templateDir!, episodeNumber);
                        _logger.LogInformation($"  ✓ Generated example{episodeNumber}.png");
                    }

                    _logger.LogInformation($"✓ Generated all demos for: {exampleName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to generate demo for template: {templateFile}");
                }
            }

            _logger.LogInformation("Demo generation complete!");
        }

        private async Task GenerateDemoForTemplateAsync(string templatePath, string templateDir, int episodeNumber)
        {
            // Load template with case-insensitive property matching and enum converter
            var templateJson = await File.ReadAllTextAsync(templatePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            var template = JsonSerializer.Deserialize<PosterTemplate>(templateJson, options);

            if (template?.Settings == null)
            {
                throw new InvalidOperationException($"Invalid template file: {templatePath}");
            }

            var settings = template.Settings;

            // Load and prepare base image
            using var baseImage = SKBitmap.Decode(BaseImagePath);
            if (baseImage == null)
            {
                throw new FileNotFoundException($"Base image not found: {BaseImagePath}");
            }

            // Create mock video metadata for the CroppingService
            var videoMetadata = new VideoMetadata
            {
                VideoWidth = baseImage.Width,
                VideoHeight = baseImage.Height
            };

            // Set logo for Logo poster styles
            if (settings.PosterStyle == PosterStyle.Logo)
            {
                var absoluteLogoPath = Path.GetFullPath(LogoImagePath);
                if (File.Exists(absoluteLogoPath))
                {
                    videoMetadata.SeriesLogoFilePath = absoluteLogoPath;
                }
            }

            // Create mock episode metadata with current episode number
            var metadata = new EpisodeMetadata
            {
                EpisodeName = EpisodeName,
                SeriesName = ShowName,
                SeasonNumber = SeasonNumber,
                EpisodeNumberStart = episodeNumber,
                VideoMetadata = videoMetadata
            };

            // Apply the poster fill/crop logic using CroppingService (same as actual plugin)
            var canvasBitmap = _croppingService.CropPoster(baseImage, videoMetadata, settings);

            // Create appropriate poster generator using the same factory pattern as PosterService
            IPosterGenerator generator = settings.PosterStyle switch
            {
                PosterStyle.Logo => new LogoPosterGenerator(_loggerFactory.CreateLogger<LogoPosterGenerator>()),
                PosterStyle.Numeral => new NumeralPosterGenerator(_loggerFactory.CreateLogger<NumeralPosterGenerator>()),
                PosterStyle.Cutout => new CutoutPosterGenerator(_loggerFactory.CreateLogger<CutoutPosterGenerator>()),
                PosterStyle.Standard => new StandardPosterGenerator(_loggerFactory.CreateLogger<StandardPosterGenerator>()),
                PosterStyle.Frame => new FramePosterGenerator(_loggerFactory.CreateLogger<FramePosterGenerator>()),
                PosterStyle.Brush => new BrushPosterGenerator(_loggerFactory.CreateLogger<BrushPosterGenerator>()),
                _ => new StandardPosterGenerator(_loggerFactory.CreateLogger<StandardPosterGenerator>())
            };

            // Generate the poster and save to the template directory
            var outputPath = Path.Combine(templateDir, $"Example{episodeNumber}.png");
            var result = generator.Generate(canvasBitmap, metadata, settings, outputPath);

            if (canvasBitmap != baseImage)
            {
                canvasBitmap.Dispose();
            }

            if (result == null)
            {
                throw new Exception($"Failed to generate poster for episode {episodeNumber}");
            }
        }

    }
}
