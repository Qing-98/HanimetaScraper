(function () {
    var pluginId = "d2e313b1-0c08-4a4b-9696-768a06561c3f";

    function log(){ try{ console.log.apply(console, arguments); }catch(_){}}

    function loadPage(page, config) {
        page.querySelector('#txtBackendUrl').value = config.BackendUrl || 'http://127.0.0.1:8585';
        page.querySelector('#txtApiToken').value = config.ApiToken || '';
        page.querySelector('#chkEnableLogging').checked = config.EnableLogging || false;
        Dashboard.hideLoadingMsg();
        log('[DLsite] loadPage ok');
    }

    function save() {
        Dashboard.showLoadingMsg();
        var page = document.querySelector('#DLsiteConfigurationPage');
        if (!page) {
            log('[DLsite] save skipped - configuration page not found');
            Dashboard.hideLoadingMsg();
            return;
        }

        var newCfg = {
            BackendUrl: (page.querySelector('#txtBackendUrl').value || '').trim(),
            ApiToken: (page.querySelector('#txtApiToken').value || '').trim(),
            EnableLogging: page.querySelector('#chkEnableLogging').checked
        };

        log('[DLsite] Saving configuration (ui values):', newCfg);

        ApiClient.getPluginConfiguration(pluginId)
            .then(function (currentConfig) {
                log('[DLsite] Current server configuration:', currentConfig);
                Object.assign(currentConfig, newCfg);
                log('[DLsite] Sending updatePluginConfiguration:', currentConfig);
                return ApiClient.updatePluginConfiguration(pluginId, currentConfig);
            })
            .then(function (result) {
                log('[DLsite] updatePluginConfiguration result:', result);
                Dashboard.processPluginConfigurationUpdateResult(result);
                return ApiClient.getPluginConfiguration(pluginId);
            })
            .then(function (serverCfg) {
                log('[DLsite] Configuration after save (server read-back):', serverCfg);
            })
            .catch(function (err) {
                log('[DLsite] Error saving configuration:', err);
                try { Dashboard.hideLoadingMsg(); } catch (e2) {}
                alert('Save failed: ' + (err && (err.message || err)));
            });
    }

    function getTabs() {
        return [{ href: Dashboard.getPluginUrl(pluginId), name: 'DLsite Scraper' }];
    }

    function onPageShow(page) {
        Dashboard.showLoadingMsg();
        LibraryMenu.setTabs('plugins', 0, getTabs);
        ApiClient.getPluginConfiguration(pluginId)
            .then(function (config) {
                log('[DLsite] Loaded configuration from server:', config);
                loadPage(page, config);
            })
            .catch(function (err) {
                log('[DLsite] Error loading configuration:', err);
                Dashboard.hideLoadingMsg();
            });
    }

    var wired = false;
    function wire(){
        if (wired){
            return;
        }

        var page = document.querySelector('#DLsiteConfigurationPage');
        if (!page){
            log('[DLsite] page element not found, retry...');
            setTimeout(wire, 200);
            return;
        }

        wired = true;

        function handleShow(){
            onPageShow(page);
        }

        page.addEventListener('pageshow', handleShow);
        page.addEventListener('viewshow', handleShow);

        handleShow();

        var btn = page.querySelector('#btnSave');
        if (btn){
            btn.addEventListener('click', save);
            log('[DLsite] #btnSave wired');
        } else {
            log('[DLsite] #btnSave not found');
        }
    }

    log('[DLsite] configPage bootstrap');

    if (document.readyState === 'complete' || document.readyState === 'interactive') {
        wire();
    } else {
        document.addEventListener('DOMContentLoaded', wire);
    }
})();
