using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Cinematics.FX
{
    /// <summary>
    /// <see cref="IScreenFX"/> (TECH_PLAN §7.12 ScreenFXService): the FX volumes of §3.7 — VP_Death (saturation −100 over
    /// 0.6 s), VP_LowHealth (desaturation below 50 % HP), VP_Suppression (vignette pulse), VP_Flash (+EV white-outs) — as
    /// global volumes on layer Default whose weights follow the requests, the "Glitch" renderer feature (overlay static as
    /// fallback), glare, and fades to black or white on the overlay canvas. Cinematic clips drive the same layers through
    /// the <c>Cinematic*</c> channel setters. Everything runs on unscaled time.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Cinematics + 60)]
    [DisallowMultipleComponent]
    public sealed class ScreenFXService : MonoBehaviour, IScreenFX
    {
        /// <summary>Priority of the FX volumes (above every look and lens volume).</summary>
        public const float FxPriority = 100f;

        private Volume _death;
        private Volume _lowHealth;
        private Volume _suppression;
        private Volume _flash;
        private Volume _cineDesat;
        private ColorAdjustments _flashAdjust;
        private GlitchPass _glitch;

        private float _lowHealthAmount;
        private float _deathT;
        private float _suppressAmount;
        private float _suppressAge = -1f;
        private float _flashEv;
        private float _flashSeconds;
        private float _flashAge = -1f;
        private float _glitchIntensity;
        private float _glitchSeconds;
        private float _glitchAge = -1f;
        private float _glare;

        private Color _scriptFadeColor = Color.black;
        private float _scriptFadeFrom;
        private float _scriptFadeTo;
        private float _scriptFadeSeconds;
        private float _scriptFadeAge = -1f;
        private float _scriptFade;

        private Color _cineFadeColor = Color.black;
        private float _cineFade;
        private float _cineExposure;
        private float _cineDesatAmount;
        private float _cineGlitch;
        private float _cineWhiteFrame;
        private float _cineBurn;
        private Texture _cineFeed;
        private float _cineFeedAlpha;
        private float _cineVignette;

        private CinematicOverlay Overlay => CinematicRuntime.Overlay;

        /// <inheritdoc/>
        public bool IsBlack => Overlay != null && Overlay.IsBlack;

        /// <summary>Current death desaturation 0..1 (set through <see cref="Death"/>).</summary>
        public float DeathAmount => _deathT;

        private void Awake()
        {
            CinematicRuntime.Set(this);
            GameServices.Register<IScreenFX>(this);
            var lib = CinematicLibrary.Current;
            _death = Create(lib, CinematicAssetIds.ProfileDeath, BuildDeath);
            _lowHealth = Create(lib, CinematicAssetIds.ProfileLowHealth, BuildLowHealth);
            _suppression = Create(lib, CinematicAssetIds.ProfileSuppression, BuildSuppression);
            _flash = Create(lib, CinematicAssetIds.ProfileFlash, BuildFlash);
            _cineDesat = Create(null, "VP_CinematicDesat", p => p.Add<ColorAdjustments>(false).saturation.Override(-100f));
            _flashAdjust = VolumeFactory.Get<ColorAdjustments>(_flash);
            _flashAdjust.postExposure.overrideState = true;
            _glitch = new GlitchPass(lib != null ? lib.Find<Material>(CinematicAssetIds.MatGlitch) : null);
        }

        private void OnDestroy()
        {
            GameServices.Unregister<IScreenFX>(this);
            CinematicRuntime.Clear(this);
            _glitch?.Restore();
            VolumeFactory.Destroy(_death);
            VolumeFactory.Destroy(_lowHealth);
            VolumeFactory.Destroy(_suppression);
            VolumeFactory.Destroy(_flash);
            VolumeFactory.Destroy(_cineDesat);
        }

        // ------------------------------------------------------------------ IScreenFX

        /// <inheritdoc/>
        public void SetLowHealth(float amount01) => _lowHealthAmount = Mathf.Clamp01(amount01);

        /// <inheritdoc/>
        public void PulseSuppression(float amount01)
        {
            var current = _suppressAge >= 0f ? FxEnvelope.Pulse(_suppressAmount, _suppressAge) : 0f;
            _suppressAmount = Mathf.Clamp01(Mathf.Max(amount01, current));
            _suppressAge = 0f;
        }

        /// <inheritdoc/>
        public void Death(float t01) => _deathT = Mathf.Clamp01(t01);

        /// <inheritdoc/>
        public void Flash(float ev, float seconds)
        {
            _flashEv = ev;
            _flashSeconds = Mathf.Max(0.01f, seconds);
            _flashAge = 0f;
        }

        /// <inheritdoc/>
        public void Glitch(float intensity, float seconds)
        {
            _glitchIntensity = Mathf.Clamp01(intensity);
            _glitchSeconds = Mathf.Max(0.02f, seconds);
            _glitchAge = 0f;
        }

        /// <inheritdoc/>
        public void Glare(float amount01) => _glare = Mathf.Clamp01(amount01);

        /// <inheritdoc/>
        public void FadeToBlack(float seconds) => StartFade(Color.black, 1f, seconds);

        /// <inheritdoc/>
        public void FadeIn(float seconds) => StartFade(_scriptFadeColor, 0f, seconds);

        /// <summary>Fades to white over seconds (the climax cut to white).</summary>
        public void FadeToWhite(float seconds) => StartFade(Color.white, 1f, seconds);

        /// <summary>Holds a fully covered screen in a colour until <see cref="FadeIn"/> (end screens).</summary>
        public void Hold(Color color)
        {
            _scriptFadeColor = color;
            _scriptFade = 1f;
            _scriptFadeAge = -1f;
        }

        /// <summary>Clears every script fade at once.</summary>
        public void ClearFade()
        {
            _scriptFade = 0f;
            _scriptFadeAge = -1f;
        }

        // ------------------------------------------------------------------ cinematic channels (ScreenFX clips)

        /// <summary>Clip-driven fade (max-combined with script fades).</summary>
        public void CinematicFade(Color color, float alpha)
        {
            _cineFadeColor = color;
            _cineFade = Mathf.Clamp01(alpha);
        }

        /// <summary>Clip-driven exposure offset in EV (intro shot 9 white-out landing).</summary>
        public void CinematicExposure(float ev) => _cineExposure = ev;

        /// <summary>Clip-driven desaturation 0..1 (1 = monochrome).</summary>
        public void CinematicDesaturate(float amount01) => _cineDesatAmount = Mathf.Clamp01(amount01);

        /// <summary>Clip-driven glitch amount and white frame.</summary>
        public void CinematicGlitch(float intensity, float white)
        {
            _cineGlitch = Mathf.Clamp01(intensity);
            _cineWhiteFrame = Mathf.Clamp01(white);
        }

        /// <summary>Clip-driven film burn-out 0..1.</summary>
        public void CinematicBurn(float t01) => _cineBurn = Mathf.Clamp01(t01);

        /// <summary>Clip-driven camera feed cross-dissolve.</summary>
        public void CinematicFeed(Texture texture, float alpha)
        {
            _cineFeed = texture;
            _cineFeedAlpha = Mathf.Clamp01(alpha);
        }

        /// <summary>Clip-driven vignette tightening 0..1 (MID-A digits, dread moments).</summary>
        public void CinematicVignette(float amount01) => _cineVignette = Mathf.Clamp01(amount01);

        /// <summary>Resets every cinematic channel.</summary>
        public void ClearCinematicChannels()
        {
            _cineFade = 0f;
            _cineExposure = 0f;
            _cineDesatAmount = 0f;
            _cineGlitch = 0f;
            _cineWhiteFrame = 0f;
            _cineBurn = 0f;
            _cineFeed = null;
            _cineFeedAlpha = 0f;
            _cineVignette = 0f;
        }

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            var dt = Time.unscaledDeltaTime;

            // Script fade.
            if (_scriptFadeAge >= 0f)
            {
                _scriptFadeAge += dt;
                _scriptFade = FxEnvelope.Fade(_scriptFadeFrom, _scriptFadeTo, _scriptFadeSeconds, _scriptFadeAge);
                if (_scriptFadeAge >= _scriptFadeSeconds) _scriptFadeAge = -1f;
            }

            // Flash: weight follows the envelope; the profile holds the peak.
            var flashEv = 0f;
            if (_flashAge >= 0f)
            {
                _flashAge += dt;
                flashEv = FxEnvelope.Flash(_flashEv, _flashSeconds, _flashAge);
                if (_flashAge >= _flashSeconds) _flashAge = -1f;
            }
            var ev = flashEv + _cineExposure;
            if (_flash != null)
            {
                var on = Mathf.Abs(ev) > 0.001f;
                _flash.weight = on ? 1f : 0f;
                if (on) _flashAdjust.postExposure.value = ev;
            }

            // Suppression pulse (+ cinematic vignette).
            var pulse = 0f;
            if (_suppressAge >= 0f)
            {
                _suppressAge += dt;
                pulse = FxEnvelope.Pulse(_suppressAmount, _suppressAge);
                if (_suppressAge >= FxEnvelope.SuppressionSeconds) _suppressAge = -1f;
            }
            if (_suppression != null) _suppression.weight = Mathf.Clamp01(Mathf.Max(pulse, _cineVignette));
            if (_lowHealth != null) _lowHealth.weight = _lowHealthAmount;
            if (_death != null) _death.weight = _deathT;
            if (_cineDesat != null) _cineDesat.weight = _cineDesatAmount;

            // Glitch.
            var g = 0f;
            if (_glitchAge >= 0f)
            {
                _glitchAge += dt;
                g = FxEnvelope.Glitch(_glitchIntensity, _glitchSeconds, _glitchAge);
                if (_glitchAge >= _glitchSeconds) _glitchAge = -1f;
            }
            g = Mathf.Max(g, _cineGlitch);
            var white = _cineWhiteFrame;
            var pass = _glitch != null && _glitch.Available;
            if (pass) _glitch.Set(g, white);

            var o = Overlay;
            if (o == null) return;
            if (!pass) o.SetStatic(g * 0.85f, g, white);
            else o.SetStatic(0f, 0f, 0f);
            o.SetGlare(_glare);
            o.SetBurn(_cineBurn);
            o.SetFeed(_cineFeed, _cineFeedAlpha);
            if (_cineFade >= _scriptFade) o.SetFade(_cineFadeColor, _cineFade);
            else o.SetFade(_scriptFadeColor, _scriptFade);
        }

        private void StartFade(Color color, float to, float seconds)
        {
            _scriptFadeFrom = _scriptFade;
            _scriptFadeTo = to;
            if (to > 0f) _scriptFadeColor = color;
            _scriptFadeSeconds = Mathf.Max(0f, seconds);
            _scriptFadeAge = 0f;
            if (_scriptFadeSeconds <= 0f)
            {
                _scriptFade = to;
                _scriptFadeAge = -1f;
            }
        }

        private Volume Create(CinematicLibrary lib, string id, System.Action<VolumeProfile> fallback)
        {
            var template = lib != null ? lib.Find<VolumeProfile>(id) : null;
            var v = VolumeFactory.Create(transform, id, LayerIds.Default, FxPriority, template);
            if (template == null) fallback(v.sharedProfile);
            return v;
        }

        /// <summary>VP_Death: saturation −100, a little contrast (TECH_PLAN §3.7).</summary>
        public static void BuildDeath(VolumeProfile p)
        {
            var ca = p.Add<ColorAdjustments>(false);
            ca.saturation.Override(-100f);
            ca.contrast.Override(18f);
            var v = p.Add<Vignette>(false);
            v.intensity.Override(0.45f);
        }

        /// <summary>VP_LowHealth: desaturation and a tighter vignette (GDD §11 below 50 % HP).</summary>
        public static void BuildLowHealth(VolumeProfile p)
        {
            var ca = p.Add<ColorAdjustments>(false);
            ca.saturation.Override(FxEnvelope.LowHealthSaturation);
            var v = p.Add<Vignette>(false);
            v.intensity.Override(0.25f + FxEnvelope.LowHealthVignette);
            v.color.Override(new Color(0.12f, 0.02f, 0.02f));
        }

        /// <summary>VP_Suppression: vignette pulse with a touch of chromatic aberration.</summary>
        public static void BuildSuppression(VolumeProfile p)
        {
            var v = p.Add<Vignette>(false);
            v.intensity.Override(0.25f + FxEnvelope.SuppressionVignette);
            v.smoothness.Override(0.5f);
            var ch = p.Add<ChromaticAberration>(false);
            ch.intensity.Override(0.35f);
        }

        /// <summary>VP_Flash: post exposure (value set per frame).</summary>
        public static void BuildFlash(VolumeProfile p)
        {
            var ca = p.Add<ColorAdjustments>(false);
            ca.postExposure.Override(6f);
        }
    }
}
