// LongShadowWeb.jslib - browser services for OPERATION LONG SHADOW (TECH_PLAN §7.1, webgl.md §4.8, §6.5).
// C# side: Assets/_Game/Scripts/Platform/WebNative.cs (every import is behind UNITY_WEBGL && !UNITY_EDITOR).
// Rules: nothing here throws into the engine (every entry point catches), nothing calls alert(), callbacks go to the
// "WebBridge" GameObject through SendMessage. The CovertOps template exposes window.LSPage (HTML loader / click gate);
// every page call is optional so the plugin also works under another template.

var LongShadowWebLib = {
  $LSWeb: {
    bridge: 'WebBridge',
    inited: false,
    armed: 0,            // 1 = pointer lock, 2 = fullscreen, 4 = resume audio (run on the next pointerdown/keydown)
    unadjusted: true,

    canvas: function () {
      try {
        if (typeof Module !== 'undefined' && Module['canvas']) return Module['canvas'];
      } catch (e) {}
      return document.getElementById('unity-canvas') || document.querySelector('canvas');
    },

    page: function () {
      try { return (typeof window !== 'undefined' && window.LSPage) ? window.LSPage : null; } catch (e) { return null; }
    },

    send: function (method, value) {
      try {
        if (typeof Module !== 'undefined' && typeof Module['SendMessage'] === 'function') Module['SendMessage'](LSWeb.bridge, method, String(value));
        else if (typeof SendMessage === 'function') SendMessage(LSWeb.bridge, method, String(value));
      } catch (e) {
        if (typeof console !== 'undefined') console.warn('[LS] SendMessage ' + method + ' failed', e);
      }
    },

    str: function (s) {
      if (s === null || s === undefined) return 0;
      s = String(s);
      var n = lengthBytesUTF8(s) + 1;
      var p = _malloc(n);
      stringToUTF8(s, p, n);
      return p;
    },

    locked: function () {
      var c = LSWeb.canvas();
      return !!(c && (document.pointerLockElement === c || document.mozPointerLockElement === c));
    },

    isFullscreen: function () {
      return !!(document.fullscreenElement || document.webkitFullscreenElement);
    },

    errorName: function (e) {
      if (!e) return 'Error';
      return (e.name || 'Error') + (e.message ? ': ' + e.message : '');
    },

    lockFailed: function (e) {
      LSWeb.send('OnPointerLockError', LSWeb.errorName(e));
    },

    // Raw mouse first (Chrome's unadjustedMovement); plain lock when the browser does not support it. Every promise
    // rejection is caught: an unhandled one would reach the loader's error handler (webgl.md §3.6).
    lock: function (unadjusted) {
      var c = LSWeb.canvas();
      if (!c || !c.requestPointerLock) return 0;
      try {
        var p = unadjusted ? c.requestPointerLock({ unadjustedMovement: true }) : c.requestPointerLock();
        if (p && typeof p.then === 'function') {
          p.then(null, function (err) {
            if (unadjusted && err && err.name === 'NotSupportedError') {
              try {
                var p2 = c.requestPointerLock();
                if (p2 && typeof p2.then === 'function') p2.then(null, LSWeb.lockFailed);
              } catch (e2) { LSWeb.lockFailed(e2); }
            } else {
              LSWeb.lockFailed(err);
            }
          });
        }
        return 1;
      } catch (e) {
        LSWeb.lockFailed(e);
        return 0;
      }
    },

    fullscreen: function () {
      try {
        var el = document.getElementById('ls-stage') || (LSWeb.canvas() && LSWeb.canvas().parentElement) || LSWeb.canvas();
        if (!el) return 0;
        var fn = el.requestFullscreen || el.webkitRequestFullscreen;
        if (!fn) return 0;
        var p = fn.call(el);
        if (p && typeof p.then === 'function') p.then(null, function (e) {
          if (typeof console !== 'undefined') console.warn('[LS] fullscreen refused: ' + LSWeb.errorName(e));
        });
        return 1;
      } catch (e) {
        return 0;
      }
    },

    resumeAudio: function () {
      try {
        if (typeof WEBAudio !== 'undefined' && WEBAudio && WEBAudio.audioContext && WEBAudio.audioContext.state !== 'running') {
          var p = WEBAudio.audioContext.resume();
          if (p && typeof p.then === 'function') p.then(null, function () {});
        }
      } catch (e) {}
    },

    onGesture: function (e) {
      var a = LSWeb.armed;
      if (!a) return;
      if (e && e.type === 'keydown' && (e.key === 'Escape' || e.keyCode === 27)) return;
      if (a & 4) LSWeb.resumeAudio();
      if ((a & 2) && !LSWeb.isFullscreen()) LSWeb.fullscreen();
      if ((a & 1) && !LSWeb.locked()) LSWeb.lock(LSWeb.unadjusted);
      LSWeb.armed = a & 1; // the lock stays armed until granted; audio and fullscreen are one-shot
    }
  },

  LS_Init: function (bridgePtr) {
    try {
      var name = UTF8ToString(bridgePtr);
      if (name) LSWeb.bridge = name;
      if (LSWeb.inited) return;
      LSWeb.inited = true;
      var onLock = function () {
        var l = LSWeb.locked();
        if (l) LSWeb.armed = LSWeb.armed & ~1;
        LSWeb.send('OnPointerLockChanged', l ? '1' : '0');
      };
      document.addEventListener('pointerlockchange', onLock, false);
      document.addEventListener('mozpointerlockchange', onLock, false);
      document.addEventListener('pointerlockerror', function () { LSWeb.send('OnPointerLockError', 'pointerlockerror'); }, false);
      document.addEventListener('visibilitychange', function () { LSWeb.send('OnVisibilityChanged', document.hidden ? '0' : '1'); }, false);
      var onFs = function () { LSWeb.send('OnFullscreenChanged', LSWeb.isFullscreen() ? '1' : '0'); };
      document.addEventListener('fullscreenchange', onFs, false);
      document.addEventListener('webkitfullscreenchange', onFs, false);
      window.addEventListener('pointerdown', LSWeb.onGesture, true);
      window.addEventListener('keydown', LSWeb.onGesture, true);
      var page = LSWeb.page();
      if (page) {
        page.unitySend = function (method, value) { LSWeb.send(method, value === undefined ? '' : value); };
        page.resumeAudio = LSWeb.resumeAudio;
        page.requestLock = function () { return LSWeb.lock(LSWeb.unadjusted); };
        page.requestFullscreen = LSWeb.fullscreen;
        if (typeof page.onEngineReady === 'function') page.onEngineReady();
      }
    } catch (e) {
      if (typeof console !== 'undefined') console.warn('[LS] LS_Init failed', e);
    }
  },

  LS_GetDevicePixelRatio: function () {
    try {
      var d = (typeof Module !== 'undefined' && Module['devicePixelRatio']) || window.devicePixelRatio || 1;
      return d > 0 ? d : 1;
    } catch (e) { return 1; }
  },

  LS_RequestPointerLock: function (unadjusted) {
    LSWeb.unadjusted = !!unadjusted;
    return LSWeb.lock(!!unadjusted);
  },

  LS_ExitPointerLock: function () {
    try {
      LSWeb.armed = LSWeb.armed & ~1;
      if (document.exitPointerLock) document.exitPointerLock();
    } catch (e) {}
  },

  LS_IsPointerLocked: function () {
    try { return LSWeb.locked() ? 1 : 0; } catch (e) { return 0; }
  },

  LS_PointerLockSupported: function () {
    try {
      var c = LSWeb.canvas();
      return c && c.requestPointerLock ? 1 : 0;
    } catch (e) { return 0; }
  },

  LS_ArmGesture: function (flags, unadjusted) {
    LSWeb.armed = flags | 0;
    LSWeb.unadjusted = !!unadjusted;
  },

  LS_RequestFullscreen: function () {
    return LSWeb.fullscreen();
  },

  LS_ExitFullscreen: function () {
    try {
      var fn = document.exitFullscreen || document.webkitExitFullscreen;
      if (fn && LSWeb.isFullscreen()) {
        var p = fn.call(document);
        if (p && typeof p.then === 'function') p.then(null, function () {});
      }
    } catch (e) {}
  },

  LS_IsFullscreen: function () {
    try { return LSWeb.isFullscreen() ? 1 : 0; } catch (e) { return 0; }
  },

  LS_ResumeAudio: function () {
    LSWeb.resumeAudio();
  },

  LS_StorageGet: function (keyPtr) {
    try {
      var v = window.localStorage.getItem(UTF8ToString(keyPtr));
      return v === null ? 0 : LSWeb.str(v);
    } catch (e) { return 0; }
  },

  LS_StorageSet: function (keyPtr, valuePtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
      return 1;
    } catch (e) { return 0; }
  },

  LS_StorageRemove: function (keyPtr) {
    try {
      window.localStorage.removeItem(UTF8ToString(keyPtr));
      return 1;
    } catch (e) { return 0; }
  },

  LS_StorageAvailable: function () {
    try {
      var k = '__ls_probe__';
      window.localStorage.setItem(k, '1');
      var ok = window.localStorage.getItem(k) === '1';
      window.localStorage.removeItem(k);
      return ok ? 1 : 0;
    } catch (e) { return 0; }
  },

  LS_IsInIframe: function () {
    try { return window.self !== window.top ? 1 : 0; } catch (e) { return 1; }
  },

  // name "" returns the raw "?query#hash" text; otherwise the value of one key (hash wins over query), or null.
  LS_GetQuery: function (namePtr) {
    try {
      var name = UTF8ToString(namePtr);
      var search = window.location.search || '';
      var hash = window.location.hash || '';
      if (!name) return (search || hash) ? LSWeb.str(search + hash) : 0;
      var read = function (text) {
        if (!text) return null;
        try { return new URLSearchParams(text.replace(/^[?#]/, '')).get(name); } catch (e) { return null; }
      };
      var v = read(hash);
      if (v === null) v = read(search);
      return v === null ? 0 : LSWeb.str(v);
    } catch (e) { return 0; }
  },

  LS_GetUserAgent: function () {
    try { return LSWeb.str(navigator.userAgent || ''); } catch (e) { return 0; }
  },

  LS_SetSmokeResult: function (jsonPtr) {
    try {
      var s = UTF8ToString(jsonPtr);
      try { window.LS_SMOKE_RESULT = JSON.parse(s); } catch (e) { window.LS_SMOKE_RESULT = s; }
    } catch (e) {}
  },

  LS_GetAssetSuffix: function () {
    try { return window.LS_ASSET_SUFFIX ? LSWeb.str(window.LS_ASSET_SUFFIX) : 0; } catch (e) { return 0; }
  },

  LS_GetServedExtensions: function () {
    try { return window.LS_SERVED_EXT ? LSWeb.str(window.LS_SERVED_EXT) : 0; } catch (e) { return 0; }
  },

  LS_GetBuildVariant: function () {
    try { return window.LS_VARIANT ? LSWeb.str(window.LS_VARIANT) : 0; } catch (e) { return 0; }
  },

  LS_LoaderProgress: function (progress, labelPtr) {
    try {
      var p = LSWeb.page();
      if (p && p.setBootProgress) p.setBootProgress(progress, UTF8ToString(labelPtr));
    } catch (e) {}
  },

  LS_LoaderReady: function (flags) {
    try {
      var p = LSWeb.page();
      if (p && p.setReady) p.setReady(flags | 0);
    } catch (e) {}
  },

  LS_LoaderVisible: function (visible) {
    try {
      var p = LSWeb.page();
      if (p && p.setVisible) p.setVisible(!!visible);
    } catch (e) {}
  },

  LS_LoaderError: function (messagePtr) {
    try {
      var p = LSWeb.page();
      if (p && p.fatal) p.fatal(UTF8ToString(messagePtr));
    } catch (e) {}
  },

  LS_GetHeapBytes: function () {
    try {
      if (typeof wasmMemory !== 'undefined' && wasmMemory && wasmMemory.buffer) return wasmMemory.buffer.byteLength;
      if (typeof HEAPU8 !== 'undefined' && HEAPU8) return HEAPU8.length;
    } catch (e) {}
    return 0;
  }
};

autoAddDeps(LongShadowWebLib, '$LSWeb');
mergeInto(LibraryManager.library, LongShadowWebLib);
