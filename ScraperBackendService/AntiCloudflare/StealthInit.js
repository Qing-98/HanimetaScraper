(() => {
    // ===== Core webdriver removal =====
    try {
        Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
    } catch (e) { }
    try {
        Object.defineProperty(window, 'webdriver', { get: () => undefined });
        Object.defineProperty(document, 'webdriver', { get: () => undefined });
    } catch (e) { }
    
    // ===== Enhanced plugin emulation =====
    try {
        const fakePlugins = [
            { name: 'Chrome PDF Plugin', description: 'Portable Document Format', filename: 'internal-pdf-viewer' },
            { name: 'Chrome PDF Viewer', description: 'Portable Document Format', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai' },
            { name: 'Chromium PDF Plugin', description: 'Portable Document Format', filename: 'internal-pdf-viewer' },
            { name: 'Microsoft Edge PDF Plugin', description: 'Portable Document Format', filename: 'internal-pdf-viewer' },
            { name: 'WebKit built-in PDF', description: 'Portable Document Format', filename: 'internal-pdf-viewer' }
        ];
        Object.defineProperty(navigator, 'plugins', { get: () => fakePlugins });
    } catch (e) { }
    
    // ===== Enhanced language configuration =====
    try {
        Object.defineProperty(navigator, 'languages', { get: () => ['zh-CN', 'zh', 'en-US', 'en'] });
    } catch (e) { }
    
    // ===== Permission API enhancement =====
    try {
        const originalQuery = window.navigator.permissions.query;
        window.navigator.permissions.query = (parameters) => {
            if (parameters && parameters.name === 'notifications') {
                return Promise.resolve({ state: Notification.permission });
            }
            return originalQuery(parameters);
        };
    } catch (e) { }
    
    // ===== Enhanced Chrome object =====
    try {
        if (!window.chrome) window.chrome = {};
        if (!window.chrome.runtime) {
            window.chrome.runtime = {
                PlatformOs: { ANDROID: 'android', CROS: 'cros', LINUX: 'linux', MAC: 'mac', OPENBSD: 'openbsd', WIN: 'win' },
                PlatformArch: { ARM: 'arm', ARM64: 'arm64', MIPS: 'mips', MIPS64: 'mips64', X86_32: 'x86-32', X86_64: 'x86-64' },
                PlatformNaclArch: { ARM: 'arm', MIPS: 'mips', MIPS64: 'mips64', X86_32: 'x86-32', X86_64: 'x86-64' },
                RequestUpdateCheckStatus: { NO_UPDATE: 'no_update', THROTTLED: 'throttled', UPDATE_AVAILABLE: 'update_available' },
                OnInstalledReason: { CHROME_UPDATE: 'chrome_update', INSTALL: 'install', SHARED_MODULE_UPDATE: 'shared_module_update', UPDATE: 'update' },
                OnRestartRequiredReason: { APP_UPDATE: 'app_update', OS_UPDATE: 'os_update', PERIODIC: 'periodic' }
            };
        }
        if (!window.chrome.loadTimes) {
            window.chrome.loadTimes = function () { 
                return { 
                    requestTime: Date.now() / 1000,
                    startLoadTime: Date.now() / 1000,
                    commitLoadTime: Date.now() / 1000,
                    finishDocumentLoadTime: Date.now() / 1000,
                    finishLoadTime: Date.now() / 1000,
                    firstPaintTime: Date.now() / 1000,
                    firstPaintAfterLoadTime: 0,
                    navigationType: 'Other',
                    wasFetchedViaSpdy: false,
                    wasNpnNegotiated: true,
                    npnNegotiatedProtocol: 'h2',
                    wasAlternateProtocolAvailable: false,
                    connectionInfo: 'h2'
                }; 
            };
        }
        if (!window.chrome.csi) {
            window.chrome.csi = function () {
                return {
                    onloadT: Date.now(),
                    pageT: Date.now() - performance.timing.navigationStart,
                    startE: performance.timing.navigationStart,
                    tran: 15
                };
            };
        }
        if (!window.chrome.app) {
            window.chrome.app = {
                isInstalled: false,
                InstallState: { DISABLED: 'disabled', INSTALLED: 'installed', NOT_INSTALLED: 'not_installed' },
                RunningState: { CANNOT_RUN: 'cannot_run', READY_TO_RUN: 'ready_to_run', RUNNING: 'running' }
            };
        }
    } catch (e) { }
    
    // ===== Platform configuration =====
    try {
        Object.defineProperty(navigator, 'platform', { get: () => 'Win32' });
    } catch (e) { }
    
    // ===== Hardware configuration =====
    try {
        if (window.navigator.hardwareConcurrency === undefined || window.navigator.hardwareConcurrency === 0) {
            Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8 });
        }
    } catch (e) { }
    try {
        if (navigator.maxTouchPoints === 0) {
            Object.defineProperty(navigator, 'maxTouchPoints', { get: () => 1 });
        }
    } catch (e) { }
    try {
        Object.defineProperty(navigator, 'deviceMemory', { get: () => 8 });
    } catch (e) { }
    
    // ===== Enhanced Function.prototype.toString override =====
    try {
        const originalToString = Function.prototype.toString;
        const proxyToString = new Proxy(originalToString, {
            apply(target, thisArg, args) {
                if (thisArg === window.navigator.permissions.query) {
                    return 'function query() { [native code] }';
                }
                if (thisArg === window.chrome.loadTimes) {
                    return 'function loadTimes() { [native code] }';
                }
                if (thisArg === window.chrome.csi) {
                    return 'function csi() { [native code] }';
                }
                return target.apply(thisArg, args);
            }
        });
        Function.prototype.toString = proxyToString;
        Object.defineProperty(Function.prototype.toString, 'toString', {
            value: () => 'function toString() { [native code] }',
            writable: false,
            enumerable: false,
            configurable: false
        });
    } catch (e) { }
    
    // ===== Iframe webdriver removal =====
    try {
        const originalCreateElement = Document.prototype.createElement;
        Document.prototype.createElement = function (tagName) {
            const el = originalCreateElement.call(this, tagName);
            if (tagName && tagName.toLowerCase() === 'iframe') {
                try {
                    Object.defineProperty(el.contentWindow, 'webdriver', { get: () => undefined });
                } catch (e) { }
            }
            return el;
        };
    } catch (e) { }
    
    // ===== User activation API =====
    try {
        if (!navigator.userActivation) {
            Object.defineProperty(navigator, 'userActivation', {
                get: () => ({
                    hasBeenActive: true,
                    isActive: true
                })
            });
        }
    } catch (e) { }
    
    // ===== Connection API =====
    try {
        if (navigator.connection && navigator.connection.rtt === 0) {
            Object.defineProperty(navigator.connection, 'rtt', { get: () => 100 });
        }
    } catch (e) { }
    
    // ===== Screen resolution matching =====
    try {
        // Make sure screen dimensions are reasonable
        if (screen.width === 0 || screen.height === 0) {
            Object.defineProperty(screen, 'width', { get: () => 1920 });
            Object.defineProperty(screen, 'height', { get: () => 1080 });
            Object.defineProperty(screen, 'availWidth', { get: () => 1920 });
            Object.defineProperty(screen, 'availHeight', { get: () => 1040 });
        }
    } catch (e) { }
    
    // ===== Battery API =====
    try {
        const originalGetBattery = navigator.getBattery;
        if (originalGetBattery) {
            navigator.getBattery = function () {
                return Promise.resolve({
                    charging: true,
                    chargingTime: 0,
                    dischargingTime: Infinity,
                    level: 1,
                    addEventListener: () => {},
                    removeEventListener: () => {},
                    dispatchEvent: () => true
                });
            };
        }
    } catch (e) { }
    
    // ===== Media devices API =====
    try {
        const originalEnumerateDevices = navigator.mediaDevices?.enumerateDevices;
        if (originalEnumerateDevices) {
            navigator.mediaDevices.enumerateDevices = function () {
                return originalEnumerateDevices.call(navigator.mediaDevices).then(devices => {
                    // Ensure at least some devices are present
                    if (devices.length === 0) {
                        return [
                            { kind: 'audioinput', deviceId: 'default', label: '', groupId: 'default' },
                            { kind: 'videoinput', deviceId: 'default', label: '', groupId: 'default' },
                            { kind: 'audiooutput', deviceId: 'default', label: '', groupId: 'default' }
                        ];
                    }
                    return devices;
                });
            };
        }
    } catch (e) { }
    
    // ===== Remove automation-controlled flag from window =====
    try {
        delete window.cdc_adoQpoasnfa76pfcZLmcfl_Array;
        delete window.cdc_adoQpoasnfa76pfcZLmcfl_Promise;
        delete window.cdc_adoQpoasnfa76pfcZLmcfl_Symbol;
        delete window.cdc_adoQpoasnfa76pfcZLmcfl_Object;
        delete window.cdc_adoQpoasnfa76pfcZLmcfl_Proxy;
    } catch (e) { }
    
    // ===== Notification API =====
    try {
        if (window.Notification) {
            Object.defineProperty(Notification, 'permission', {
                get: () => 'default'
            });
        }
    } catch (e) { }
    
    // ===== Performance API normalization =====
    try {
        if (performance.timing) {
            const timingStart = performance.timing.navigationStart || Date.now();
            // Ensure reasonable timing values
            if (performance.timing.connectEnd - performance.timing.connectStart < 1) {
                Object.defineProperty(performance.timing, 'connectEnd', {
                    get: () => timingStart + 50
                });
            }
        }
    } catch (e) { }
    
    // ===== Additional vendor and renderer information =====
    try {
        const canvas = document.createElement('canvas');
        const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
        if (gl) {
            const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
            if (debugInfo) {
                Object.defineProperty(gl, 'getParameter', {
                    value: new Proxy(gl.getParameter, {
                        apply(target, thisArg, args) {
                            const param = args[0];
                            if (param === debugInfo.UNMASKED_VENDOR_WEBGL) {
                                return 'Google Inc. (Intel)';
                            }
                            if (param === debugInfo.UNMASKED_RENDERER_WEBGL) {
                                return 'ANGLE (Intel, Intel(R) UHD Graphics 620 Direct3D11 vs_5_0 ps_5_0, D3D11)';
                            }
                            return target.apply(thisArg, args);
                        }
                    })
                });
            }
        }
    } catch (e) { }
})();
