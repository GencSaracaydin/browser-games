using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Menus;
using LongShadow.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// The HUD and its menus (TECH_PLAN §7.13, GDD §11): implements <see cref="IHud"/>, builds every view at runtime from
    /// a <see cref="UITheme"/> (so WP08 ships no hand-made UI prefabs), and ticks them all from one LateUpdate with
    /// unscaled time. Lives in S_Persistent as PF_HUD (LongShadow/UI/Build HUD Prefab). Hides the gameplay HUD during
    /// cinematics, glitch inserts and the debrief, and on <see cref="SetVisible"/>(false); subtitles stay up.
    /// Every service is optional: the HUD runs alone in test scenes and degrades when a system is missing.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.UI)]
    [DisallowMultipleComponent]
    public sealed class HudRoot : MonoBehaviour, IHud
    {
        [SerializeField, Tooltip("Theme asset (Assets/_Game/UI/Themes/UI_Theme.asset). Empty = procedural sprites and the TMP default font.")]
        private UITheme _theme;
        [SerializeField, Tooltip("Canvas sorting order (the click gate in S_Boot draws above at 100).")]
        private int _sortingOrder = 10;
        [SerializeField, Tooltip("Register as IHud on Awake.")]
        private bool _registerService = true;

        private HudContext _ctx;
        private readonly List<HudView> _views = new List<HudView>(32);
        private CanvasGroup _gameplay;
        private Canvas _canvas;
        private Vector2 _lastCanvasSize;
        private bool _explicitVisible = true;
        private int _cinematics;
        private int _inserts;
        private float _gameplayAlpha = 1f;
        private bool _enabledViews;

        private System.Action<string> _onCinematicStarted, _onCinematicEnded, _onInsertStarted, _onInsertEnded, _onCheckpointLoaded;
        private System.Action<float> _onLightStage;

        // Views (internal for tests and the test bed).
        internal HealthVignette Vignette;
        internal OverlayBinoculars Binoculars;
        internal OverlayDoorGun DoorGun;
        internal ObjectiveMarkers Markers;
        internal SquadNameplates Nameplates;
        internal WorldRings Rings;
        internal DetectionMeters Detection;
        internal CompassStrip Compass;
        internal ObjectiveText Objectives;
        internal Crosshair Crosshair;
        internal HitMarkers Hits;
        internal DamageIndicator Damage;
        internal GrenadeIndicator Grenades;
        internal AmmoPanel Ammo;
        internal PromptView Prompt;
        internal TutorialPrompts Tutorials;
        internal IntelCardView Intel;
        internal SubtitleView Subtitles;
        internal LetterboxBars Letterboxes;
        internal TitleCardView Title;
        internal BannerView BannerView;
        internal FailCardView Fail;
        internal DebriefScreen Debrief;
        internal PauseMenu Pause;
        internal OptionsMenu Options;
        internal FadeScreen Fade;

        /// <summary>The shared HUD state (null before <see cref="Initialize"/>).</summary>
        public HudContext Context => _ctx;

        /// <summary>Every view in tick order.</summary>
        public IReadOnlyList<HudView> Views => _views;

        /// <summary>True while the gameplay HUD is shown.</summary>
        public bool GameplayVisible => _ctx != null && _ctx.GameplayVisible;

        /// <summary>Theme in use.</summary>
        public UITheme Theme => _theme;

        private void Awake()
        {
            Initialize(_theme);
            if (_registerService) GameServices.Register<IHud>(this);
            UiEventSystem.Ensure();
        }

        private void OnEnable()
        {
            if (_ctx == null) return;
            EnableViews();
        }

        private void OnDisable() => DisableViews();

        private void OnDestroy()
        {
            DisableViews();
            if (_registerService) GameServices.Unregister<IHud>(this);
        }

        /// <summary>
        /// Builds the canvas and every view (idempotent). Called by Awake; tests and editor tools call it directly
        /// because Awake does not run in edit mode.
        /// </summary>
        public void Initialize(UITheme theme)
        {
            if (_ctx != null) return;
            _theme = theme;
            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            canvasGo.layer = 5;
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _sortingOrder;
            _canvas.pixelPerfect = false;
            _canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudMath.Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = HudMath.Match;
            var root = (RectTransform)canvasGo.transform;
            // Edit mode (tests, layout audit) has no canvas update: start at the reference size. In play mode the canvas
            // drives its own size and the views relayout when it changes.
            if (!Application.isPlaying || root.rect.width < 1f) root.sizeDelta = HudMath.Reference;
            if (!Application.isPlaying)
            {
                // The (ExecuteAlways) scaler would size the canvas from the editor's game view; edit-mode users lay the
                // canvas out explicitly (ForceLayout) in reference units at scale 1.
                scaler.enabled = false;
                _canvas.scaleFactor = 1f;
            }

            _ctx = new HudContext(theme, _canvas);
            _ctx.BeginFrame(0f);
            Build(root);
            _lastCanvasSize = _ctx.CanvasSize;
            if (isActiveAndEnabled && Application.isPlaying) EnableViews();
        }

        /// <summary>
        /// Subscribes (or unsubscribes) the HUD and its views to <see cref="GameEvents"/>. Automatic in play mode
        /// (OnEnable/OnDisable); edit-mode tests and tools call it explicitly and must turn it off before destroying the HUD.
        /// </summary>
        public void SetEventsEnabled(bool on)
        {
            if (on) EnableViews();
            else DisableViews();
        }

        private void Build(RectTransform root)
        {
            Vignette = Add(new HealthVignette(), root);

            var gameplay = UIFactory.Stretch("Gameplay", root);
            _gameplay = UIFactory.Group(gameplay.gameObject);
            Binoculars = Add(new OverlayBinoculars(), gameplay);
            DoorGun = Add(new OverlayDoorGun(), gameplay);
            var world = UIFactory.Stretch("World", gameplay);
            UIFactory.SubCanvas(world.gameObject, false);
            Markers = Add(new ObjectiveMarkers(), world);
            Nameplates = Add(new SquadNameplates(), world);
            Rings = Add(new WorldRings(), world);
            Detection = Add(new DetectionMeters(), world);
            Compass = Add(new CompassStrip(), gameplay);
            Objectives = Add(new ObjectiveText(), gameplay);
            var reticle = UIFactory.Rect("Reticle", gameplay, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(10f, 10f));
            UIFactory.SubCanvas(reticle.gameObject, false);
            Crosshair = Add(new Crosshair(), reticle);
            Hits = Add(new HitMarkers(), reticle);
            Damage = Add(new DamageIndicator(), reticle);
            Grenades = Add(new GrenadeIndicator(), reticle);
            Ammo = Add(new AmmoPanel(), gameplay);
            Prompt = Add(new PromptView(), gameplay);
            Tutorials = Add(new TutorialPrompts(), gameplay);
            Intel = Add(new IntelCardView(), gameplay);

            Subtitles = Add(new SubtitleView(), root);
            var cinematic = UIFactory.Stretch("Cinematic", root);
            Letterboxes = Add(new LetterboxBars(), cinematic);
            Title = Add(new TitleCardView(Letterboxes), cinematic);
            BannerView = Add(new BannerView(), root);
            Fail = Add(new FailCardView(), root);

            var debrief = UIFactory.Stretch("DebriefLayer", root);
            UIFactory.SubCanvas(debrief.gameObject, true);
            Debrief = Add(new DebriefScreen(), debrief);

            var menus = UIFactory.Stretch("Menus", root);
            UIFactory.SubCanvas(menus.gameObject, true);
            Options = new OptionsMenu();
            Pause = Add(new PauseMenu(Options), menus);
            Add(Options, menus);

            Fade = Add(new FadeScreen(), root);
        }

        private T Add<T>(T view, RectTransform parent) where T : HudView
        {
            view.Build(_ctx, parent);
            _views.Add(view);
            return view;
        }

        private void EnableViews()
        {
            if (_enabledViews || _ctx == null) return;
            _enabledViews = true;
            _onCinematicStarted ??= _ => { _cinematics++; };
            _onCinematicEnded ??= _ => { _cinematics = Mathf.Max(0, _cinematics - 1); };
            _onInsertStarted ??= _ => { _inserts++; };
            _onInsertEnded ??= _ => { _inserts = Mathf.Max(0, _inserts - 1); };
            _onCheckpointLoaded ??= OnCheckpointLoaded;
            _onLightStage ??= s => { if (_ctx != null) _ctx.StageS = s; };
            GameEvents.CinematicStarted += _onCinematicStarted;
            GameEvents.CinematicEnded += _onCinematicEnded;
            GameEvents.InsertStarted += _onInsertStarted;
            GameEvents.InsertEnded += _onInsertEnded;
            GameEvents.CheckpointLoaded += _onCheckpointLoaded;
            GameEvents.LightStageChanged += _onLightStage;
            for (var i = 0; i < _views.Count; i++) _views[i].Enable();
        }

        private void DisableViews()
        {
            if (!_enabledViews) return;
            _enabledViews = false;
            GameEvents.CinematicStarted -= _onCinematicStarted;
            GameEvents.CinematicEnded -= _onCinematicEnded;
            GameEvents.InsertStarted -= _onInsertStarted;
            GameEvents.InsertEnded -= _onInsertEnded;
            GameEvents.CheckpointLoaded -= _onCheckpointLoaded;
            GameEvents.LightStageChanged -= _onLightStage;
            for (var i = 0; i < _views.Count; i++) _views[i].Disable();
        }

        private void OnCheckpointLoaded(string cp)
        {
            // Transient feedback does not survive a reload; objectives and markers rebuild from the service.
            Damage.ResetState();
            Grenades.ResetState();
            Hits.ResetState();
            BannerView.ResetState();
            Intel.ResetState();
            Title.ResetState();
            Objectives.ResetState();
            Rings.ResetState();
        }

        private void LateUpdate() => TickAll(Time.unscaledDeltaTime);

        /// <summary>Ticks every view once (LateUpdate; tests call it directly). Allocation-free.</summary>
        public void TickAll(float unscaledDt)
        {
            if (_ctx == null) return;
            _ctx.BeginFrame(unscaledDt);
            var size = _ctx.CanvasSize;
            if ((size - _lastCanvasSize).sqrMagnitude > 1f)
            {
                _lastCanvasSize = size;
                for (var i = 0; i < _views.Count; i++) _views[i].Relayout(size);
            }
            var visible = _explicitVisible && _cinematics == 0 && _inserts == 0 && !Debrief.IsOpen;
            _ctx.GameplayVisible = visible;
            _gameplayAlpha = UIAnim.Approach(_gameplayAlpha, visible ? 1f : 0f, visible ? 4f : 8f, unscaledDt);
            _gameplay.alpha = _gameplayAlpha * (_ctx.Theme != null ? _ctx.Theme.HudOpacity : 1f);
            for (var i = 0; i < _views.Count; i++) _views[i].Tick(unscaledDt);
        }

        /// <summary>Static layout regions for the overlap audit (GDD §11 "no overlap").</summary>
        public void CollectLayout(List<HudLayoutRegion> regions)
        {
            if (_ctx == null) return;
            for (var i = 0; i < _views.Count; i++) _views[i].CollectLayout(regions);
        }

        /// <summary>Re-applies layout for a canvas size (layout audit in edit mode).</summary>
        public void ForceLayout(Vector2 canvasSize)
        {
            if (_ctx == null) return;
            ((RectTransform)_canvas.transform).sizeDelta = canvasSize;
            _ctx.BeginFrame(0f);
            _lastCanvasSize = canvasSize;
            for (var i = 0; i < _views.Count; i++) _views[i].Relayout(canvasSize);
        }

        // ---------------------------------------------------------------- IHud

        /// <inheritdoc/>
        public void ShowPrompt(PromptData prompt) => Prompt?.Show(prompt);

        /// <inheritdoc/>
        public void HidePrompt(string id) => Prompt?.Hide(id);

        /// <inheritdoc/>
        public void Tutorial(string key, string text) => Tutorials?.Show(key, text);

        /// <inheritdoc/>
        public void IntelCard(string title, Sprite photo, string counter) => Intel?.Show(title, photo, counter);

        /// <inheritdoc/>
        public void FailCard(string text) => Fail?.Show(text);

        /// <inheritdoc/>
        public void Banner(string text, float seconds) => BannerView?.Show(text, seconds);

        /// <inheritdoc/>
        public void SetOverlay(HudOverlay overlay)
        {
            if (_ctx != null) _ctx.Overlay = overlay;
        }

        /// <inheritdoc/>
        public void SetRing(string id, Transform target, float value01, string label) => Rings?.Set(id, target, value01, label);

        /// <inheritdoc/>
        public void ClearRing(string id) => Rings?.Clear(id);

        /// <inheritdoc/>
        public void Letterbox(bool on, float seconds) => Letterboxes?.Set(on, seconds);

        /// <inheritdoc/>
        public void SetVisible(bool visible) => _explicitVisible = visible;

        /// <inheritdoc/>
        public void TitleCard(float seconds) => Title?.Play(seconds);

        /// <inheritdoc/>
        public void ShowDebrief(MissionStats stats)
        {
            if (Debrief == null) return;
            Pause?.Close();
            Debrief.Open(stats ?? GameServices.Get<IMissionDirector>()?.Stats);
        }
    }
}
