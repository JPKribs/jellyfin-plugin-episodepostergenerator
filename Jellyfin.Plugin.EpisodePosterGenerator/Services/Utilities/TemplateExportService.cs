using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class TemplateExportService
    {
        private readonly ILogger<TemplateExportService> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public TemplateExportService(ILogger<TemplateExportService> logger)
        {
            _logger = logger;
        }

        // MARK: ExportTemplate
        public string? ExportTemplate(PosterConfiguration configuration, string exportPath, string? description = null, string? author = null)
        {
            try
            {
                var template = new PosterTemplate
                {
                    Name = configuration.Name,
                    Description = description,
                    Author = author,
                    Version = "1.0",
                    CreatedDate = DateTime.UtcNow,
                    Settings = configuration.Settings
                };

                var json = JsonSerializer.Serialize(template, JsonOptions);
                File.WriteAllText(exportPath, json);

                _logger.LogInformation("Exported template '{Name}' to {Path}", configuration.Name, exportPath);
                return exportPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export template '{Name}'", configuration.Name);
                return null;
            }
        }

        // MARK: ExportTemplateAsString
        public string? ExportTemplateAsString(PosterConfiguration configuration, string? description = null, string? author = null)
        {
            try
            {
                var template = new PosterTemplate
                {
                    Name = configuration.Name,
                    Description = description,
                    Author = author,
                    Version = "1.0",
                    CreatedDate = DateTime.UtcNow,
                    Settings = configuration.Settings
                };

                return JsonSerializer.Serialize(template, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize template '{Name}'", configuration.Name);
                return null;
            }
        }

        // MARK: ImportTemplate
        public PosterConfiguration? ImportTemplate(string json, string? customName = null)
        {
            try
            {
                var template = JsonSerializer.Deserialize<PosterTemplate>(json, JsonOptions);

                if (template == null)
                {
                    _logger.LogError("Failed to deserialize template: result was null");
                    return null;
                }

                var config = new PosterConfiguration
                {
                    Id = Guid.NewGuid(),
                    Name = customName ?? template.Name,
                    Settings = template.Settings,
                    SeriesIds = new List<Guid>(),
                    IsDefault = false
                };

                _logger.LogInformation("Imported template '{Name}' (original: '{OriginalName}')", config.Name, template.Name);
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import template from JSON");
                return null;
            }
        }

        // MARK: ImportTemplateFromFile
        public PosterConfiguration? ImportTemplateFromFile(string filePath, string? customName = null)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                return ImportTemplate(json, customName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import template from file {Path}", filePath);
                return null;
            }
        }
    }
}