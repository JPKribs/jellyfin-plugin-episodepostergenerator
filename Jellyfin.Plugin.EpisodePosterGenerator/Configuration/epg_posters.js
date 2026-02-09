export default function (view) {
    'use strict';

    var pluginId = 'b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e';
    var fullConfig = null;
    var currentConfigId = null;
    var allSeries = [];
    var _initialized = false;
    var _dirty = false;
    var _savedConfigSnapshot = null;
    var _seriesModalTrigger = null;

    function getTabs() {
        return [
            { href: 'configurationpage?name=epg_posters', name: 'Posters' },
            { href: 'configurationpage?name=epg_settings', name: 'Settings' }
        ];
    }

    // ===== Utilities =====

    function generateGuid() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0, v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function parseARGBHex(input) {
        if (!input) return null;
        input = input.replace('#', '').toUpperCase();

        if (input.length === 8) {
            return { rgb: '#' + input.substring(2), alpha: parseInt(input.substring(0, 2), 16) };
        }
        if (input.length === 6) {
            return { rgb: '#' + input, alpha: 255 };
        }
        if (input.length === 3) {
            var expanded = input[0] + input[0] + input[1] + input[1] + input[2] + input[2];
            return { rgb: '#' + expanded, alpha: 255 };
        }
        return null;
    }

    function trapFocus(modalContent, e) {
        if (e.key !== 'Tab') return;
        var focusable = modalContent.querySelectorAll(
            'button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
        );
        if (focusable.length === 0) return;
        var first = focusable[0];
        var last = focusable[focusable.length - 1];
        if (e.shiftKey && document.activeElement === first) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    }

    function debounce(fn, delay) {
        var timer = null;
        return function () {
            var context = this, args = arguments;
            clearTimeout(timer);
            timer = setTimeout(function () {
                fn.apply(context, args);
            }, delay);
        };
    }

    // ===== Unsaved Changes =====

    function takeConfigSnapshot() {
        _savedConfigSnapshot = JSON.stringify(fullConfig.PosterConfigurations);
    }

    function markDirty() {
        if (!_dirty) {
            _dirty = true;
            var indicator = view.querySelector('#unsavedIndicator');
            if (indicator) indicator.classList.add('visible');
        }
    }

    function markClean() {
        _dirty = false;
        var indicator = view.querySelector('#unsavedIndicator');
        if (indicator) indicator.classList.remove('visible');
        takeConfigSnapshot();
    }

    function flashSaveSuccess() {
        var indicator = view.querySelector('#unsavedIndicator');
        if (!indicator) return;

        indicator.innerHTML = '';
        var dot = document.createElement('span');
        dot.className = 'unsaved-indicator-dot';
        dot.style.background = 'var(--epg-success-text)';
        indicator.appendChild(dot);
        indicator.appendChild(document.createTextNode(' Saved!'));
        indicator.classList.add('visible', 'save-success');

        setTimeout(function () {
            indicator.classList.remove('visible', 'save-success');
            // Restore original content after fade
            setTimeout(function () {
                indicator.innerHTML = '<span class="unsaved-indicator-dot"></span> Unsaved changes';
            }, 300);
        }, 2000);
    }

    function checkDirty() {
        if (!fullConfig || !_savedConfigSnapshot) return;
        saveCurrentConfigSettings();
        var current = JSON.stringify(fullConfig.PosterConfigurations);
        if (current !== _savedConfigSnapshot) {
            markDirty();
        } else {
            _dirty = false;
            var indicator = view.querySelector('#unsavedIndicator');
            if (indicator) indicator.classList.remove('visible');
        }
    }

    // ===== Input Modal =====

    function showInputModal(title, fields, callback) {
        var triggerElement = document.activeElement;
        var modal = view.querySelector('#inputModal');
        var modalContent = view.querySelector('.input-modal-content');
        var titleEl = view.querySelector('#inputModalTitle');
        var fieldsContainer = view.querySelector('#inputModalFields');
        var btnConfirm = view.querySelector('#btnConfirmInputModal');
        var btnCancel = view.querySelector('#btnCancelInputModal');
        var btnClose = view.querySelector('#btnCloseInputModal');

        titleEl.textContent = title;
        fieldsContainer.innerHTML = '';

        fields.forEach(function (field, index) {
            var container = document.createElement('div');
            container.className = 'inputContainer';

            var label = document.createElement('label');
            label.setAttribute('for', 'inputModalField_' + index);
            label.textContent = field.label;
            container.appendChild(label);

            var input = document.createElement('input');
            input.setAttribute('is', 'emby-input');
            input.type = field.type || 'text';
            input.id = 'inputModalField_' + index;
            input.value = field.value || '';
            if (field.placeholder) input.placeholder = field.placeholder;
            if (field.required) input.required = true;
            container.appendChild(input);

            if (field.description) {
                var desc = document.createElement('div');
                desc.className = 'fieldDescription';
                desc.textContent = field.description;
                container.appendChild(desc);
            }

            fieldsContainer.appendChild(container);
        });

        modal.style.display = 'flex';

        var firstInput = fieldsContainer.querySelector('input');
        if (firstInput) {
            setTimeout(function () { firstInput.focus(); firstInput.select(); }, 100);
        }

        function cleanup() {
            btnConfirm.removeEventListener('click', onConfirm);
            btnCancel.removeEventListener('click', onCancel);
            btnClose.removeEventListener('click', onCancel);
            modal.removeEventListener('click', onBackdrop);
            document.removeEventListener('keydown', onKeydown);
            modal.style.display = 'none';
            if (triggerElement && triggerElement.focus) triggerElement.focus();
        }

        function onConfirm() {
            var values = [];
            fieldsContainer.querySelectorAll('input').forEach(function (input) {
                values.push(input.value);
            });
            cleanup();
            callback(values);
        }

        function onCancel() {
            cleanup();
            callback(null);
        }

        function onBackdrop(e) {
            if (e.target === modal) onCancel();
        }

        function onKeydown(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                onConfirm();
            } else if (e.key === 'Escape') {
                e.preventDefault();
                onCancel();
            } else {
                trapFocus(modalContent, e);
            }
        }

        btnConfirm.addEventListener('click', onConfirm);
        btnCancel.addEventListener('click', onCancel);
        btnClose.addEventListener('click', onCancel);
        modal.addEventListener('click', onBackdrop);
        document.addEventListener('keydown', onKeydown);
    }

    // ===== Collapsibles =====

    function initCollapsibles() {
        view.querySelectorAll('.collapsibleHeader').forEach(function (header) {
            header.addEventListener('click', function () {
                var targetId = this.dataset.target;
                var content = view.querySelector('#' + targetId);
                if (content) {
                    this.classList.toggle('collapsed');
                    content.classList.toggle('collapsed');
                    var isExpanded = !this.classList.contains('collapsed');
                    this.setAttribute('aria-expanded', String(isExpanded));
                }
            });
        });
    }

    // ===== Color Controls =====

    function updateHexFromControls(container) {
        var colorPicker = container.querySelector('.color-picker');
        var alphaSlider = container.querySelector('.alpha-slider');
        var hexInput = container.querySelector('.hex-input');

        var rgb = colorPicker.value.substring(1);
        var alpha = parseInt(alphaSlider.value);
        var alphaHex = alpha.toString(16).padStart(2, '0').toUpperCase();
        hexInput.value = '#' + alphaHex + rgb.toUpperCase();
    }

    function updateControlsFromHex(container) {
        var colorPicker = container.querySelector('.color-picker');
        var alphaSlider = container.querySelector('.alpha-slider');
        var alphaLabel = container.querySelector('.alpha-label');
        var hexInput = container.querySelector('.hex-input');

        var parsed = parseARGBHex(hexInput.value);
        if (parsed) {
            colorPicker.value = parsed.rgb;
            alphaSlider.value = parsed.alpha;
            alphaLabel.textContent = parsed.alpha;
        }
    }

    function bindColorControls() {
        view.querySelectorAll('.color-picker').forEach(function (colorPicker) {
            var container = colorPicker.parentElement;
            var alphaSlider = container.querySelector('.alpha-slider');
            var alphaLabel = container.querySelector('.alpha-label');
            var hexInput = container.querySelector('.hex-input');

            colorPicker.addEventListener('input', function () {
                updateHexFromControls(container);
                checkDirty();
            });
            alphaSlider.addEventListener('input', function () {
                alphaLabel.textContent = alphaSlider.value;
                updateHexFromControls(container);
                checkDirty();
            });
            hexInput.addEventListener('input', function () {
                updateControlsFromHex(container);
                checkDirty();
            });
        });
    }

    function syncColorControls() {
        view.querySelectorAll('.color-picker').forEach(function (colorPicker) {
            var container = colorPicker.parentElement;
            var hexInput = container.querySelector('.hex-input');
            var alphaSlider = container.querySelector('.alpha-slider');
            var alphaLabel = container.querySelector('.alpha-label');

            if (hexInput.value) {
                var parsed = parseARGBHex(hexInput.value);
                if (parsed) {
                    colorPicker.value = parsed.rgb;
                    alphaSlider.value = parsed.alpha;
                    alphaLabel.textContent = parsed.alpha;
                }
            }
        });
    }

    // ===== Config Loading =====

    function loadConfig() {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            fullConfig = config;

            if (!config.PosterConfigurations || config.PosterConfigurations.length === 0) {
                config.PosterConfigurations = [{
                    Id: generateGuid(),
                    Settings: {},
                    SeriesIds: []
                }];
            }

            populateConfigDropdown();

            loadAllSeries().then(function () {
                syncColorControls();
                takeConfigSnapshot();
                markClean();
                Dashboard.hideLoadingMsg();
            });
        }).catch(function (error) {
            console.error('Failed to load config:', error);
            Dashboard.hideLoadingMsg();
        });
    }

    function populateConfigDropdown() {
        var select = view.querySelector('#selectPosterConfig');
        var previousId = currentConfigId;
        select.innerHTML = '';

        var configs = fullConfig.PosterConfigurations.slice().sort(function (a, b) {
            if (a.IsDefault && !b.IsDefault) return -1;
            if (!a.IsDefault && b.IsDefault) return 1;
            return (a.Name || '').localeCompare(b.Name || '');
        });

        configs.forEach(function (config) {
            var option = document.createElement('option');
            option.value = config.Id;
            option.textContent = config.Name || 'Unnamed Configuration';
            select.appendChild(option);
        });

        if (previousId && configs.some(function (c) { return c.Id === previousId; })) {
            currentConfigId = previousId;
        } else {
            currentConfigId = configs[0].Id;
        }

        select.value = currentConfigId;
        loadCurrentConfig();
    }

    function loadCurrentConfig() {
        var config = getCurrentConfig();
        if (!config) return;

        var settings = config.Settings || {};

        view.querySelectorAll('[data-setting]').forEach(function (element) {
            var settingKey = element.getAttribute('data-setting');
            var settingValue = settings[settingKey];

            if (element.type === 'checkbox') {
                element.checked = settingValue !== false;
            } else if (element.getAttribute('data-type') === 'number') {
                element.value = settingValue || 0;
            } else {
                element.value = settingValue || '';
            }
        });

        updateSeriesAssignment();
        updateVisibility();
        updateStyleDescription();
        syncColorControls();
    }

    function getCurrentConfig() {
        return fullConfig.PosterConfigurations.find(function (c) { return c.Id === currentConfigId; });
    }

    // ===== Series Management =====

    function updateSeriesAssignment() {
        var config = getCurrentConfig();
        var isDefault = config.IsDefault;

        view.querySelector('#seriesAssignmentSection').style.display = isDefault ? 'none' : 'block';
        view.querySelector('#btnDeleteConfig').style.display = isDefault ? 'none' : 'inline-block';
        view.querySelector('#btnRenameConfig').style.display = isDefault ? 'none' : 'inline-block';

        if (!isDefault) {
            renderAssignedSeries();
        }
    }

    function renderAssignedSeries() {
        var config = getCurrentConfig();
        var container = view.querySelector('#assignedSeriesList');
        container.innerHTML = '';

        if (!config.SeriesIds || config.SeriesIds.length === 0) {
            var emptyMsg = document.createElement('div');
            emptyMsg.className = 'series-empty-state';
            emptyMsg.innerHTML = '<span class="series-empty-state-icon">&#9888;</span> No series assigned — assign at least one series before saving.';
            container.appendChild(emptyMsg);
            return;
        }

        config.SeriesIds.forEach(function (seriesId) {
            var series = allSeries.find(function (s) { return s.Id === seriesId; });

            if (!series) {
                console.warn('Series not found for ID:', seriesId);
                return;
            }

            var tag = document.createElement('div');
            tag.className = 'series-tag';

            var posterImg = document.createElement('img');
            posterImg.className = 'series-tag-poster';
            posterImg.src = ApiClient.getImageUrl(series.Id, {
                type: 'Primary',
                maxWidth: 64,
                quality: 90
            });
            posterImg.onerror = function () {
                this.style.display = 'none';
            };

            var nameSpan = document.createElement('span');
            nameSpan.className = 'series-tag-name';
            nameSpan.textContent = series.Name;

            var removeSpan = document.createElement('span');
            removeSpan.className = 'series-tag-remove';
            removeSpan.textContent = '\u00d7';
            removeSpan.setAttribute('data-series-id', seriesId);

            tag.appendChild(posterImg);
            tag.appendChild(nameSpan);
            tag.appendChild(removeSpan);
            container.appendChild(tag);
        });

        container.querySelectorAll('.series-tag-remove').forEach(function (btn) {
            btn.addEventListener('click', function () {
                removeSeries(this.getAttribute('data-series-id'));
            });
        });
    }

    function loadAllSeries() {
        console.log('Loading all series from Jellyfin...');
        return ApiClient.getItems(ApiClient.getCurrentUserId(), {
            IncludeItemTypes: 'Series',
            Recursive: true,
            SortBy: 'SortName',
            SortOrder: 'Ascending',
            Fields: 'Overview,ProductionYear'
        }).then(function (result) {
            allSeries = result.Items || [];
            console.log('Loaded series count:', allSeries.length);
            return allSeries;
        }).catch(function (error) {
            console.error('Failed to load series:', error);
            allSeries = [];
            return [];
        });
    }

    function showSeriesSelectionModal() {
        _seriesModalTrigger = document.activeElement;
        var modal = view.querySelector('#seriesSelectionModal');
        var listContainer = view.querySelector('#seriesCheckboxList');
        var summaryEl = view.querySelector('#seriesSelectionSummary');

        if (!allSeries || allSeries.length === 0) {
            // Show modal with loading state
            listContainer.innerHTML = '<div class="series-modal-loading"><div class="series-modal-spinner"></div><span>Loading series...</span></div>';
            summaryEl.textContent = '';
            modal.style.display = 'flex';
            document.addEventListener('keydown', _onSeriesModalKeydown);

            loadAllSeries().then(function () {
                if (!allSeries || allSeries.length === 0) {
                    closeSeriesSelectionModal();
                    Dashboard.alert('No series found. Make sure you have TV series in your Jellyfin library.');
                    return;
                }
                populateSeriesModal();
            });
            return;
        }

        modal.style.display = 'flex';
        document.addEventListener('keydown', _onSeriesModalKeydown);
        populateSeriesModal();
    }

    function populateSeriesModal() {
        var listContainer = view.querySelector('#seriesCheckboxList');
        var summaryEl = view.querySelector('#seriesSelectionSummary');
        var config = getCurrentConfig();
        var currentSeriesIds = config.SeriesIds || [];
        var assignedSeriesIds = getAllAssignedSeriesIds();

        listContainer.innerHTML = '';

        var selectedCount = currentSeriesIds.length;
        var availableCount = 0;

        allSeries.forEach(function (series) {
            var isAssignedHere = currentSeriesIds.includes(series.Id);
            var isAssignedElsewhere = !isAssignedHere && assignedSeriesIds.includes(series.Id);
            if (!isAssignedElsewhere) availableCount++;

            var item = document.createElement('label');
            item.className = 'series-checkbox-item' + (isAssignedElsewhere ? ' disabled' : '');

            var checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.className = 'series-checkbox';
            checkbox.value = series.Id;
            if (isAssignedHere) checkbox.checked = true;
            if (isAssignedElsewhere) checkbox.disabled = true;

            var posterImg = document.createElement('img');
            posterImg.className = 'series-item-poster';
            posterImg.src = ApiClient.getImageUrl(series.Id, {
                type: 'Primary',
                maxWidth: 80,
                quality: 80
            });
            posterImg.onerror = function () {
                this.style.visibility = 'hidden';
            };

            var info = document.createElement('div');
            info.className = 'series-item-info';

            var nameSpan = document.createElement('span');
            nameSpan.className = 'series-item-name';
            nameSpan.textContent = series.Name;
            info.appendChild(nameSpan);

            var year = series.ProductionYear;
            if (year) {
                var yearSpan = document.createElement('span');
                yearSpan.className = 'series-item-year';
                yearSpan.textContent = year;
                info.appendChild(yearSpan);
            }

            var overview = series.Overview;
            if (overview) {
                var descSpan = document.createElement('span');
                descSpan.className = 'series-item-overview';
                descSpan.textContent = overview.length > 120 ? overview.substring(0, 120) + '...' : overview;
                info.appendChild(descSpan);
            }

            if (isAssignedElsewhere) {
                var badge = document.createElement('span');
                badge.className = 'series-item-badge';
                badge.textContent = 'Assigned elsewhere';
                info.appendChild(badge);
            }

            item.appendChild(checkbox);
            item.appendChild(posterImg);
            item.appendChild(info);
            listContainer.appendChild(item);

            checkbox.addEventListener('change', function () {
                updateSelectionSummary();
            });
        });

        function updateSelectionSummary() {
            var checked = listContainer.querySelectorAll('.series-checkbox:checked').length;
            summaryEl.textContent = checked + ' of ' + availableCount + ' available series selected';
        }

        updateSelectionSummary();

        var searchInput = view.querySelector('#seriesSearchInput');
        searchInput.value = '';
        searchInput.focus();
    }

    function filterSeriesList() {
        var searchInput = view.querySelector('#seriesSearchInput');
        var listContainer = view.querySelector('#seriesCheckboxList');
        var searchTerm = searchInput.value.toLowerCase();

        listContainer.querySelectorAll('.series-checkbox-item').forEach(function (item) {
            var nameEl = item.querySelector('.series-item-name');
            var text = nameEl ? nameEl.textContent.toLowerCase() : item.textContent.toLowerCase();
            item.style.display = text.includes(searchTerm) ? '' : 'none';
        });
    }

    var debouncedFilterSeriesList = debounce(filterSeriesList, 200);

    function getAllAssignedSeriesIds() {
        var allIds = [];
        fullConfig.PosterConfigurations.forEach(function (config) {
            if (config.SeriesIds) {
                allIds.push.apply(allIds, config.SeriesIds);
            }
        });
        return allIds;
    }

    function confirmSeriesSelection() {
        var config = getCurrentConfig();
        var selectedIds = [];

        view.querySelectorAll('.series-checkbox:checked').forEach(function (checkbox) {
            selectedIds.push(checkbox.value);
        });

        config.SeriesIds = selectedIds;
        renderAssignedSeries();
        closeSeriesSelectionModal();
        checkDirty();
    }

    function _onSeriesModalKeydown(e) {
        if (e.key === 'Escape') {
            e.preventDefault();
            closeSeriesSelectionModal();
        } else {
            var modalContent = view.querySelector('.series-modal-content');
            trapFocus(modalContent, e);
        }
    }

    function closeSeriesSelectionModal() {
        view.querySelector('#seriesSelectionModal').style.display = 'none';
        document.removeEventListener('keydown', _onSeriesModalKeydown);
        if (_seriesModalTrigger && _seriesModalTrigger.focus) _seriesModalTrigger.focus();
        _seriesModalTrigger = null;
    }

    function removeSeries(seriesId) {
        var config = getCurrentConfig();
        config.SeriesIds = config.SeriesIds.filter(function (id) { return id !== seriesId; });
        renderAssignedSeries();
        checkDirty();
    }

    // ===== Config CRUD =====

    function createNewConfig() {
        showInputModal('New Configuration', [
            { label: 'Name', placeholder: 'My Poster Config', required: true }
        ], function (values) {
            if (!values) return;
            var name = values[0];

            if (!name || name.trim() === '') {
                Dashboard.alert('Configuration name is required.');
                return;
            }

            if (name.trim().toLowerCase() === 'default') {
                Dashboard.alert('The name "Default" is reserved and cannot be used.');
                return;
            }

            var exists = fullConfig.PosterConfigurations.some(function (c) {
                return c.Name && c.Name.toLowerCase() === name.trim().toLowerCase();
            });

            if (exists) {
                Dashboard.alert('A configuration with this name already exists.');
                return;
            }

            var newConfig = {
                Id: generateGuid(),
                Name: name.trim(),
                Settings: {
                    ExtractPoster: true,
                    EnableHWA: false,
                    EnableLetterboxDetection: true,
                    LetterboxBlackThreshold: 25,
                    LetterboxConfidence: 85.0,
                    ExtractWindowStart: 20.0,
                    ExtractWindowEnd: 80.0,
                    PosterStyle: 'Standard',
                    CutoutType: 'Code',
                    CutoutBorder: true,
                    LogoPosition: 'Center',
                    LogoAlignment: 'Center',
                    LogoHeight: 30.0,
                    BrightenHDR: 25.0,
                    PosterFill: 'Original',
                    PosterDimensionRatio: '16:9',
                    PosterFileType: 'WEBP',
                    PosterSafeArea: 5.0,
                    ShowEpisode: true,
                    EpisodeFontFamily: 'Arial',
                    EpisodeUseCustomFont: false,
                    EpisodeFontPath: '',
                    EpisodeFontStyle: 'Bold',
                    EpisodeFontSize: 7.0,
                    EpisodeFontColor: '#FFFFFFFF',
                    ShowTitle: true,
                    TitleFontFamily: 'Arial',
                    TitleUseCustomFont: false,
                    TitleFontPath: '',
                    TitleFontStyle: 'Bold',
                    TitleFontSize: 10.0,
                    TitleFontColor: '#FFFFFFFF',
                    OverlayColor: '#66000000',
                    OverlayGradient: 'None',
                    OverlaySecondaryColor: '#66000000',
                    GraphicPath: '',
                    GraphicWidth: 25.0,
                    GraphicHeight: 25.0,
                    GraphicPosition: 'Center',
                    GraphicAlignment: 'Center'
                },
                SeriesIds: []
            };

            fullConfig.PosterConfigurations.push(newConfig);
            currentConfigId = newConfig.Id;
            populateConfigDropdown();
            markDirty();
        });
    }

    function renameCurrentConfig() {
        var config = getCurrentConfig();

        if (config.IsDefault) {
            Dashboard.alert('The default configuration cannot be renamed.');
            return;
        }

        var currentName = config.Name || 'Unnamed';

        showInputModal('Rename Configuration', [
            { label: 'Name', value: currentName, required: true }
        ], function (values) {
            if (!values) return;
            var newName = values[0];

            if (!newName || newName.trim() === '') {
                return;
            }

            if (newName.trim().toLowerCase() === 'default') {
                Dashboard.alert('The name "Default" is reserved and cannot be used.');
                return;
            }

            var exists = fullConfig.PosterConfigurations.some(function (c) {
                return c.Id !== config.Id && c.Name && c.Name.toLowerCase() === newName.trim().toLowerCase();
            });

            if (exists) {
                Dashboard.alert('A configuration with this name already exists.');
                return;
            }

            config.Name = newName.trim();
            populateConfigDropdown();
            markDirty();
        });
    }

    function deleteCurrentConfig() {
        var config = getCurrentConfig();

        if (config.IsDefault) {
            Dashboard.alert('Cannot delete the default configuration.');
            return;
        }

        Dashboard.confirm('Are you sure you want to delete this poster configuration?', 'Delete Configuration', function (confirmed) {
            if (confirmed) {
                fullConfig.PosterConfigurations = fullConfig.PosterConfigurations.filter(function (c) {
                    return c.Id !== currentConfigId;
                });

                currentConfigId = fullConfig.PosterConfigurations[0].Id;
                populateConfigDropdown();
                markDirty();
            }
        });
    }

    function importCurrentConfig() {
        var fileInput = view.querySelector('#templateFileInput');

        fileInput.onchange = function (e) {
            var file = e.target.files[0];
            if (!file) return;

            var reader = new FileReader();
            reader.onload = function (event) {
                try {
                    var template = JSON.parse(event.target.result);

                    if (!template.settings) {
                        Dashboard.alert('Invalid template file: missing settings.');
                        return;
                    }

                    var configName = template.name || 'Imported Configuration';

                    showInputModal('Import Configuration', [
                        { label: 'Name', value: configName, required: true, description: 'Enter a name for the imported configuration.' }
                    ], function (values) {
                        if (!values) return;
                        var nameInput = values[0];

                        if (!nameInput || nameInput.trim() === '') {
                            return;
                        }

                        if (nameInput.trim().toLowerCase() === 'default') {
                            Dashboard.alert('The name "Default" is reserved and cannot be used.');
                            return;
                        }

                        var exists = fullConfig.PosterConfigurations.some(function (c) {
                            return c.Name && c.Name.toLowerCase() === nameInput.trim().toLowerCase();
                        });

                        if (exists) {
                            Dashboard.alert('A configuration with this name already exists.');
                            return;
                        }

                        var newConfig = {
                            Id: generateGuid(),
                            Name: nameInput.trim(),
                            Settings: template.settings,
                            SeriesIds: [],
                            IsDefault: false
                        };

                        fullConfig.PosterConfigurations.push(newConfig);
                        currentConfigId = newConfig.Id;
                        populateConfigDropdown();
                        markDirty();

                        Dashboard.alert('Template imported successfully! Version: ' + (template.version || 'unknown') +
                            (template.author ? '\nAuthor: ' + template.author : '') +
                            (template.description ? '\nDescription: ' + template.description : ''));
                    });

                } catch (error) {
                    console.error('Import error:', error);
                    Dashboard.alert('Failed to import template. Please ensure the file is a valid JSON template.');
                }
            };

            reader.readAsText(file);
            fileInput.value = '';
        };

        fileInput.click();
    }

    function exportCurrentConfig() {
        var config = getCurrentConfig();

        if (!config) {
            Dashboard.alert('No configuration selected.');
            return;
        }

        showInputModal('Export Configuration', [
            { label: 'Author', placeholder: 'Your name', required: true },
            { label: 'Description', placeholder: 'Brief description of this config', required: true }
        ], function (values) {
            if (!values) return;
            var author = values[0];
            var description = values[1];

            if (!author || !author.trim()) {
                Dashboard.alert('Author name is required.');
                return;
            }

            if (!description || !description.trim()) {
                Dashboard.alert('Description is required.');
                return;
            }

            Dashboard.showLoadingMsg();

            ApiClient.getPluginConfiguration(pluginId).then(function (pluginConfig) {
                var pluginVersion = pluginConfig.Version;

                var template = {
                    name: config.Name,
                    description: description.trim(),
                    author: author.trim(),
                    version: pluginVersion,
                    createdDate: new Date().toISOString(),
                    settings: config.Settings
                };

                var json = JSON.stringify(template, null, 2);
                var blob = new Blob([json], { type: 'application/json' });
                var url = URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = url;
                var cleanName = config.Name.replace(/[^a-z0-9\s]/gi, '').replace(/\s+/g, '_');
                a.download = cleanName + '_v' + pluginVersion + '.json';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);

                Dashboard.hideLoadingMsg();
                Dashboard.alert('Template exported successfully!');
            }).catch(function (error) {
                console.error('Failed to get plugin version:', error);
                Dashboard.hideLoadingMsg();
                Dashboard.alert('Export failed. Please try again.');
            });
        });
    }

    // ===== Save =====

    function saveCurrentConfigSettings() {
        var config = getCurrentConfig();
        if (!config) return;
        if (!config.Settings) {
            config.Settings = {};
        }

        view.querySelectorAll('[data-setting]').forEach(function (element) {
            var settingKey = element.getAttribute('data-setting');

            if (element.type === 'checkbox') {
                config.Settings[settingKey] = element.checked;
            } else if (element.getAttribute('data-type') === 'number') {
                config.Settings[settingKey] = parseFloat(element.value) || 0;
            } else {
                config.Settings[settingKey] = element.value;
            }
        });
    }

    function validateNumberInputs() {
        var errors = [];
        view.querySelectorAll('[data-setting][data-type="number"]').forEach(function (input) {
            // Skip inputs that are hidden (inside collapsed/invisible sections)
            if (input.offsetParent === null) return;

            var value = parseFloat(input.value);
            var min = parseFloat(input.getAttribute('min'));
            var max = parseFloat(input.getAttribute('max'));
            var labelEl = input.closest('.inputContainer');
            var labelText = labelEl ? labelEl.querySelector('label') : null;
            var name = labelText ? labelText.textContent.replace(':', '').trim() : input.id;

            if (isNaN(value)) {
                errors.push(name + ' must be a valid number.');
            } else {
                if (!isNaN(min) && value < min) {
                    errors.push(name + ' must be at least ' + min + '.');
                }
                if (!isNaN(max) && value > max) {
                    errors.push(name + ' must be at most ' + max + '.');
                }
            }
        });
        return errors;
    }

    function saveConfig() {
        saveCurrentConfigSettings();

        var validationErrors = validateNumberInputs();
        if (validationErrors.length > 0) {
            Dashboard.alert(validationErrors.join('\n'));
            return;
        }

        var invalidConfigs = fullConfig.PosterConfigurations.filter(function (config) {
            var hasNoSeries = !config.SeriesIds || config.SeriesIds.length === 0;
            var isNotDefault = config.IsDefault !== true;
            return isNotDefault && hasNoSeries;
        });

        if (invalidConfigs.length > 0) {
            var configNames = invalidConfigs.map(function (c) { return c.Name || 'Unnamed'; }).join(', ');
            Dashboard.alert('Cannot save: The following non-default configurations have no series assigned: ' + configNames + '. Please assign series or delete these configurations.');
            return;
        }

        Dashboard.showLoadingMsg();
        ApiClient.updatePluginConfiguration(pluginId, fullConfig).then(function (result) {
            markClean();
            flashSaveSuccess();
            Dashboard.processPluginConfigurationUpdateResult(result);
        });
    }

    // ===== Style Descriptions =====

    var posterStyleDescriptions = {
        Standard: 'Full-frame episode image with text overlaid at the bottom. Clean and versatile — works well for most libraries.',
        Brush: 'Artistic brush-stroke mask effect with episode info and title. Creates a painted, editorial look.',
        Cutout: 'Large episode number or code cut out of the image. Bold typographic style, great for minimal designs.',
        Frame: 'Episode image framed within a border with metadata outside. Gives a polished, gallery-like appearance.',
        Logo: 'Series logo overlaid on the episode image. Ideal when you want branding-forward posters.',
        Numeral: 'Prominent episode numeral as the focal element. Minimal and distinctive, emphasizes episode numbering.',
        Split: 'Image split into sections with text in between. Creates a dynamic, magazine-style layout.'
    };

    function updateStyleDescription() {
        var style = view.querySelector('#selectPosterStyle').value;
        var descEl = view.querySelector('#posterStyleDescription');
        if (descEl) {
            descEl.textContent = posterStyleDescriptions[style] || 'Choose the poster generation style and layout.';
        }
    }

    // ===== Visibility =====

    function updateVisibility() {
        var posterStyle = view.querySelector('#selectPosterStyle').value;

        view.querySelectorAll('[data-poster-styles]').forEach(function (element) {
            var supportedStyles = element.getAttribute('data-poster-styles').split(',');
            var isVisible = supportedStyles.includes(posterStyle);
            element.style.display = isVisible ? 'block' : 'none';
        });

        var posterFill = view.querySelector('#selectPosterFill').value;
        view.querySelectorAll('[data-hide-for-posterfill]').forEach(function (element) {
            var hiddenFills = element.getAttribute('data-hide-for-posterfill').split(',');
            var shouldHide = hiddenFills.includes(posterFill);
            element.style.display = shouldHide ? 'none' : 'block';
        });

        view.querySelectorAll('[data-hide-for-styles]').forEach(function (element) {
            var hiddenStyles = element.getAttribute('data-hide-for-styles').split(',');
            var shouldHide = hiddenStyles.includes(posterStyle);
            element.style.display = shouldHide ? 'none' : 'block';
        });

        view.querySelectorAll('[data-depends-on]').forEach(function (element) {
            var dependencies = element.getAttribute('data-depends-on').split(',');
            var allDependenciesMet = true;

            dependencies.forEach(function (dependencyId) {
                var dependencyElement = view.querySelector('#' + dependencyId.trim());
                if (!dependencyElement || !dependencyElement.checked) {
                    allDependenciesMet = false;
                }
            });

            var hideForStyles = element.getAttribute('data-hide-for-styles');
            var shouldHideForStyle = false;
            if (hideForStyles) {
                var hiddenStyles = hideForStyles.split(',');
                shouldHideForStyle = hiddenStyles.includes(posterStyle);
            }

            element.style.display = (allDependenciesMet && !shouldHideForStyle) ? 'block' : 'none';
        });

        view.querySelectorAll('[data-depends-on-gradient]').forEach(function (element) {
            var gradientSelectId = element.getAttribute('data-depends-on-gradient');
            var gradientSelect = view.querySelector('#' + gradientSelectId);
            var isVisible = gradientSelect && gradientSelect.value !== 'None';
            element.style.display = isVisible ? 'block' : 'none';
        });

        view.querySelectorAll('[data-depends-on-value]').forEach(function (element) {
            var inputId = element.getAttribute('data-depends-on-value');
            var input = view.querySelector('#' + inputId);
            var hasValue = input && input.value && input.value.trim() !== '';
            element.style.display = hasValue ? 'block' : 'none';
        });

        view.querySelectorAll('[data-hide-when-checked]').forEach(function (element) {
            var checkboxId = element.getAttribute('data-hide-when-checked');
            var checkbox = view.querySelector('#' + checkboxId);
            var isChecked = checkbox && checkbox.checked;
            element.style.display = isChecked ? 'none' : 'block';
        });

        var showEpisodeCheckbox = view.querySelector('#chkShowEpisode');
        if (posterStyle === 'Cutout' || posterStyle === 'Numeral' || posterStyle === 'Brush') {
            showEpisodeCheckbox.checked = true;
        }

        var showTitleCheckbox = view.querySelector('#chkShowTitle');
        if (posterStyle === 'Frame' || posterStyle === 'Brush') {
            showTitleCheckbox.checked = true;
        }
    }

    // ===== Event Binding =====

    function bindEventListeners() {
        view.querySelector('#EpgPostersForm').addEventListener('submit', function (e) {
            e.preventDefault();
            saveConfig();
        });

        view.querySelector('#selectPosterConfig').addEventListener('change', function () {
            saveCurrentConfigSettings();
            currentConfigId = this.value;
            loadCurrentConfig();
            view.querySelector('.content-primary').scrollIntoView({ behavior: 'smooth' });
        });

        view.querySelector('#btnNewConfig').addEventListener('click', function () {
            createNewConfig();
        });

        view.querySelector('#btnDeleteConfig').addEventListener('click', function () {
            deleteCurrentConfig();
        });

        view.querySelector('#btnRenameConfig').addEventListener('click', function () {
            renameCurrentConfig();
        });

        view.querySelector('#btnExportConfig').addEventListener('click', function () {
            exportCurrentConfig();
        });

        view.querySelector('#btnImportConfig').addEventListener('click', function () {
            importCurrentConfig();
        });

        view.querySelector('#btnAddSeries').addEventListener('click', function () {
            showSeriesSelectionModal();
        });

        view.querySelector('#btnCancelSeriesSelection').addEventListener('click', function () {
            closeSeriesSelectionModal();
        });

        view.querySelector('#btnCloseSeriesModal').addEventListener('click', function () {
            closeSeriesSelectionModal();
        });

        view.querySelector('#seriesSelectionModal').addEventListener('click', function (e) {
            if (e.target === this) {
                closeSeriesSelectionModal();
            }
        });

        view.querySelector('#btnConfirmSeriesSelection').addEventListener('click', function () {
            confirmSeriesSelection();
        });

        view.querySelector('#seriesSearchInput').addEventListener('input', debouncedFilterSeriesList);

        view.querySelector('#selectPosterStyle').addEventListener('change', function () {
            updateVisibility();
            updateStyleDescription();
            checkDirty();
        });

        view.querySelector('#chkShowTitle').addEventListener('change', function () {
            updateVisibility();
            checkDirty();
        });

        view.querySelector('#chkShowEpisode').addEventListener('change', function () {
            updateVisibility();
            checkDirty();
        });

        view.querySelector('#chkExtractPoster').addEventListener('change', function () {
            updateVisibility();
            checkDirty();
        });

        view.querySelector('#chkEnableLetterboxDetection').addEventListener('change', function () {
            updateVisibility();
            checkDirty();
        });

        view.querySelector('#selectPosterFill').addEventListener('change', function () {
            updateVisibility();
            checkDirty();
        });

        view.querySelector('#selectOverlayGradient').addEventListener('change', function () {
            updateVisibility();
            checkDirty();
        });

        view.querySelector('#txtGraphicPath').addEventListener('input', function () {
            updateVisibility();
            checkDirty();
        });

        view.querySelector('#chkEpisodeUseCustomFont').addEventListener('change', function () {
            updateVisibility();
            checkDirty();
        });

        view.querySelector('#chkTitleUseCustomFont').addEventListener('change', function () {
            updateVisibility();
            checkDirty();
        });

        // Track changes on all settings inputs
        view.querySelectorAll('[data-setting]').forEach(function (element) {
            var eventType = (element.type === 'checkbox' || element.tagName === 'SELECT') ? 'change' : 'input';
            element.addEventListener(eventType, function () {
                checkDirty();
            });
        });
    }

    // ===== Lifecycle =====

    function onBeforeUnload(e) {
        if (_dirty) {
            e.preventDefault();
            e.returnValue = '';
        }
    }

    view.addEventListener('viewshow', function () {
        LibraryMenu.setTabs('epg', 0, getTabs);

        if (!_initialized) {
            _initialized = true;
            initCollapsibles();
            bindEventListeners();
            bindColorControls();
        }

        window.addEventListener('beforeunload', onBeforeUnload);
        loadConfig();
    });

    view.addEventListener('viewbeforehide', function (e) {
        window.removeEventListener('beforeunload', onBeforeUnload);

        if (_dirty) {
            var confirmed = confirm('You have unsaved changes. Are you sure you want to leave?');
            if (!confirmed) {
                e.preventDefault();
                // Re-select the Posters tab since we're staying
                LibraryMenu.setTabs('epg', 0, getTabs);
            }
        }
    });
}
