using System.IO;
using LongShadow.Cinematics.FX;
using LongShadow.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// One booth CRT (GDD §8.0: six mismatched sets, 512 × 384 content through SG_CRTScreen with curvature, scanlines,
    /// flicker, phosphor tint, static and degauss wobble). Owns a material instance on the TV's screen-glass slot, a
    /// weak point light that throws the screen's glow into the room (CINEMATIC_DIRECTION §3: degauss flashes light the
    /// room), an optional burn-in text line (live TMP: "ZAROUD RELAY / 14 OCT 1984 1012Z / FRAME 0640", "26.10.84 18:31")
    /// and a VideoPlayer created on demand. Runs on unscaled time so the CRTs stay alive during inserts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CRTScreen : MonoBehaviour
    {
        /// <summary>Snap-on time of the picture (collapsed line to full frame).</summary>
        public const float PowerOnSeconds = 0.35f;
        /// <summary>Degauss wobble time.</summary>
        public const float DegaussSeconds = 1.4f;

        [SerializeField, Range(1, 6)] private int _index = 1;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private int _screenMaterialIndex = -1;
        [SerializeField, Tooltip("Centre of the glass, +Z toward the viewer (burn-in text anchor).")] private Transform _textAnchor;
        [SerializeField] private Vector2 _glassSize = new Vector2(0.36f, 0.27f);
        [SerializeField] private Light _glow;
        [SerializeField] private float _glowIntensity = 0.9f;
        [SerializeField] private Color _phosphor = new Color(0.86f, 1f, 0.92f);

        private Material _crt;
        private Material _gunCam;
        private Material _applied;
        private bool _usingGunCam;
        private CrtContent _content = CrtContent.Off;
        private Texture _texture;
        private string _feedId;
        private RenderTexture _feedTex;
        private string _flashFeedId;
        private RenderTexture _flashTex;
        private float _flashUntil;
        private Texture[] _slides;
        private float _slideSeconds = 1f;
        private float _slideClock;
        private int _slide;
        private VideoPlayer _video;
        private RenderTexture _videoRt;
        private string _videoFile;
        private bool _videoReady;
        private TextMeshPro _text;
        private float _power;
        private float _powerTarget;
        private float _powerOn = 1f;
        private float _degauss;
        private float _snapAt = -1f;
        private float _static;
        private float _brightness = 1.5f;
        private float _flash;
        private float _lightFlash;
        private Vector4 _uvRect = new Vector4(0f, 0f, 1f, 1f);
        private Vector2 _scroll;
        private float _scanBoost;
        private bool _paused;

        /// <summary>CRT number 1..6 (CRT1-3 bottom row).</summary>
        public int Index => _index;
        /// <summary>Current content.</summary>
        public CrtContent Content => _content;
        /// <summary>True while the tube is powered.</summary>
        public bool IsOn => _powerTarget > 0f;
        /// <summary>Estimated screen luminance 0..1 (drives the glow light and the needle-less room light).</summary>
        public float Luminance { get; private set; }

        private void Awake()
        {
            BuildMaterial();
            if (_glow != null) _glow.intensity = 0f;
            Apply();
        }

        private void OnEnable() => GameEvents.PauseChanged += OnPause;

        private void OnDisable()
        {
            GameEvents.PauseChanged -= OnPause;
            ReleaseFeeds();
        }

        private void OnDestroy()
        {
            ReleaseFeeds();
            if (_videoRt != null)
            {
                _videoRt.Release();
                Destroy(_videoRt);
            }
            if (_crt != null) Destroy(_crt);
            if (_gunCam != null) Destroy(_gunCam);
        }

        /// <summary>Builder wiring.</summary>
        public void Setup(int index, Renderer screenRenderer, int screenMaterialIndex, Transform textAnchor, Vector2 glassSize, Light glow)
        {
            _index = index;
            _renderer = screenRenderer;
            _screenMaterialIndex = screenMaterialIndex;
            _textAnchor = textAnchor;
            _glassSize = glassSize;
            _glow = glow;
        }

        // ------------------------------------------------------------------ content API

        /// <summary>Turns the tube off (picture collapses quickly).</summary>
        public void Off()
        {
            SetContent(CrtContent.Off);
            _powerTarget = 0f;
            _static = 0f;
        }

        /// <summary>Snow at an amount 0..1.</summary>
        public void ShowStatic(float amount = 1f)
        {
            SetContent(CrtContent.Static);
            _powerTarget = 1f;
            _static = Mathf.Clamp01(amount);
        }

        /// <summary>A still (null shows static). <paramref name="staticAmount"/> keeps a little snow over it.</summary>
        public void ShowTexture(Texture texture, float staticAmount = 0.06f)
        {
            if (texture == null)
            {
                ShowStatic();
                return;
            }
            SetContent(CrtContent.Still);
            _texture = texture;
            _powerTarget = 1f;
            _static = staticAmount;
        }

        /// <summary>A live feed by id (static while the feed is unavailable).</summary>
        public void ShowFeed(string feedId, float staticAmount = 0.05f)
        {
            if (_content == CrtContent.Feed && _feedId == feedId && _feedTex != null)
            {
                _static = staticAmount;
                return;
            }
            SetContent(CrtContent.Feed);
            _feedId = feedId;
            _feedTex = FeedCameras.Acquire(feedId);
            _powerTarget = 1f;
            _static = _feedTex != null ? staticAmount : 1f;
        }

        /// <summary>A StreamingAssets/video clip (static until it is prepared, then the clip).</summary>
        public void ShowVideo(string fileName, bool loop, bool gunCam, Texture fallbackStill = null)
        {
            SetContent(CrtContent.Video);
            _powerTarget = 1f;
            _static = 1f;
            _texture = fallbackStill;
            _usingGunCam = gunCam;
            StartVideo(fileName, loop);
        }

        /// <summary>A slideshow of stills, <paramref name="secondsEach"/> each.</summary>
        public void ShowSlides(Texture[] slides, float secondsEach)
        {
            if (slides == null || slides.Length == 0)
            {
                ShowStatic();
                return;
            }
            SetContent(CrtContent.Slides);
            _slides = slides;
            _slideSeconds = Mathf.Max(0.1f, secondsEach);
            _slide = 0;
            _slideClock = 0f;
            _powerTarget = 1f;
            _static = 0.05f;
        }

        /// <summary>
        /// Power-on with the snap and degauss of intro shot 4 ("CRT1-6 snap on 0.6 s apart... degauss thunk and high
        /// whine per set") after <paramref name="delay"/> seconds.
        /// </summary>
        public void SnapOn(float delay)
        {
            _power = 0f;
            _powerOn = 0f;
            _snapAt = Time.unscaledTime + Mathf.Max(0f, delay);
        }

        /// <summary>Shows a feed (the stopped clock) over the content for a moment (intro shot 5 clock flashes).</summary>
        public void FlashFeed(string feedId, float seconds)
        {
            if (_flashFeedId != feedId)
            {
                if (_flashFeedId != null) FeedCameras.Release(_flashFeedId);
                _flashFeedId = feedId;
                _flashTex = FeedCameras.Acquire(feedId);
            }
            _flashUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Burn-in text (null or empty hides it).</summary>
        public void SetOverlayText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (_text != null) _text.gameObject.SetActive(false);
                return;
            }
            EnsureText();
            if (_text == null) return;
            _text.gameObject.SetActive(true);
            _text.text = text;
        }

        /// <summary>Content UV window (x, y offset; z, w size) for zooms and pans (outro shot 4 roster push-in).</summary>
        public void SetUvRect(Vector4 rect) => _uvRect = rect;

        /// <summary>Scroll speed in UV/s (teletype roll).</summary>
        public void SetScroll(Vector2 uvPerSecond) => _scroll = uvPerSecond;

        /// <summary>Scanline thickening 0..1 (intro shot 8: "the scanlines thicken until they become the horizon").</summary>
        public void SetScanlineBoost(float amount01) => _scanBoost = Mathf.Clamp01(amount01);

        /// <summary>Picture brightness (emission multiplier, default 1.5).</summary>
        public void SetBrightness(float brightness) => _brightness = Mathf.Max(0f, brightness);

        /// <summary>White flash 0..1 (glitch frames).</summary>
        public void SetFlash(float amount01) => _flash = Mathf.Clamp01(amount01);

        /// <summary>Switches the gun-camera shader (green phosphor video tube, outro shot 3) on or off.</summary>
        public void SetGunCam(bool on) => _usingGunCam = on;

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var now = Time.unscaledTime;
            if (_snapAt >= 0f && now >= _snapAt)
            {
                _snapAt = -1f;
                _powerOn = 0.001f;
                _degauss = 1f;
                _lightFlash = 1f;
                CinematicAudio.Play(CinematicCues.CrtDegauss, transform.position);
            }
            if (_snapAt < 0f)
            {
                if (_powerOn < 1f) _powerOn = Mathf.Min(1f, _powerOn + dt / PowerOnSeconds);
                var rate = _powerTarget > _power ? 1f / 0.12f : 1f / 0.25f;
                _power = Mathf.MoveTowards(_power, _powerTarget, rate * dt);
            }
            if (_degauss > 0f) _degauss = Mathf.Max(0f, _degauss - dt / DegaussSeconds);
            if (_lightFlash > 0f) _lightFlash = Mathf.Max(0f, _lightFlash - dt * 2.5f);

            if (_content == CrtContent.Slides && _slides != null && !_paused)
            {
                _slideClock += dt;
                if (_slideClock >= _slideSeconds)
                {
                    _slideClock = 0f;
                    _slide = (_slide + 1) % _slides.Length;
                }
            }
            if (_flashFeedId != null && now >= _flashUntil)
            {
                FeedCameras.Release(_flashFeedId);
                _flashFeedId = null;
                _flashTex = null;
            }
            if (_content == CrtContent.Feed && _feedTex == null && FeedCameras.IsAvailable(_feedId))
            {
                _feedTex = FeedCameras.Acquire(_feedId);
                if (_feedTex != null) _static = 0.05f;
            }
            if (_content == CrtContent.Video && _videoReady && _static > 0.07f) _static = Mathf.MoveTowards(_static, 0.05f, dt * 4f);
            Apply();
        }

        private void Apply()
        {
            var mat = _usingGunCam && _gunCam != null ? _gunCam : _crt;
            if (mat == null) return;
            if (_renderer != null && _screenMaterialIndex >= 0 && _applied != mat)
            {
                var shared = _renderer.sharedMaterials;
                if (_screenMaterialIndex < shared.Length)
                {
                    shared[_screenMaterialIndex] = mat;
                    _renderer.sharedMaterials = shared;
                }
                _applied = mat;
            }
            var tex = CurrentTexture();
            mat.SetTexture(ShaderIds.ScreenTex, tex != null ? tex : Texture2D.blackTexture);
            mat.SetVector(ShaderIds.UVRect, _uvRect);
            mat.SetVector(ShaderIds.Scroll, new Vector4(_scroll.x, _scroll.y, 0f, 0f));
            mat.SetFloat(ShaderIds.Power, _power);
            mat.SetFloat(ShaderIds.PowerOn, _powerOn);
            var staticAmount = tex == null && _content != CrtContent.Off ? 1f : _static;
            mat.SetFloat(ShaderIds.Static, staticAmount);
            mat.SetFloat(ShaderIds.Brightness, _brightness);
            mat.SetFloat(ShaderIds.Degauss, _degauss);
            mat.SetFloat(ShaderIds.ScanlineBoost, _scanBoost);
            mat.SetFloat(ShaderIds.Flash, Mathf.Max(_flash, _lightFlash * 0.35f));
            mat.SetColor(ShaderIds.Tint, _phosphor);

            var lum = _power * (_content == CrtContent.Off ? 0f : Mathf.Lerp(0.38f, 0.6f, staticAmount)) * _powerOn;
            Luminance = Mathf.Clamp01(lum + _lightFlash * 0.8f + _flash);
            if (_glow != null)
            {
                var flicker = 0.94f + 0.06f * Mathf.PerlinNoise(Time.unscaledTime * 9f, _index * 3.1f);
                _glow.intensity = _glowIntensity * Luminance * flicker;
                _glow.enabled = _glow.intensity > 0.005f;
            }
        }

        private Texture CurrentTexture()
        {
            if (_flashFeedId != null && _flashTex != null) return _flashTex;
            switch (_content)
            {
                case CrtContent.Still: return _texture;
                case CrtContent.Feed: return _feedTex;
                case CrtContent.Video: return _videoReady ? _videoRt : _texture;
                case CrtContent.Slides: return _slides != null && _slides.Length > 0 ? _slides[_slide] : null;
                default: return null;
            }
        }

        private void SetContent(CrtContent c)
        {
            if (_content == CrtContent.Feed && c != CrtContent.Feed && _feedId != null)
            {
                FeedCameras.Release(_feedId);
                _feedId = null;
                _feedTex = null;
            }
            if (_content == CrtContent.Video && c != CrtContent.Video) StopVideo();
            if (c != CrtContent.Video) _usingGunCam = false;
            _content = c;
            _scroll = Vector2.zero;
            _uvRect = new Vector4(0f, 0f, 1f, 1f);
        }

        private void ReleaseFeeds()
        {
            if (_feedId != null) FeedCameras.Release(_feedId);
            if (_flashFeedId != null) FeedCameras.Release(_flashFeedId);
            _feedId = null;
            _feedTex = null;
            _flashFeedId = null;
            _flashTex = null;
        }

        // ------------------------------------------------------------------ video (TECH_PLAN D14: VideoPlayer.url)

        private void StartVideo(string fileName, bool loop)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            if (_video == null)
            {
                _video = gameObject.AddComponent<VideoPlayer>();
                _video.playOnAwake = false;
                _video.source = VideoSource.Url;
                _video.renderMode = VideoRenderMode.RenderTexture;
                _video.audioOutputMode = VideoAudioOutputMode.None;
                _video.skipOnDrop = true;
                _video.waitForFirstFrame = true;
                _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                _video.prepareCompleted += OnVideoPrepared;
                _video.errorReceived += OnVideoError;
            }
            if (_videoRt == null)
            {
                _videoRt = new RenderTexture(FeedCamera.Width, FeedCamera.Height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "RT_CRT" + _index + "_Video",
                    useMipMap = false,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _videoRt.Create();
            }
            _video.targetTexture = _videoRt;
            _video.isLooping = loop;
            _videoReady = false;
            _videoFile = fileName;
            _video.url = VideoUrl(fileName);
            _video.Prepare();
        }

        private void StopVideo()
        {
            _videoReady = false;
            _videoFile = null;
            if (_video != null && (_video.isPlaying || _video.isPrepared)) _video.Stop();
        }

        private void OnVideoPrepared(VideoPlayer vp)
        {
            if (_content != CrtContent.Video || _videoFile == null) return;
            _videoReady = true;
            if (!_paused) vp.Play();
        }

        private void OnVideoError(VideoPlayer vp, string message)
        {
            LSLog.WarnOnce("crt-video:" + _videoFile, LogCategory.Cinematics,
                "CRT" + _index + " video '" + _videoFile + "' failed (" + message + "); showing the fallback still.", this);
            _videoReady = false;
            _static = _texture != null ? 0.08f : 1f;
        }

        /// <summary>StreamingAssets URL of a video file (VideoPlayer.url, TECH_PLAN D14).</summary>
        public static string VideoUrl(string fileName) =>
            Path.Combine(Application.streamingAssetsPath, "video", fileName).Replace('\\', '/');

        private void OnPause(bool paused)
        {
            _paused = paused;
            if (_video == null || !_videoReady) return;
            if (paused) _video.Pause();
            else _video.Play();
        }

        // ------------------------------------------------------------------ helpers

        private void BuildMaterial()
        {
            var lib = CinematicLibrary.Current;
            var template = lib != null ? lib.Find<Material>(CinematicAssetIds.MatCrt) : null;
            if (template == null)
            {
                var shader = Shader.Find(ShaderIds.CrtShader);
                if (shader != null) template = new Material(shader);
                else LSLog.MissingAsset(LogCategory.Cinematics, ShaderIds.CrtShader, "Assets/_Game/Art/Shaders/SG_CRTScreen.shader", this);
            }
            if (template != null)
            {
                _crt = new Material(template) { name = "M_CRT" + _index + "_Runtime" };
                _crt.SetFloat(ShaderIds.Seed, _index * 17.31f);
            }
            var gunTemplate = lib != null ? lib.Find<Material>(CinematicAssetIds.MatGunCam) : null;
            if (gunTemplate == null)
            {
                var shader = Shader.Find(ShaderIds.GunCamShader);
                if (shader != null) gunTemplate = new Material(shader);
            }
            if (gunTemplate != null) _gunCam = new Material(gunTemplate) { name = "M_CRT" + _index + "_GunCam_Runtime" };
        }

        private void EnsureText()
        {
            if (_text != null) return;
            var font = CinematicFonts.Get(CinematicAssetIds.FontMono);
            if (font == null)
            {
                LSLog.WarnOnce("crt-font", LogCategory.Cinematics, "No TMP font for CRT burn-in text; text hidden.", this);
                return;
            }
            var go = new GameObject("BurnIn");
            go.layer = gameObject.layer;
            var anchor = _textAnchor != null ? _textAnchor : transform;
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, -_glassSize.y * 0.36f, 0.004f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _text = go.AddComponent<TextMeshPro>();
            _text.font = font;
            _text.fontSize = 0.12f;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.alignment = TextAlignmentOptions.Bottom;
            _text.color = new Color(0.8f, 1f, 0.82f, 0.9f);
            _text.rectTransform.sizeDelta = new Vector2(_glassSize.x * 0.92f, _glassSize.y * 0.25f);
        }
    }
}
