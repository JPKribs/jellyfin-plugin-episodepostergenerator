using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Controllers
{
    [ApiController]
    [Authorize(Policy = "RequiresElevation")]
    [Route("Plugins/EpisodePosterGenerator")]
    public class ConfigurationController : ControllerBase
    {
        // Cache reflection results — PluginConfiguration properties don't change at runtime
        private static readonly PropertyInfo[] ConfigProperties = typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray();

        private readonly ILogger<ConfigurationController> _logger;

        public ConfigurationController(ILogger<ConfigurationController> logger)
        {
            _logger = logger;
        }

        // MARK: GET
        [HttpGet("Configuration")]
        public IActionResult GetConfiguration()
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                _logger.LogError("Plugin instance was null in GET.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Plugin not initialized.");
            }

            return Ok(plugin.Configuration);
        }

        // MARK: POST
        // GetPosterStyles
        // Returns each poster style and its description, read from the generators themselves so the UI
        // no longer hardcodes them.
        [HttpGet("PosterStyles")]
        public IActionResult GetPosterStyles()
        {
            var styles = PreviewService.GetStyleCatalog()
                .Select(g => new { value = g.Style.ToString(), description = g.Description });
            return Ok(styles);
        }

        [HttpPost("Configuration")]
        public IActionResult UpdateConfiguration([FromBody] PluginConfiguration newConfig)
        {
            try
            {
                var plugin = Plugin.Instance;
                if (plugin == null)
                {
                    _logger.LogError("Plugin instance was null in POST.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Plugin not initialized.");
                }

                _logger.LogInformation("Received config: {@newConfig}", newConfig);

                var currentConfig = plugin.Configuration;
                CopyConfigurationProperties(newConfig, currentConfig);

                plugin.SaveConfiguration();

                _logger.LogInformation("Configuration saved successfully.");
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update configuration.");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // MARK: Preview
        [HttpPost("Preview")]
        public IActionResult GeneratePreview([FromBody] PosterSettings settings)
        {
            if (settings == null)
            {
                return BadRequest("Poster settings are required.");
            }

            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                _logger.LogError("Plugin instance was null in Preview.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Plugin not initialized.");
            }

            try
            {
                var imageBytes = plugin.PreviewService.GeneratePreview(settings);
                if (imageBytes == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to render preview.");
                }

                return File(imageBytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate poster preview.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to render preview.");
            }
        }

        // MARK: PreviewComponent
        [HttpGet("Preview/Component/{component}")]
        [AllowAnonymous]
        public IActionResult GetPreviewComponent([FromRoute] string component)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                _logger.LogError("Plugin instance was null in PreviewComponent.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Plugin not initialized.");
            }

            var result = plugin.PreviewService.GetComponentImage(component);
            if (result == null)
            {
                return NotFound();
            }

            return File(result.Value.Bytes, result.Value.ContentType);
        }

        // MARK: ResetHistory
        [HttpPost("ResetHistory")]
        public async Task<IActionResult> ResetHistory()
        {
            try
            {
                var plugin = Plugin.Instance;
                if (plugin == null)
                {
                    _logger.LogError("Plugin instance was null in ResetHistory.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Plugin not initialized.");
                }

                // Wait for database initialization before accessing tracking service
                var dbReady = await plugin.WaitForDatabaseAsync().ConfigureAwait(false);
                if (!dbReady)
                {
                    _logger.LogError("Episode tracking database not initialized in ResetHistory.");
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Database not initialized.");
                }

                // Get count before clearing for response
                var clearedCount = await plugin.TrackingService.GetProcessedCountAsync().ConfigureAwait(false);
                
                // Clear all processed episodes
                await plugin.TrackingService.ClearAllProcessedEpisodesAsync().ConfigureAwait(false);
                
                _logger.LogInformation("Processing history reset - cleared {Count} episodes", clearedCount);
                
                return Ok(new { 
                    success = true, 
                    clearedCount = clearedCount,
                    message = "Processing history cleared successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset processing history");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to reset processing history");
            }
        }

        // MARK: CopyConfigurationProperties
        private void CopyConfigurationProperties(PluginConfiguration source, PluginConfiguration target)
        {
            foreach (var property in ConfigProperties)
            {
                try
                {
                    var value = property.GetValue(source);
                    property.SetValue(target, value);
                }
                catch (Exception ex)
                {
                    var sanitizedName = property.Name.Replace(Environment.NewLine, string.Empty, StringComparison.Ordinal);
                    _logger.LogWarning(ex, "Failed to copy property {PropertyName}", sanitizedName);
                }
            }
        }
    }
}