using System;
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

                var config = plugin.Configuration;

                config.PosterFill = newConfig.PosterFill;
                config.PosterStyle = newConfig.PosterStyle;
                config.CutoutType = newConfig.CutoutType;
                config.EpisodeFontSize = newConfig.EpisodeFontSize;
                config.TitleFontSize = newConfig.TitleFontSize;
                config.TextColor = newConfig.TextColor;
                config.TextPosition = newConfig.TextPosition;
                config.OverlayColor = newConfig.OverlayColor;

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
    }
}