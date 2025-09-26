using System;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
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
            var properties = typeof(PluginConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (property.CanRead && property.CanWrite)
                {
                    try
                    {
                        var value = property.GetValue(source);
                        property.SetValue(target, value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to copy property {PropertyName}", property.Name);
                    }
                }
            }
        }
    }
}