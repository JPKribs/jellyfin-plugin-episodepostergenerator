using System;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Controllers;

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
    public ActionResult GetConfigurationPage()
    {
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <title>Episode Poster Generator Configuration</title>
</head>
<body>
    <div id=""EpisodePosterGeneratorConfigPage"" data-role=""page"" class=""page type-interior pluginConfigurationPage"" data-require=""emby-input,emby-button,emby-select,emby-checkbox"">
        <div data-role=""content"">
            <div class=""content-primary"">
                <form id=""EpisodePosterGeneratorConfigForm"">
                    <div class=""sectionTitleContainer flex align-items-center"">
                        <h2 class=""sectionTitle"">Episode Poster Generator Settings</h2>
                    </div>
                    
                    <div class=""verticalSection"">
                        <div class=""checkboxContainer"">
                            <label>
                                <input is=""emby-checkbox"" type=""checkbox"" id=""chkEnablePlugin"" />
                                <span>Enable Episode Poster Generator Plugin</span>
                            </label>
                        </div>
                    </div>

                    <div class=""verticalSection"">
                        <h3 class=""sectionTitle"">Black Scene Detection</h3>
                        
                        <label class=""inputLabel inputLabelUnfocused"" for=""txtBlackThreshold"">Black Detection Threshold:</label>
                        <input is=""emby-input"" type=""number"" id=""txtBlackThreshold"" step=""0.01"" min=""0"" max=""1"" label=""Black Threshold"" />
                        <div class=""fieldDescription"">Pixel luminance threshold for black detection (0.0 = pure black, 1.0 = light black)</div>

                        <label class=""inputLabel inputLabelUnfocused"" for=""txtBlackDuration"">Black Duration Threshold:</label>
                        <input is=""emby-input"" type=""number"" id=""txtBlackDuration"" step=""0.1"" min=""0.1"" label=""Black Duration"" />
                        <div class=""fieldDescription"">Minimum duration (seconds) for a black scene to be detected</div>
                    </div>

                    <div class=""verticalSection"">
                        <h3 class=""sectionTitle"">Overlay Settings</h3>
                        
                        <div class=""checkboxContainer"">
                            <label>
                                <input is=""emby-checkbox"" type=""checkbox"" id=""chkUseOverlay"" />
                                <span>Use Custom Overlay Image</span>
                            </label>
                        </div>

                        <label class=""inputLabel inputLabelUnfocused"" for=""fileOverlayImage"">Upload Overlay Image:</label>
                        <input type=""file"" id=""fileOverlayImage"" accept=""image/*"" />
                        <div class=""fieldDescription"">Upload a PNG or JPG image to overlay on episode frames</div>

                        <label class=""inputLabel inputLabelUnfocused"" for=""txtCurrentOverlay"">Current Overlay:</label>
                        <input is=""emby-input"" type=""text"" id=""txtCurrentOverlay"" readonly />
                        <button is=""emby-button"" type=""button"" id=""btnClearOverlay"" class=""raised"">Clear Overlay</button>
                    </div>

                    <div class=""verticalSection"">
                        <h3 class=""sectionTitle"">Text Settings</h3>
                        
                        <label class=""inputLabel inputLabelUnfocused"" for=""txtEpisodeFontSize"">Episode Number Font Size (SXXEXX):</label>
                        <input is=""emby-input"" type=""number"" id=""txtEpisodeFontSize"" min=""12"" max=""128"" label=""Episode Font Size"" />
                        <div class=""fieldDescription"">Font size for episode number (SXXEXX format)</div>

                        <label class=""inputLabel inputLabelUnfocused"" for=""txtTitleFontSize"">Title Font Size:</label>
                        <input is=""emby-input"" type=""number"" id=""txtTitleFontSize"" min=""12"" max=""128"" label=""Title Font Size"" />
                        <div class=""fieldDescription"">Font size for episode title (smaller text under episode number)</div>

                        <label class=""inputLabel inputLabelUnfocused"" for=""selectTextColor"">Text Color:</label>
                        <select is=""emby-select"" id=""selectTextColor"" label=""Text Color"">
                            <option value=""White"">White</option>
                            <option value=""Black"">Black</option>
                            <option value=""Yellow"">Yellow</option>
                            <option value=""Red"">Red</option>
                            <option value=""Blue"">Blue</option>
                            <option value=""Green"">Green</option>
                        </select>

                        <label class=""inputLabel inputLabelUnfocused"" for=""selectTextPosition"">Text Position:</label>
                        <select is=""emby-select"" id=""selectTextPosition"" label=""Text Position"">
                            <option value=""Bottom"">Bottom Center</option>
                            <option value=""Top"">Top Center</option>
                            <option value=""BottomLeft"">Bottom Left</option>
                            <option value=""BottomRight"">Bottom Right</option>
                        </select>
                    </div>

                    <div class=""verticalSection"">
                        <button is=""emby-button"" type=""submit"" class=""raised button-submit block"">
                            <span>Save</span>
                        </button>
                    </div>
                </form>
            </div>
        </div>
    </div>

    <script type=""text/javascript"">
        (function () {
            var pluginId = ""b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e"";

            document.querySelector('#EpisodePosterGeneratorConfigPage').addEventListener('pageshow', function () {
                Dashboard.showLoadingMsg();
                ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                    document.querySelector('#chkEnablePlugin').checked = config.EnablePlugin || false;
                    document.querySelector('#txtBlackThreshold').value = config.BlackDetectionThreshold || 0.1;
                    document.querySelector('#txtBlackDuration').value = config.BlackDurationThreshold || 0.1;
                    document.querySelector('#chkUseOverlay').checked = config.UseOverlay || false;
                    document.querySelector('#txtCurrentOverlay').value = config.OverlayImagePath || 'No overlay selected';
                    document.querySelector('#txtEpisodeFontSize').value = config.EpisodeFontSize || 32;
                    document.querySelector('#txtTitleFontSize').value = config.TitleFontSize || 24;
                    document.querySelector('#selectTextColor').value = config.TextColor || 'White';
                    document.querySelector('#selectTextPosition').value = config.TextPosition || 'Bottom';
                    Dashboard.hideLoadingMsg();
                });
            });

            document.querySelector('#EpisodePosterGeneratorConfigForm').addEventListener('submit', function (e) {
                Dashboard.showLoadingMsg();
                e.preventDefault();

                var config = {
                    EnablePlugin: document.querySelector('#chkEnablePlugin').checked,
                    BlackDetectionThreshold: parseFloat(document.querySelector('#txtBlackThreshold').value),
                    BlackDurationThreshold: parseFloat(document.querySelector('#txtBlackDuration').value),
                    UseOverlay: document.querySelector('#chkUseOverlay').checked,
                    OverlayImagePath: document.querySelector('#txtCurrentOverlay').value !== 'No overlay selected' ? document.querySelector('#txtCurrentOverlay').value : null,
                    EpisodeFontSize: parseInt(document.querySelector('#txtEpisodeFontSize').value),
                    TitleFontSize: parseInt(document.querySelector('#txtTitleFontSize').value),
                    TextColor: document.querySelector('#selectTextColor').value,
                    TextPosition: document.querySelector('#selectTextPosition').value
                };

                ApiClient.updatePluginConfiguration(pluginId, config).then(function (result) {
                    Dashboard.processPluginConfigurationUpdateResult(result);
                });

                return false;
            });
        })();
    </script>
</body>
</html>";
        return Content(html, "text/html");
    }

    [HttpPost("UploadOverlay")]
    public async Task<IActionResult> UploadOverlay(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, error = "No file uploaded" });
            }

            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                return BadRequest(new { success = false, error = "Plugin not available" });
            }

            var overlayDir = Path.Combine(plugin.DataFolderPath, "overlays");
            Directory.CreateDirectory(overlayDir);

            var fileName = $"overlay_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(overlayDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream).ConfigureAwait(false);
            }

            var config = plugin.Configuration;
            config.OverlayImagePath = fileName;
            config.UseOverlay = true;
            plugin.SaveConfiguration();

            _logger.LogInformation("Overlay uploaded: {FileName}", fileName);

            return Ok(new { success = true, fileName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading overlay");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("ClearOverlay")]
    public IActionResult ClearOverlay()
    {
        try
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                return BadRequest(new { success = false, error = "Plugin not available" });
            }

            var config = plugin.Configuration;
            if (!string.IsNullOrEmpty(config.OverlayImagePath))
            {
                var overlayPath = Path.Combine(plugin.DataFolderPath, "overlays", config.OverlayImagePath);
                if (System.IO.File.Exists(overlayPath))
                {
                    System.IO.File.Delete(overlayPath);
                }
            }

            config.OverlayImagePath = null;
            config.UseOverlay = false;
            plugin.SaveConfiguration();

            _logger.LogInformation("Overlay cleared");

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing overlay");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}