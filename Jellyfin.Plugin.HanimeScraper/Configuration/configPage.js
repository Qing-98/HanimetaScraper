(function () {
    var pluginId = "18a03d4c-4691-424c-9fda-fe675ea849c4";

    function log(){ try{ console.log.apply(console, arguments); }catch(_){}}

    function loadPage(page, config) {
        page.querySelector('#txtBackendUrl').value = config.BackendUrl || 'http://127.0.0.1:8585';
        page.querySelector('#txtApiToken').value = config.ApiToken || '';
        page.querySelector('#chkEnableLogging').checked = config.EnableLogging || false;
        Dashboard.hideLoadingMsg();
        log('[Hanime] loadPage ok');
    }

    function save() {
        Dashboard.showLoadingMsg();
        var page = document.querySelector('#HanimeScraperConfigPage');
        var newCfg = {
            BackendUrl: (page.querySelector('#txtBackendUrl').value || '').trim(),
            ApiToken: (page.querySelector('#txtApiToken').value || '').trim(),
            EnableLogging: page.querySelector('#chkEnableLogging').checked
        };
        log('[Hanime] Saving configuration (ui values):', newCfg);

        ApiClient.getPluginConfiguration(pluginId)
            .then(function (currentConfig) {
                log('[Hanime] Current server configuration:', currentConfig);
                Object.assign(currentConfig, newCfg);
                log('[Hanime] Sending updatePluginConfiguration:', currentConfig);
                return ApiClient.updatePluginConfiguration(pluginId, currentConfig);
            })
            .then(function (result) {
                log('[Hanime] updatePluginConfiguration result:', result);
                Dashboard.processPluginConfigurationUpdateResult(result);
                return ApiClient.getPluginConfiguration(pluginId);
            })
            .then(function (serverCfg) {
                log('[Hanime] Configuration after save (server read-back):', serverCfg);
            })
            .catch(function (err) {
                log('[Hanime] Error saving configuration:', err);
                try { Dashboard.hideLoadingMsg(); } catch (e2) {}
                alert('Save failed: ' + (err && (err.message || err)));
            });
    }

    function getTabs() {
        return [{ href: Dashboard.getPluginUrl(pluginId), name: 'Hanime Scraper' }];
    }

    function wire(){
        var page = document.querySelector('#HanimeScraperConfigPage');
        if (!page){
            log('[Hanime] page element not found, retry...');
            setTimeout(wire, 200);
            return;
        }

        page.addEventListener('pageshow', function(){
            Dashboard.showLoadingMsg();
            LibraryMenu.setTabs('plugins', 0, getTabs);
            ApiClient.getPluginConfiguration(pluginId)
                .then(function (config) {
                    log('[Hanime] Loaded configuration from server:', config);
                    loadPage(page, config);
                })
                .catch(function (err) {
                    log('[Hanime] Error loading configuration:', err);
                    Dashboard.hideLoadingMsg();
                });
        });

        var btn = page.querySelector('#btnSave');
        if (btn){
            btn.addEventListener('click', save);
            log('[Hanime] #btnSave wired');
        } else {
            log('[Hanime] #btnSave not found');
        }
    }

    log('[Hanime] configPage bootstrap');
    wire();
})();
