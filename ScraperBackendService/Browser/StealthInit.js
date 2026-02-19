(() => {
// ©¤©¤ 0. Automation / CDP artifact cleanup ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
// ChromeDriver injects window.cdc_* variables; Playwright CDP may inject
// __pw_* bindings via Runtime.addBinding. Delete these proactively before
// any page script can read them.
try {
    const _autoPatterns = [/^cdc_/, /^\$cdc_/, /^__pw_/, /^__playwright/];
    for (const _key of Object.getOwnPropertyNames(window)) {
        if (_autoPatterns.some(p => p.test(_key))) {
            try { Reflect.deleteProperty(window, _key); } catch (_e) { }
        }
    }
} catch (_e) { }

// ©¤©¤ 1. navigator.webdriver ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
try {
    Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
    Object.defineProperty(window,    'webdriver', { get: () => undefined });
    Object.defineProperty(document,  'webdriver', { get: () => undefined });
} catch (e) { }

    // ©¤©¤ 2. Plugins (Turnstile checks plugin count > 0) ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        const mimeTypes = [
            { type: 'application/pdf', suffixes: 'pdf', description: '', enabledPlugin: null },
            { type: 'application/x-google-chrome-pdf', suffixes: 'pdf', description: 'Portable Document Format', enabledPlugin: null }
        ];
        const plugins = [
            { name: 'Chrome PDF Plugin',                filename: 'internal-pdf-viewer',  description: 'Portable Document Format', length: 1 },
            { name: 'Chrome PDF Viewer',                filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai', description: '',                    length: 1 },
            { name: 'Native Client',                    filename: 'internal-nacl-plugin',  description: '',                    length: 2 },
            { name: 'Widevine Content Decryption Module', filename: 'widevinecdmadapter.dll', description: 'Enables Widevine licenses for playback of HTML audio/video content. (version: 4.10.2710.0)', length: 1 }
        ];
        Object.defineProperty(navigator, 'plugins', { get: () => plugins });
        Object.defineProperty(navigator, 'mimeTypes', { get: () => mimeTypes });
    } catch (e) { }

    // ©¤©¤ 3. Languages ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        Object.defineProperty(navigator, 'languages', { get: () => ['zh-CN', 'zh', 'en-US', 'en'] });
    } catch (e) { }

    // ©¤©¤ 4. Platform / vendor ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        Object.defineProperty(navigator, 'platform', { get: () => 'Win32' });
        Object.defineProperty(navigator, 'vendor',   { get: () => 'Google Inc.' });
        Object.defineProperty(navigator, 'vendorSub',{ get: () => '' });
        Object.defineProperty(navigator, 'productSub',{ get: () => '20030107' });
    } catch (e) { }

    // ©¤©¤ 5. Hardware ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8 });
        Object.defineProperty(navigator, 'deviceMemory',        { get: () => 8 });
        Object.defineProperty(navigator, 'maxTouchPoints',      { get: () => 0 });
    } catch (e) { }

    // ©¤©¤ 6. Permissions (Turnstile probes notification permission) ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        const _origQuery = window.navigator.permissions.query.bind(navigator.permissions);
        window.navigator.permissions.__proto__.query = (params) => {
            if (params && params.name === 'notifications') {
                return Promise.resolve({ state: Notification.permission, onchange: null });
            }
            return _origQuery(params);
        };
    } catch (e) { }

    // ©¤©¤ 7. window.chrome (Turnstile checks existence and structure) ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        if (!window.chrome) {
            Object.defineProperty(window, 'chrome', {
                value: {
                    app: { isInstalled: false, InstallState: { DISABLED: 'disabled', INSTALLED: 'installed', NOT_INSTALLED: 'not_installed' }, RunningState: { CANNOT_RUN: 'cannot_run', READY_TO_RUN: 'ready_to_run', RUNNING: 'running' } },
                    runtime: {
                        PlatformOs: { MAC: 'mac', WIN: 'win', ANDROID: 'android', CROS: 'cros', LINUX: 'linux', OPENBSD: 'openbsd' },
                        PlatformArch: { ARM: 'arm', X86_32: 'x86-32', X86_64: 'x86-64', MIPS: 'mips', MIPS64: 'mips64' },
                        PlatformNaclArch: { ARM: 'arm', X86_32: 'x86-32', X86_64: 'x86-64', MIPS: 'mips', MIPS64: 'mips64' },
                        RequestUpdateCheckStatus: { THROTTLED: 'throttled', NO_UPDATE: 'no_update', UPDATE_AVAILABLE: 'update_available' },
                        OnInstalledReason: { INSTALL: 'install', UPDATE: 'update', CHROME_UPDATE: 'chrome_update', SHARED_MODULE_UPDATE: 'shared_module_update' },
                        OnRestartRequiredReason: { APP_UPDATE: 'app_update', OS_UPDATE: 'os_update', PERIODIC: 'periodic' }
                    },
                    csi: function () { return { startE: Date.now(), onloadT: Date.now(), pageT: Math.random() * 5000, tran: 15 }; },
                    loadTimes: function () {
                        return {
                            requestTime: Date.now() / 1000, startLoadTime: Date.now() / 1000, commitLoadTime: Date.now() / 1000,
                            finishDocumentLoadTime: Date.now() / 1000, finishLoadTime: Date.now() / 1000,
                            firstPaintTime: 0, firstPaintAfterLoadTime: 0, navigationType: 'Other',
                            wasFetchedViaSpdy: false, wasNpnNegotiated: false, npnNegotiatedProtocol: 'http/1.1',
                            wasAlternateProtocolAvailable: false, connectionInfo: 'http/1.1'
                        };
                    }
                },
                writable: false, configurable: false
            });
        }
    } catch (e) { }

    // ©¤©¤ 8. Canvas fingerprint noise (critical for Turnstile click verification) ©¤
    // Turnstile reads canvas pixel data on click to fingerprint the renderer.
    // Injecting sub-pixel noise makes the Chromium canvas hash match real Chrome's range.
    try {
        const _origToDataURL = HTMLCanvasElement.prototype.toDataURL;
        HTMLCanvasElement.prototype.toDataURL = function (type, ...args) {
            const ctx2d = this.getContext('2d');
            if (ctx2d) {
                const imageData = ctx2d.getImageData(0, 0, this.width || 1, this.height || 1);
                // Flip a single LSB in two pixels ¨C invisible to eye, changes hash
                if (imageData.data.length > 7) {
                    imageData.data[0] ^= 1;
                    imageData.data[4] ^= 1;
                    ctx2d.putImageData(imageData, 0, 0);
                }
            }
            return _origToDataURL.call(this, type, ...args);
        };

        const _origGetImageData = CanvasRenderingContext2D.prototype.getImageData;
        CanvasRenderingContext2D.prototype.getImageData = function (x, y, w, h) {
            const imageData = _origGetImageData.call(this, x, y, w, h);
            if (imageData.data.length > 3) {
                imageData.data[0] ^= 1;
            }
            return imageData;
        };
    } catch (e) { }

    // ©¤©¤ 9. WebGL renderer/vendor (Turnstile reads UNMASKED strings) ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        const _origGetParam = WebGLRenderingContext.prototype.getParameter;
        WebGLRenderingContext.prototype.getParameter = function (param) {
            // UNMASKED_VENDOR_WEBGL = 0x9245, UNMASKED_RENDERER_WEBGL = 0x9246
            if (param === 0x9245) return 'Google Inc. (NVIDIA)';
            if (param === 0x9246) return 'ANGLE (NVIDIA, NVIDIA GeForce GTX 1060 6GB Direct3D11 vs_5_0 ps_5_0, D3D11)';
            return _origGetParam.call(this, param);
        };
    } catch (e) { }

    try {
        const _origGetParam2 = WebGL2RenderingContext.prototype.getParameter;
        WebGL2RenderingContext.prototype.getParameter = function (param) {
            if (param === 0x9245) return 'Google Inc. (NVIDIA)';
            if (param === 0x9246) return 'ANGLE (NVIDIA, NVIDIA GeForce GTX 1060 6GB Direct3D11 vs_5_0 ps_5_0, D3D11)';
            return _origGetParam2.call(this, param);
        };
    } catch (e) { }

    // ©¤©¤ 10. AudioContext fingerprint noise ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        const _origCreateOscillator = AudioContext.prototype.createOscillator;
        AudioContext.prototype.createOscillator = function () {
            const osc = _origCreateOscillator.call(this);
            const _origConnect = osc.connect.bind(osc);
            osc.connect = function (dest, ...rest) {
                // Tiny inaudible gain jitter
                const gain = this.context.createGain();
                gain.gain.value = 1 + Math.random() * 1e-7;
                _origConnect(gain);
                gain.connect(dest, ...rest);
                return gain;
            };
            return osc;
        };
    } catch (e) { }

    // ©¤©¤ 11. Network info ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        if (!navigator.connection) {
            Object.defineProperty(navigator, 'connection', {
                get: () => ({ rtt: 50, downlink: 10, effectiveType: '4g', saveData: false, type: 'wifi', onchange: null })
            });
        }
    } catch (e) { }

    // ©¤©¤ 12. iframe webdriver propagation ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    try {
        const _origCreateElement = Document.prototype.createElement;
        Document.prototype.createElement = function (tagName) {
            const el = _origCreateElement.call(this, tagName);
            if (typeof tagName === 'string' && tagName.toLowerCase() === 'iframe') {
                try { Object.defineProperty(el, 'webdriver', { get: () => undefined }); } catch (e) { }
            }
            return el;
        };
    } catch (e) { }

    // ©¤©¤ 13. Function.prototype.toString ¨C hide all overrides (must be last) ©¤©¤©¤©¤©¤
    // Placed after every other patch so the Set captures all replaced prototype
    // methods: canvas, WebGL, WebGL2, AudioContext, permissions, createElement.
    try {
        const _origFnToString = Function.prototype.toString;
        const _patched = new Set();
        const _reg = (...fns) => fns.forEach(f => { try { if (typeof f === 'function') _patched.add(f); } catch (e) { } });
        _reg(
            HTMLCanvasElement.prototype.toDataURL,
            CanvasRenderingContext2D.prototype.getImageData,
            WebGLRenderingContext.prototype.getParameter,
            WebGL2RenderingContext.prototype.getParameter,
            window.navigator.permissions.__proto__.query,
            AudioContext.prototype.createOscillator,
            Document.prototype.createElement,
        );
        Function.prototype.toString = function () {
            if (_patched.has(this)) return `function ${this.name || ''}() { [native code] }`;
            return _origFnToString.call(this);
        };
    } catch (e) { }
})();