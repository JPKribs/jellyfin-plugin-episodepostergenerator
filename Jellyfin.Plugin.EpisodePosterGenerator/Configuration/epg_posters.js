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

    // ── Utilities ────────────────────────────────────────────

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

    function trapFocus(container, e) {
        if (e.key !== 'Tab') return;
        var focusable = container.querySelectorAll(
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
            var ctx = this, args = arguments;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(ctx, args); }, delay);
        };
    }

    // ── Unsaved Changes ─────────────────────────────────────

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

    // ── Input Modal ─────────────────────────────────────────

    function showInputModal(title, fields, callback) {
        var triggerElement = document.activeElement;
        var modal = view.querySelector('#inputModal');
        var modalContent = view.querySelector('.input-modal-content');
        var fieldsContainer = view.querySelector('#inputModalFields');
        var btnConfirm = view.querySelector('#btnConfirmInputModal');
        var btnCancel = view.querySelector('#btnCancelInputModal');
        var btnClose = view.querySelector('#btnCloseInputModal');

        view.querySelector('#inputModalTitle').textContent = title;
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

        function onCancel() { cleanup(); callback(null); }

        function onBackdrop(e) { if (e.target === modal) onCancel(); }

        function onKeydown(e) {
            if (e.key === 'Enter') { e.preventDefault(); onConfirm(); }
            else if (e.key === 'Escape') { e.preventDefault(); onCancel(); }
            else { trapFocus(modalContent, e); }
        }

        btnConfirm.addEventListener('click', onConfirm);
        btnCancel.addEventListener('click', onCancel);
        btnClose.addEventListener('click', onCancel);
        modal.addEventListener('click', onBackdrop);
        document.addEventListener('keydown', onKeydown);
    }

    // ── Collapsibles ────────────────────────────────────────

    function initCollapsibles() {
        view.querySelectorAll('.collapsibleHeader').forEach(function (header) {
            header.addEventListener('click', function () {
                var content = view.querySelector('#' + this.dataset.target);
                if (content) {
                    this.classList.toggle('collapsed');
                    content.classList.toggle('collapsed');
                    this.setAttribute('aria-expanded', String(!this.classList.contains('collapsed')));
                }
            });
        });
    }

    // ── Color Controls ──────────────────────────────────────

    function updateHexFromControls(container) {
        var rgb = container.querySelector('.color-picker').value.substring(1);
        var alpha = parseInt(container.querySelector('.alpha-slider').value);
        container.querySelector('.hex-input').value = '#' + alpha.toString(16).padStart(2, '0').toUpperCase() + rgb.toUpperCase();
    }

    function updateControlsFromHex(container) {
        var parsed = parseARGBHex(container.querySelector('.hex-input').value);
        if (parsed) {
            container.querySelector('.color-picker').value = parsed.rgb;
            container.querySelector('.alpha-slider').value = parsed.alpha;
            container.querySelector('.alpha-label').textContent = parsed.alpha;
        }
    }

    function bindColorControls() {
        view.querySelectorAll('.color-picker').forEach(function (picker) {
            var container = picker.parentElement;
            var slider = container.querySelector('.alpha-slider');
            var label = container.querySelector('.alpha-label');

            picker.addEventListener('input', function () { updateHexFromControls(container); checkDirty(); });
            slider.addEventListener('input', function () { label.textContent = slider.value; updateHexFromControls(container); checkDirty(); });
            container.querySelector('.hex-input').addEventListener('input', function () { updateControlsFromHex(container); checkDirty(); });
        });
    }

    function syncColorControls() {
        view.querySelectorAll('.color-picker').forEach(function (picker) {
            var container = picker.parentElement;
            var hex = container.querySelector('.hex-input').value;
            if (hex) {
                var parsed = parseARGBHex(hex);
                if (parsed) {
                    picker.value = parsed.rgb;
                    container.querySelector('.alpha-slider').value = parsed.alpha;
                    container.querySelector('.alpha-label').textContent = parsed.alpha;
                }
            }
        });
    }

    // ── Config Loading ──────────────────────────────────────

    function loadConfig() {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            fullConfig = config;

            if (!config.PosterConfigurations || config.PosterConfigurations.length === 0) {
                config.PosterConfigurations = [{ Id: generateGuid(), Settings: {}, SeriesIds: [] }];
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

        view.querySelectorAll('[data-setting]').forEach(function (el) {
            var key = el.getAttribute('data-setting');
            var val = settings[key];

            if (el.type === 'checkbox') {
                el.checked = val !== false;
            } else if (el.getAttribute('data-type') === 'number') {
                el.value = val || 0;
            } else {
                el.value = val || '';
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

    // ── Series Management ───────────────────────────────────

    function updateSeriesAssignment() {
        var config = getCurrentConfig();
        var isDefault = config.IsDefault;

        view.querySelector('#seriesAssignmentSection').style.display = isDefault ? 'none' : 'block';
        view.querySelector('#btnDeleteConfig').classList.toggle('hidden', isDefault);
        view.querySelector('#btnRenameConfig').classList.toggle('hidden', isDefault);

        if (!isDefault) renderAssignedSeries();
    }

    function renderAssignedSeries() {
        var config = getCurrentConfig();
        var container = view.querySelector('#assignedSeriesList');
        container.innerHTML = '';

        if (!config.SeriesIds || config.SeriesIds.length === 0) {
            var msg = document.createElement('div');
            msg.className = 'series-empty-state';
            msg.innerHTML = '<span class="series-empty-state-icon">&#9888;</span> No series assigned — assign at least one series before saving.';
            container.appendChild(msg);
            return;
        }

        config.SeriesIds.forEach(function (seriesId) {
            var series = allSeries.find(function (s) { return s.Id === seriesId; });
            if (!series) return;

            var tag = document.createElement('div');
            tag.className = 'series-tag';

            var img = document.createElement('img');
            img.className = 'series-tag-poster';
            img.src = ApiClient.getImageUrl(series.Id, { type: 'Primary', maxWidth: 64, quality: 90 });
            img.onerror = function () { this.style.display = 'none'; };

            var name = document.createElement('span');
            name.className = 'series-tag-name';
            name.textContent = series.Name;

            var remove = document.createElement('span');
            remove.className = 'series-tag-remove';
            remove.textContent = '\u00d7';
            remove.setAttribute('data-series-id', seriesId);

            tag.appendChild(img);
            tag.appendChild(name);
            tag.appendChild(remove);
            container.appendChild(tag);
        });

        container.querySelectorAll('.series-tag-remove').forEach(function (btn) {
            btn.addEventListener('click', function () {
                removeSeries(this.getAttribute('data-series-id'));
            });
        });
    }

    function loadAllSeries() {
        return ApiClient.getItems(ApiClient.getCurrentUserId(), {
            IncludeItemTypes: 'Series',
            Recursive: true,
            SortBy: 'SortName',
            SortOrder: 'Ascending',
            Fields: 'Overview,ProductionYear'
        }).then(function (result) {
            allSeries = result.Items || [];
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

        if (!allSeries || allSeries.length === 0) {
            listContainer.innerHTML = '<div class="series-modal-loading"><div class="series-modal-spinner"></div><span>Loading series...</span></div>';
            view.querySelector('#seriesSelectionSummary').textContent = '';
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
        var availableCount = 0;

        listContainer.innerHTML = '';

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

            var poster = document.createElement('img');
            poster.className = 'series-item-poster';
            poster.src = ApiClient.getImageUrl(series.Id, { type: 'Primary', maxWidth: 80, quality: 80 });
            poster.onerror = function () { this.style.visibility = 'hidden'; };

            var info = document.createElement('div');
            info.className = 'series-item-info';

            var nameSpan = document.createElement('span');
            nameSpan.className = 'series-item-name';
            nameSpan.textContent = series.Name;
            info.appendChild(nameSpan);

            if (series.ProductionYear) {
                var yearSpan = document.createElement('span');
                yearSpan.className = 'series-item-year';
                yearSpan.textContent = series.ProductionYear;
                info.appendChild(yearSpan);
            }

            if (series.Overview) {
                var descSpan = document.createElement('span');
                descSpan.className = 'series-item-overview';
                descSpan.textContent = series.Overview.length > 120 ? series.Overview.substring(0, 120) + '...' : series.Overview;
                info.appendChild(descSpan);
            }

            if (isAssignedElsewhere) {
                var badge = document.createElement('span');
                badge.className = 'series-item-badge';
                badge.textContent = 'Assigned elsewhere';
                info.appendChild(badge);
            }

            item.appendChild(checkbox);
            item.appendChild(poster);
            item.appendChild(info);
            listContainer.appendChild(item);

            checkbox.addEventListener('change', updateSelectionSummary);
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

    var debouncedFilterSeriesList = debounce(function () {
        var term = view.querySelector('#seriesSearchInput').value.toLowerCase();
        view.querySelectorAll('.series-checkbox-item').forEach(function (item) {
            var name = item.querySelector('.series-item-name');
            item.style.display = (name ? name.textContent : item.textContent).toLowerCase().includes(term) ? '' : 'none';
        });
    }, 200);

    function getAllAssignedSeriesIds() {
        var ids = [];
        fullConfig.PosterConfigurations.forEach(function (c) {
            if (c.SeriesIds) ids.push.apply(ids, c.SeriesIds);
        });
        return ids;
    }

    function confirmSeriesSelection() {
        var config = getCurrentConfig();
        var selectedIds = [];
        view.querySelectorAll('.series-checkbox:checked').forEach(function (cb) {
            selectedIds.push(cb.value);
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
            trapFocus(view.querySelector('.series-modal-content'), e);
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

    // ── Config CRUD ─────────────────────────────────────────

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
            if (fullConfig.PosterConfigurations.some(function (c) {
                return c.Name && c.Name.toLowerCase() === name.trim().toLowerCase();
            })) {
                Dashboard.alert('A configuration with this name already exists.');
                return;
            }

            var newConfig = {
                Id: generateGuid(),
                Name: name.trim(),
                Settings: {
                    ExtractPoster: true,
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

        showInputModal('Rename Configuration', [
            { label: 'Name', value: config.Name || 'Unnamed', required: true }
        ], function (values) {
            if (!values) return;
            var newName = values[0];

            if (!newName || newName.trim() === '') return;
            if (newName.trim().toLowerCase() === 'default') {
                Dashboard.alert('The name "Default" is reserved and cannot be used.');
                return;
            }
            if (fullConfig.PosterConfigurations.some(function (c) {
                return c.Id !== config.Id && c.Name && c.Name.toLowerCase() === newName.trim().toLowerCase();
            })) {
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

                    showInputModal('Import Configuration', [
                        { label: 'Name', value: template.name || 'Imported Configuration', required: true, description: 'Enter a name for the imported configuration.' }
                    ], function (values) {
                        if (!values) return;
                        var nameInput = values[0];

                        if (!nameInput || nameInput.trim() === '') return;
                        if (nameInput.trim().toLowerCase() === 'default') {
                            Dashboard.alert('The name "Default" is reserved and cannot be used.');
                            return;
                        }
                        if (fullConfig.PosterConfigurations.some(function (c) {
                            return c.Name && c.Name.toLowerCase() === nameInput.trim().toLowerCase();
                        })) {
                            Dashboard.alert('A configuration with this name already exists.');
                            return;
                        }

                        fullConfig.PosterConfigurations.push({
                            Id: generateGuid(),
                            Name: nameInput.trim(),
                            Settings: template.settings,
                            SeriesIds: [],
                            IsDefault: false
                        });
                        currentConfigId = fullConfig.PosterConfigurations[fullConfig.PosterConfigurations.length - 1].Id;
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

            if (!author || !author.trim()) { Dashboard.alert('Author name is required.'); return; }
            if (!description || !description.trim()) { Dashboard.alert('Description is required.'); return; }

            Dashboard.showLoadingMsg();
            ApiClient.getPluginConfiguration(pluginId).then(function (pluginConfig) {
                var ver = pluginConfig.Version;
                var json = JSON.stringify({
                    name: config.Name,
                    description: description.trim(),
                    author: author.trim(),
                    version: ver,
                    createdDate: new Date().toISOString(),
                    settings: config.Settings
                }, null, 2);

                var blob = new Blob([json], { type: 'application/json' });
                var url = URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = url;
                a.download = config.Name.replace(/[^a-z0-9\s]/gi, '').replace(/\s+/g, '_') + '_v' + ver + '.json';
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

    // ── Save ────────────────────────────────────────────────

    function saveCurrentConfigSettings() {
        var config = getCurrentConfig();
        if (!config) return;
        if (!config.Settings) config.Settings = {};

        view.querySelectorAll('[data-setting]').forEach(function (el) {
            var key = el.getAttribute('data-setting');
            if (el.type === 'checkbox') {
                config.Settings[key] = el.checked;
            } else if (el.getAttribute('data-type') === 'number') {
                config.Settings[key] = parseFloat(el.value) || 0;
            } else {
                config.Settings[key] = el.value;
            }
        });
    }

    function validateNumberInputs() {
        var errors = [];
        view.querySelectorAll('[data-setting][data-type="number"]').forEach(function (input) {
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
                if (!isNaN(min) && value < min) errors.push(name + ' must be at least ' + min + '.');
                if (!isNaN(max) && value > max) errors.push(name + ' must be at most ' + max + '.');
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

        var invalidConfigs = fullConfig.PosterConfigurations.filter(function (c) {
            return c.IsDefault !== true && (!c.SeriesIds || c.SeriesIds.length === 0);
        });

        if (invalidConfigs.length > 0) {
            var names = invalidConfigs.map(function (c) { return c.Name || 'Unnamed'; }).join(', ');
            Dashboard.alert('Cannot save: The following non-default configurations have no series assigned: ' + names + '. Please assign series or delete these configurations.');
            return;
        }

        Dashboard.showLoadingMsg();
        ApiClient.updatePluginConfiguration(pluginId, fullConfig).then(function (result) {
            markClean();
            flashSaveSuccess();
            Dashboard.processPluginConfigurationUpdateResult(result);
        });
    }

    // ── Style Descriptions ──────────────────────────────────

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
        var el = view.querySelector('#posterStyleDescription');
        if (el) el.textContent = posterStyleDescriptions[style] || '';
    }

    // ── Visibility ──────────────────────────────────────────

    function updateVisibility() {
        var posterStyle = view.querySelector('#selectPosterStyle').value;
        var posterFill = view.querySelector('#selectPosterFill').value;

        // Show/hide elements based on supported poster styles
        view.querySelectorAll('[data-poster-styles]').forEach(function (el) {
            el.style.display = el.getAttribute('data-poster-styles').split(',').includes(posterStyle) ? 'block' : 'none';
        });

        // Hide elements for specific fill modes
        view.querySelectorAll('[data-hide-for-posterfill]').forEach(function (el) {
            el.style.display = el.getAttribute('data-hide-for-posterfill').split(',').includes(posterFill) ? 'none' : 'block';
        });

        // Hide elements for specific styles
        view.querySelectorAll('[data-hide-for-styles]').forEach(function (el) {
            el.style.display = el.getAttribute('data-hide-for-styles').split(',').includes(posterStyle) ? 'none' : 'block';
        });

        // Checkbox dependency chains
        view.querySelectorAll('[data-depends-on]').forEach(function (el) {
            var deps = el.getAttribute('data-depends-on').split(',');
            var met = deps.every(function (id) {
                var dep = view.querySelector('#' + id.trim());
                return dep && dep.checked;
            });

            var hideForStyles = el.getAttribute('data-hide-for-styles');
            var hiddenByStyle = hideForStyles && hideForStyles.split(',').includes(posterStyle);
            el.style.display = (met && !hiddenByStyle) ? 'block' : 'none';
        });

        // Gradient dependency
        view.querySelectorAll('[data-depends-on-gradient]').forEach(function (el) {
            var select = view.querySelector('#' + el.getAttribute('data-depends-on-gradient'));
            el.style.display = (select && select.value !== 'None') ? 'block' : 'none';
        });

        // Value dependency (show when input has a value)
        view.querySelectorAll('[data-depends-on-value]').forEach(function (el) {
            var input = view.querySelector('#' + el.getAttribute('data-depends-on-value'));
            el.style.display = (input && input.value && input.value.trim() !== '') ? 'block' : 'none';
        });

        // Hide when checkbox is checked (inverse dependency)
        view.querySelectorAll('[data-hide-when-checked]').forEach(function (el) {
            var cb = view.querySelector('#' + el.getAttribute('data-hide-when-checked'));
            el.style.display = (cb && cb.checked) ? 'none' : 'block';
        });

        // Force-enable checkboxes for styles that require them
        if (posterStyle === 'Cutout' || posterStyle === 'Numeral' || posterStyle === 'Brush') {
            view.querySelector('#chkShowEpisode').checked = true;
        }
        if (posterStyle === 'Frame' || posterStyle === 'Brush') {
            view.querySelector('#chkShowTitle').checked = true;
        }
    }

    // ── Event Binding ───────────────────────────────────────

    function bindEventListeners() {
        // Form save
        view.querySelector('#EpgPostersForm').addEventListener('submit', function (e) {
            e.preventDefault();
            saveConfig();
        });

        // Config selector
        view.querySelector('#selectPosterConfig').addEventListener('change', function () {
            saveCurrentConfigSettings();
            currentConfigId = this.value;
            loadCurrentConfig();
            view.querySelector('.content-primary').scrollIntoView({ behavior: 'smooth' });
        });

        // Config action buttons
        view.querySelector('#btnNewConfig').addEventListener('click', createNewConfig);
        view.querySelector('#btnDeleteConfig').addEventListener('click', deleteCurrentConfig);
        view.querySelector('#btnRenameConfig').addEventListener('click', renameCurrentConfig);
        view.querySelector('#btnExportConfig').addEventListener('click', exportCurrentConfig);
        view.querySelector('#btnImportConfig').addEventListener('click', importCurrentConfig);

        // Series modal
        view.querySelector('#btnAddSeries').addEventListener('click', showSeriesSelectionModal);
        view.querySelector('#btnCancelSeriesSelection').addEventListener('click', closeSeriesSelectionModal);
        view.querySelector('#btnCloseSeriesModal').addEventListener('click', closeSeriesSelectionModal);
        view.querySelector('#btnConfirmSeriesSelection').addEventListener('click', confirmSeriesSelection);
        view.querySelector('#seriesSelectionModal').addEventListener('click', function (e) {
            if (e.target === this) closeSeriesSelectionModal();
        });
        view.querySelector('#seriesSearchInput').addEventListener('input', debouncedFilterSeriesList);

        // Controls that affect visibility
        var visibilityControls = [
            '#selectPosterStyle', '#chkShowTitle', '#chkShowEpisode', '#chkExtractPoster',
            '#chkEnableLetterboxDetection', '#selectPosterFill', '#selectOverlayGradient',
            '#chkEpisodeUseCustomFont', '#chkTitleUseCustomFont'
        ];
        visibilityControls.forEach(function (selector) {
            var el = view.querySelector(selector);
            if (el) {
                var evt = (el.type === 'checkbox' || el.tagName === 'SELECT') ? 'change' : 'input';
                el.addEventListener(evt, function () { updateVisibility(); checkDirty(); });
            }
        });

        // Graphic path also affects visibility
        view.querySelector('#txtGraphicPath').addEventListener('input', function () {
            updateVisibility();
            checkDirty();
        });

        // Style description updates
        view.querySelector('#selectPosterStyle').addEventListener('change', updateStyleDescription);

        // Track changes on all settings inputs
        view.querySelectorAll('[data-setting]').forEach(function (el) {
            var evt = (el.type === 'checkbox' || el.tagName === 'SELECT') ? 'change' : 'input';
            el.addEventListener(evt, checkDirty);
        });
    }

    // ── Lifecycle ───────────────────────────────────────────

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
                LibraryMenu.setTabs('epg', 0, getTabs);
            }
        }
    });
}
