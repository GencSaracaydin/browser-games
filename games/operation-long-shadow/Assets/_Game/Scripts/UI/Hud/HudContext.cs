using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Shared state of one HUD instance: theme, fonts, canvas, UI audio and cached service handles (all optional: every
    /// view degrades when a service is missing, TECH_PLAN §5.3). Per-frame values (camera, canvas scale, marker colour,
    /// ADS blend) are sampled once in <see cref="BeginFrame"/> so the views read plain fields.
    /// </summary>
    public sealed class HudContext
    {
        /// <summary>Theme the HUD was built from (never null; a runtime default when no asset is assigned).</summary>
        public readonly UITheme Theme;
        /// <summary>Resolved fonts.</summary>
        public readonly FontSet Fonts;
        /// <summary>Root canvas.</summary>
        public readonly Canvas Canvas;
        /// <summary>Root canvas rect (size in reference units).</summary>
        public readonly RectTransform CanvasRect;
        /// <summary>UI sounds.</summary>
        public readonly UIAudio Audio = new UIAudio();

        // Service handles (mutable structs: keep them as fields and read .Value).
        internal ServiceRef<IPlayerRig> Rig;
        internal ServiceRef<IWeaponHudState> Weapon;
        internal ServiceRef<IObjectiveService> Objectives;
        internal ServiceRef<ILightStage> Light;
        internal ServiceRef<ISettingsService> Settings;
        internal ServiceRef<IPlayerHealth> Health;
        internal ServiceRef<ISquadService> Squad;
        internal ServiceRef<IEncounterService> Encounters;
        internal ServiceRef<IInputReader> Input;
        internal ServiceRef<IPauseService> Pause;
        internal ServiceRef<IMissionDirector> Director;
        internal ServiceRef<ICheckpointService> Checkpoints;
        internal ServiceRef<IClickGate> Gate;
        internal ServiceRef<IMarkerRegistry> MarkerRegistry;
        internal ServiceRef<IWeaponService> Weapons;

        /// <summary>LightStage s from the last <see cref="GameEvents.LightStageChanged"/> (fallback marker palette).</summary>
        public float StageS;

        /// <summary>Camera used for projection this frame (rig camera, else Camera.main), may be null.</summary>
        public Camera Camera { get; private set; }
        /// <summary>Canvas scale factor this frame.</summary>
        public float ScaleFactor { get; private set; } = 1f;
        /// <summary>Canvas size in reference units this frame.</summary>
        public Vector2 CanvasSize { get; private set; } = HudMath.Reference;
        /// <summary>Objective marker colour this frame (GDD §11: amber before s 2.0, pale green after).</summary>
        public Color MarkerColor { get; private set; } = UIColors.MarkerAmber;
        /// <summary>ADS blend 0..1 this frame.</summary>
        public float AdsBlend { get; private set; }
        /// <summary>Player stance this frame.</summary>
        public Stance Stance { get; private set; }
        /// <summary>Active full-screen overlay (binoculars, door gun).</summary>
        public HudOverlay Overlay { get; internal set; }
        /// <summary>True while the gameplay HUD is shown (not hidden by a cinematic, an insert or SetVisible(false)).</summary>
        public bool GameplayVisible { get; internal set; } = true;
        /// <summary>Unscaled seconds since the HUD was built.</summary>
        public float Time { get; private set; }

        private Camera _mainCache;
        private float _mainCacheAge = 99f;

        /// <summary>Creates a context.</summary>
        public HudContext(UITheme theme, Canvas canvas)
        {
            Theme = theme != null ? theme : UITheme.CreateRuntimeDefault();
            Fonts = UIFonts.Resolve(Theme);
            Canvas = canvas;
            CanvasRect = canvas != null ? (RectTransform)canvas.transform : null;
        }

        /// <summary>Sprite for an id (theme art or procedural).</summary>
        public Sprite Sprite(UISpriteId id) => UISprites.Get(id, Theme);

        /// <summary>Samples the per-frame values. Called by HudRoot before ticking the views.</summary>
        public void BeginFrame(float unscaledDt)
        {
            Time += unscaledDt;
            if (Canvas != null)
            {
                ScaleFactor = Canvas.scaleFactor > 0f ? Canvas.scaleFactor : 1f;
                CanvasSize = CanvasRect.rect.size;
            }
            var rig = Rig.Value;
            Camera cam = rig != null ? rig.WorldCamera : null;
            if (cam == null || !cam.isActiveAndEnabled)
            {
                _mainCacheAge += unscaledDt;
                if (_mainCache == null || _mainCacheAge > 1f)
                {
                    _mainCache = UnityEngine.Camera.main;
                    _mainCacheAge = 0f;
                }
                cam = _mainCache;
            }
            Camera = cam;
            AdsBlend = rig != null ? Mathf.Clamp01(rig.AdsBlend) : 0f;
            Stance = rig != null ? rig.Stance : Stance.Stand;
            var light = Light.Value;
            MarkerColor = light != null ? light.HudColor : UIColors.MarkerPalette(StageS);
            if (MarkerColor.a <= 0f) MarkerColor = UIColors.MarkerPalette(StageS);
        }

        /// <summary>Eye transform of the player (rig eye, else the camera), may be null.</summary>
        public Transform Eye
        {
            get
            {
                var rig = Rig.Value;
                if (rig != null && rig.Eye != null) return rig.Eye;
                return Camera != null ? Camera.transform : null;
            }
        }

        /// <summary>Current options, or null when no settings service exists.</summary>
        public GameSettings CurrentSettings => Settings.Value?.Current;
    }
}
