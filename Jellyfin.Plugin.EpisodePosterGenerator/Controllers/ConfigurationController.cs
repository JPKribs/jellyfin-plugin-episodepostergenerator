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

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public ConfigurationController(ILogger<ConfigurationController> logger)
        {
            _logger = logger;
        }

        // MARK: GET

        /// <summary>
        /// Retrieves the current plugin configuration.
        /// </summary>
        /// <returns>The plugin configuration or an error response.</returns>
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

        /// <summary>
        /// Updates the plugin configuration.
        /// </summary>
        /// <param name="newConfig">The new configuration to apply.</param>
        /// <returns>Result of the update operation.</returns>
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

                // Copy values from the new config to the current config
                var config = plugin.Configuration;

                config.EnableProvider = newConfig.EnableProvider;
                config.EnableTask = newConfig.EnableTask;

                config.PosterStyle = newConfig.PosterStyle;

                config.CutoutType = newConfig.CutoutType;
                config.CutoutBorder = newConfig.CutoutBorder;

                config.LogoAlignment = newConfig.LogoAlignment;
                config.LogoPosition = newConfig.LogoPosition;
                config.LogoHeight = newConfig.LogoHeight;

                config.ExtractPoster = newConfig.ExtractPoster;

                config.PosterFill = newConfig.PosterFill;
                config.PosterDimensionRatio = newConfig.PosterDimensionRatio;

                config.ShowEpisode = newConfig.ShowEpisode;
                config.EpisodeFontFamily = newConfig.EpisodeFontFamily;
                config.EpisodeFontStyle = newConfig.EpisodeFontStyle;
                config.EpisodeFontSize = newConfig.EpisodeFontSize;
                config.EpisodeFontColor = newConfig.EpisodeFontColor;

                config.ShowTitle = newConfig.ShowTitle;
                config.TitleFontFamily = newConfig.TitleFontFamily;
                config.TitleFontStyle = newConfig.TitleFontStyle;
                config.TitleFontSize = newConfig.TitleFontSize;
                config.TitleFontColor = newConfig.TitleFontColor;

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