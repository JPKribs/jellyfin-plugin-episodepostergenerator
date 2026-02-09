export default function (view) {
    'use strict';

    var pluginId = 'b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e';
    var _initialized = false;
    var _saving = false;
    var _dirty = false;
    var _savedSnapshot = null;

    function getTabs() {
        return [
            { href: 'configurationpage?name=epg_posters', name: 'Posters' },
            { href: 'configurationpage?name=epg_settings', name: 'Settings' }
        ];
    }

    // ===== Unsaved Changes =====

    function takeSnapshot() {
        _savedSnapshot = JSON.stringify({
            EnableProvider: view.querySelector('#chkEnableProvider').checked,
            EnableTask: view.querySelector('#chkEnableTask').checked
        });
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
        takeSnapshot();
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
        if (!_savedSnapshot) return;
        var current = JSON.stringify({
            EnableProvider: view.querySelector('#chkEnableProvider').checked,
            EnableTask: view.querySelector('#chkEnableTask').checked
        });
        if (current !== _savedSnapshot) {
            markDirty();
        } else {
            _dirty = false;
            var indicator = view.querySelector('#unsavedIndicator');
            if (indicator) indicator.classList.remove('visible');
        }
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

    // ===== Config =====

    function loadConfig() {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            view.querySelector('#chkEnableProvider').checked = config.EnableProvider !== false;
            view.querySelector('#chkEnableTask').checked = config.EnableTask !== false;
            takeSnapshot();
            markClean();
            Dashboard.hideLoadingMsg();
        }).catch(function (error) {
            console.error('Failed to load config:', error);
            Dashboard.hideLoadingMsg();
        });
    }

    function savePluginSettings() {
        if (_saving) return;
        _saving = true;

        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.EnableProvider = view.querySelector('#chkEnableProvider').checked;
            config.EnableTask = view.querySelector('#chkEnableTask').checked;
            return ApiClient.updatePluginConfiguration(pluginId, config);
        }).then(function (result) {
            markClean();
            flashSaveSuccess();
            Dashboard.processPluginConfigurationUpdateResult(result);
        }).catch(function (error) {
            console.error('Failed to save settings:', error);
            Dashboard.hideLoadingMsg();
        }).finally(function () {
            _saving = false;
        });
    }

    // ===== Reset History =====

    function resetHistory() {
        var confirmationHtml =
            '<div class="reset-warning">' +
            '<h3>Warning</h3>' +
            '<p>This will permanently delete all episode processing history.</p>' +
            '<p>After resetting, all episodes will be reprocessed on the next run, which may take considerable time for large libraries.</p>' +
            '<p><strong>Are you sure you want to continue?</strong></p>' +
            '</div>';

        Dashboard.confirm(confirmationHtml, 'Reset Processing History', function (confirmed) {
            if (confirmed) {
                performReset();
            }
        });
    }

    function performReset() {
        Dashboard.showLoadingMsg();

        fetch('/Plugins/EpisodePosterGenerator/ResetHistory', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Emby-Token': ApiClient.accessToken()
            }
        })
        .then(function (response) {
            return response.ok ? response.json() : Promise.reject('Failed');
        })
        .then(function (data) {
            Dashboard.hideLoadingMsg();
            Dashboard.alert((data.clearedCount || 0) + ' Records Deleted', 'History Reset Complete');
        })
        .catch(function (error) {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Failed to reset processing history.', 'Error');
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
        LibraryMenu.setTabs('epg', 1, getTabs);

        if (!_initialized) {
            _initialized = true;
            initCollapsibles();
            view.querySelector('#btnSavePlugin').addEventListener('click', savePluginSettings);
            view.querySelector('#btnResetHistory').addEventListener('click', resetHistory);
            view.querySelector('#chkEnableProvider').addEventListener('change', checkDirty);
            view.querySelector('#chkEnableTask').addEventListener('change', checkDirty);
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
                LibraryMenu.setTabs('epg', 1, getTabs);
            }
        }
    });
}
