var EpisodePosterGeneratorConfig = {

    // MARK: pluginId
    pluginId: 'b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e',

    // MARK: updateVisibility
    updateVisibility: function() {
        const posterStyle = document.getElementById('selectPosterStyle').value;

        // Handle poster style specific hiding (Logo and Cutout variables)
        document.querySelectorAll('[data-poster-styles]').forEach(element => {
            const supportedStyles = element.getAttribute('data-poster-styles').split(',');
            const isVisible = supportedStyles.includes(posterStyle);
            element.style.display = isVisible ? 'block' : 'none';
        });

        // Handle styles that should be hidden for certain poster types
        document.querySelectorAll('[data-hide-for-styles]').forEach(element => {
            const hiddenStyles = element.getAttribute('data-hide-for-styles').split(',');
            const shouldHide = hiddenStyles.includes(posterStyle);
            element.style.display = shouldHide ? 'none' : 'block';
        });

        // Handle multiple dependencies (comma-separated)
        document.querySelectorAll('[data-depends-on]').forEach(element => {
            const dependencies = element.getAttribute('data-depends-on').split(',');
            let allDependenciesMet = true;
            
            dependencies.forEach(dependencyId => {
                const dependencyElement = document.getElementById(dependencyId.trim());
                if (!dependencyElement || !dependencyElement.checked) {
                    allDependenciesMet = false;
                }
            });
            
            element.style.display = allDependenciesMet ? 'block' : 'none';
        });

        // For Cutout and Numeral styles, ensure ShowEpisode is always true
        const showEpisodeCheckbox = document.getElementById('chkShowEpisode');
        if (posterStyle === 'Cutout' || posterStyle === 'Numeral') {
            showEpisodeCheckbox.checked = true;
        }
    },

    // MARK: loadConfig
    loadConfig: function () {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(this.pluginId).then(function (config) {
            // Use data attributes to automatically map config to form elements
            document.querySelectorAll('[data-config]').forEach(element => {
                const configKey = element.getAttribute('data-config');
                const configValue = config[configKey];
                
                if (element.type === 'checkbox') {
                    element.checked = configValue !== false;
                } else if (element.getAttribute('data-type') === 'number') {
                    element.value = configValue || 0;
                } else {
                    element.value = configValue || '';
                }
            });

            // Handle color pickers and alpha sliders
            EpisodePosterGeneratorConfig.initializeColorControls();
            
            EpisodePosterGeneratorConfig.updateVisibility();
            Dashboard.hideLoadingMsg();
        }).catch(function (error) {
            console.error('Failed to load config:', error);
            EpisodePosterGeneratorConfig.updateVisibility();
            Dashboard.hideLoadingMsg();
        });
    },

    // MARK: saveConfig
    saveConfig: function () {
        Dashboard.showLoadingMsg();
        var config = {};
        
        // Use data attributes to automatically map form elements to config
        document.querySelectorAll('[data-config]').forEach(element => {
            const configKey = element.getAttribute('data-config');
            
            if (element.type === 'checkbox') {
                config[configKey] = element.checked;
            } else if (element.getAttribute('data-type') === 'number') {
                config[configKey] = parseFloat(element.value) || 0;
            } else {
                config[configKey] = element.value;
            }
        });

        ApiClient.updatePluginConfiguration(this.pluginId, config).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
        });
    },

    // MARK: resetHistory
    resetHistory: function() {
        // Create confirmation dialog
        const confirmationHtml = `
            <div class="reset-warning">
                <h3>⚠️ Warning</h3>
                <p>This will permanently delete all episode processing history.</p>
                <p>After resetting, all episodes will be reprocessed on the next run, which may take considerable time for large libraries.</p>
                <p><strong>Are you sure you want to continue?</strong></p>
            </div>
        `;
        
        const dialog = Dashboard.confirm(confirmationHtml, 'Reset Processing History', function(confirmed) {
            if (confirmed) {
                EpisodePosterGeneratorConfig.performReset();
            }
        });
        
        // Style the dialog buttons
        if (dialog) {
            setTimeout(() => {
                const confirmBtn = dialog.querySelector('.btnConfirm');
                const cancelBtn = dialog.querySelector('.btnCancel');
                
                if (confirmBtn) {
                    confirmBtn.textContent = 'Reset History';
                    confirmBtn.style.backgroundColor = '#d32f2f';
                    confirmBtn.style.color = 'white';
                }
                
                if (cancelBtn) {
                    cancelBtn.textContent = 'Cancel';
                }
            }, 100);
        }
    },

    // MARK: performReset
    performReset: function() {
        Dashboard.showLoadingMsg();
        
        fetch('/Plugins/EpisodePosterGenerator/ResetHistory', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Emby-Token': ApiClient.accessToken()
            }
        })
        .then(response => {
            if (response.ok) {
                return response.json();
            }
            throw new Error('Failed to reset history');
        })
        .then(data => {
            Dashboard.hideLoadingMsg();
            
            // Show success message
            const successHtml = `
                <div class="reset-success">
                    <h3>✓ Success</h3>
                    <p>Processing history has been cleared successfully.</p>
                    <p>Processed episodes: ${data.clearedCount || 0}</p>
                </div>
            `;
            
            Dashboard.alert(successHtml, 'History Reset Complete');
        })
        .catch(error => {
            Dashboard.hideLoadingMsg();
            console.error('Reset history error:', error);
            Dashboard.alert('Failed to reset processing history. Please check the server logs.', 'Error');
        });
    },

    // MARK: bindResetHistoryEvent
    bindResetHistoryEvent: function() {
        const resetBtn = document.getElementById('btnResetHistory');
        if (resetBtn) {
            resetBtn.addEventListener('click', function(e) {
                e.preventDefault();
                EpisodePosterGeneratorConfig.resetHistory();
            });
        }
    },

    // MARK: initializeColorControls
    initializeColorControls: function() {
        document.querySelectorAll('.color-picker').forEach(colorPicker => {
            const container = colorPicker.parentElement;
            const hexInput = container.querySelector('.hex-input');
            const alphaSlider = container.querySelector('.alpha-slider');
            const alphaLabel = container.querySelector('.alpha-label');
            
            // Set initial values from hex
            if (hexInput.value) {
                const parsed = EpisodePosterGeneratorConfig.parseARGBHex(hexInput.value);
                if (parsed) {
                    colorPicker.value = parsed.rgb;
                    alphaSlider.value = parsed.alpha;
                    alphaLabel.textContent = parsed.alpha;
                }
            }
            
            // Update hex when color picker changes
            colorPicker.addEventListener('input', function() {
                EpisodePosterGeneratorConfig.updateHexFromControls(container);
            });
            
            // Update hex when alpha slider changes
            alphaSlider.addEventListener('input', function() {
                alphaLabel.textContent = alphaSlider.value;
                EpisodePosterGeneratorConfig.updateHexFromControls(container);
            });
            
            // Update controls when hex input changes
            hexInput.addEventListener('input', function() {
                EpisodePosterGeneratorConfig.updateControlsFromHex(container);
            });
        });
    },

    // MARK: updateHexFromControls
    updateHexFromControls: function(container) {
        const colorPicker = container.querySelector('.color-picker');
        const alphaSlider = container.querySelector('.alpha-slider');
        const hexInput = container.querySelector('.hex-input');
        
        const rgb = colorPicker.value.substring(1); // Remove #
        const alpha = parseInt(alphaSlider.value);
        const alphaHex = alpha.toString(16).padStart(2, '0').toUpperCase();
        hexInput.value = '#' + alphaHex + rgb.toUpperCase();
        
        // Trigger change event to save config if needed
        hexInput.dispatchEvent(new Event('change'));
    },

    // MARK: updateControlsFromHex
    updateControlsFromHex: function(container) {
        const colorPicker = container.querySelector('.color-picker');
        const alphaSlider = container.querySelector('.alpha-slider');
        const alphaLabel = container.querySelector('.alpha-label');
        const hexInput = container.querySelector('.hex-input');
        
        const parsed = EpisodePosterGeneratorConfig.parseARGBHex(hexInput.value);
        if (parsed) {
            colorPicker.value = parsed.rgb;
            alphaSlider.value = parsed.alpha;
            alphaLabel.textContent = parsed.alpha;
        }
    },

    // MARK: parseARGBHex
    parseARGBHex: function(input) {
        if (!input) return null;
        
        // Remove # if present
        input = input.replace('#', '').toUpperCase();
        
        // Handle ARGB format (8 characters)
        if (input.length === 8) {
            const alpha = parseInt(input.substring(0, 2), 16);
            const rgb = '#' + input.substring(2);
            return { rgb: rgb, alpha: alpha };
        }
        
        // Handle RGB format (6 characters) - assume full opacity
        if (input.length === 6) {
            return { rgb: '#' + input, alpha: 255 };
        }
        
        // Handle short format (3 characters) - assume full opacity
        if (input.length === 3) {
            const expanded = input[0] + input[0] + input[1] + input[1] + input[2] + input[2];
            return { rgb: '#' + expanded, alpha: 255 };
        }
        
        return null;
    }
};

// Event Listeners
document.getElementById('EpisodePosterGeneratorConfigPage').addEventListener('pageshow', function () {
    EpisodePosterGeneratorConfig.loadConfig();
    EpisodePosterGeneratorConfig.bindResetHistoryEvent();
});

document.getElementById('EpisodePosterGeneratorConfigForm').addEventListener('submit', function (e) {
    e.preventDefault();
    EpisodePosterGeneratorConfig.saveConfig();
});

// Add listeners for visibility changes
document.getElementById('selectPosterStyle').addEventListener('change', function () {
    EpisodePosterGeneratorConfig.updateVisibility();
});

document.getElementById('chkShowTitle').addEventListener('change', function () {
    EpisodePosterGeneratorConfig.updateVisibility();
});

// Add listeners for letterbox detection dependencies
document.getElementById('chkExtractPoster').addEventListener('change', function () {
    EpisodePosterGeneratorConfig.updateVisibility();
});

document.getElementById('chkEnableLetterboxDetection').addEventListener('change', function () {
    EpisodePosterGeneratorConfig.updateVisibility();
});