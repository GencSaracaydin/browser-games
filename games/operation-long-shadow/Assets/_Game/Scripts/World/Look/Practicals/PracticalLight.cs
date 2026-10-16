using LongShadow.Core;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.Stage;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// A practical light (GDD §9.5, G4; TECH_PLAN §7.14): emissive renderers (always, through a MaterialPropertyBlock on
    /// <c>_EmissionColor</c>) plus an optional realtime, unshadowed <see cref="Light"/> that <see cref="PracticalBudget"/>
    /// grants only to the nearest few (at most 6 per area, fading in/out over 0.25 s); every other practical stays
    /// emissive-only. It switches on by rule (<see cref="PracticalSwitch"/>), flickers (<see cref="FlickerMode"/>), dies
    /// with its destructible (by id, WP03 Destructible; checkpoint-safe through <see cref="Destructibles"/>), and saves its
    /// on/off state when it has an id and is event- or script-driven. Emissive materials must have <c>_EMISSION</c>
    /// enabled (the FX builder and LookValidator check it).
    /// </summary>
    [DisallowMultipleComponent]
    public class PracticalLight : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("Optional id (marker id style, e.g. \"B7.Fill1\"); needed for saving and scripts.")]
        private string _id;
        [SerializeField, Tooltip("Optional realtime light (unshadowed). Leave empty for emissive-only practicals.")]
        private Light _light;
        [SerializeField, Tooltip("Renderers whose emission follows the light (lamp heads, bulbs, signs).")]
        private Renderer[] _emissive = new Renderer[0];
        [SerializeField, ColorUsage(false, true), Tooltip("HDR emission at full output.")]
        private Color _emission = new Color(4f, 2.4f, 1.1f, 1f);
        [SerializeField, Tooltip("Realtime light intensity at full output (0 = read from the Light at start).")]
        private float _lightIntensity;
        [SerializeField] private PracticalSwitch _switch = PracticalSwitch.AlwaysOn;
        [SerializeField, Tooltip("Mission event that turns it on (PracticalSwitch.MissionEvent).")]
        private string _onEvent;
        [SerializeField, Tooltip("Mission event that turns it off.")]
        private string _offEvent;
        [SerializeField] private FlickerMode _flicker = FlickerMode.Steady;
        [SerializeField, Tooltip("Flicker seed (0 = derived from the position).")]
        private float _seed;
        [SerializeField, Tooltip("Subject to the realtime budget (false for floods, flares, fixed fills: they count as fixed lights).")]
        private bool _budgeted = true;
        [SerializeField, Tooltip("Budget priority (higher wins at equal distance).")]
        private float _priority = 1f;
        [SerializeField, Tooltip("Realtime light only within this distance of the player.")]
        private float _maxRealtimeDistance = 35f;
        [SerializeField, Tooltip("Destructible id (WP03 Destructible / IDestructible) that kills this light.")]
        private string _destructibleId;
        [SerializeField, Tooltip("Seconds for the output to follow switching (0 = instant).")]
        private float _switchFade = 0.1f;

        private MaterialPropertyBlock _mpb;
        private ServiceRef<ICheckpointService> _checkpoints;
        private Color _baseLightColor = Color.white;
        private Color _lastEmission = new Color(-1f, 0f, 0f, 0f);
        private float _level;
        private float _realtimeWeight;
        private bool _budgetAllowed;
        private bool _on;
        private bool _destroyed;
        private bool _registeredSave;
        private bool _subscribed;
        private bool _baseCaptured;

        /// <summary>Id (may be empty).</summary>
        public string Id => _id;
        /// <summary>Realtime light (may be null).</summary>
        public Light RealtimeLight => _light;
        /// <summary>True while switched on (before flicker and destruction).</summary>
        public bool IsOn => _on;
        /// <summary>True after its destructible died.</summary>
        public bool IsDestroyed => _destroyed;
        /// <summary>Subject to the budget.</summary>
        public bool Budgeted => _budgeted;
        /// <summary>Budget priority.</summary>
        public float Priority => _priority;
        /// <summary>Realtime cut-off distance.</summary>
        public float MaxRealtimeDistance => _maxRealtimeDistance;
        /// <summary>Brightness 0.. including flicker.</summary>
        public float Output { get; private set; }
        /// <summary>True when the realtime light would be visible (on, alive, has a light).</summary>
        public bool WantsRealtime => _light != null && !_destroyed && _level > 0.02f;
        /// <summary>True while the realtime light is enabled.</summary>
        public bool RealtimeActive => _light != null && _light.enabled;

        /// <inheritdoc />
        public string SaveKey => "Practical:" + _id;

        /// <summary>Flicker seed in use.</summary>
        protected float Seed => _seed;
        /// <summary>Switch rule.</summary>
        protected PracticalSwitch Switch => _switch;

        /// <summary>Called by subclasses and builders.</summary>
        protected virtual void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (string.IsNullOrEmpty(_id))
            {
                // Bound under a level marker (PracticalBinder or Level's builder): take its id for saving and scripts.
                var marker = GetComponentInParent<SceneMarker>();
                if (marker != null && !string.IsNullOrEmpty(marker.Id)) _id = marker.Id;
            }
            if (Mathf.Approximately(_seed, 0f))
            {
                var p = transform.position;
                _seed = Mathf.Abs(p.x * 0.731f + p.z * 1.379f + p.y * 0.17f) % 97f + 1f;
            }
            if (_light != null)
            {
                CaptureBase();
                _light.shadows = LightShadows.None;
                _light.enabled = false;
            }
        }

        private void CaptureBase()
        {
            if (_baseCaptured || _light == null) return;
            _baseCaptured = true;
            _baseLightColor = _light.color;
            if (_lightIntensity <= 0f) _lightIntensity = _light.intensity;
        }

        /// <summary>Registers with the budget and events.</summary>
        protected virtual void OnEnable()
        {
            PracticalBudget.Register(this);
            if (!_subscribed)
            {
                _subscribed = true;
                GameEvents.MissionEvent += OnMissionEvent;
                GameEvents.LightStageChanged += OnStageChanged;
                GameEvents.DestructibleDestroyed += OnDestructibleDestroyed;
                GameEvents.CheckpointLoaded += OnCheckpointLoaded;
            }
            SyncDestroyed();
            EvaluateRule(snap: true);
        }

        /// <summary>Unregisters.</summary>
        protected virtual void OnDisable()
        {
            PracticalBudget.Unregister(this);
            if (_subscribed)
            {
                _subscribed = false;
                GameEvents.MissionEvent -= OnMissionEvent;
                GameEvents.LightStageChanged -= OnStageChanged;
                GameEvents.DestructibleDestroyed -= OnDestructibleDestroyed;
                GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            }
            if (_registeredSave) _checkpoints.Value?.Unregister(this);
            _registeredSave = false;
            _budgetAllowed = false;
            _realtimeWeight = 0f;
            if (_light != null) _light.enabled = false;
        }

        /// <summary>Ticks output, emission and the realtime light.</summary>
        protected virtual void Update()
        {
            if (!_registeredSave && NeedsSave()) TryRegisterSave();

            var dt = Time.deltaTime;
            var target = TargetLevel();
            _level = _switchFade > 0f ? Mathf.MoveTowards(_level, target, dt / _switchFade) : target;
            var flick = _level > 0f ? Flicker(Time.time) : 0f;
            Output = _level * flick;

            // Budget fade (0.25 s) so granted/revoked lights never pop.
            var wantRealtime = _budgeted ? _budgetAllowed : true;
            _realtimeWeight = Mathf.MoveTowards(_realtimeWeight, wantRealtime && WantsRealtime ? 1f : 0f, dt * 4f);

            ApplyOutput();
        }

        /// <summary>Turns the practical on or off (scripts; Manual and MissionEvent rules).</summary>
        public void SetOn(bool on) => _on = on;

        /// <summary>
        /// Editor previews and LookCapture (no Update in edit mode): shows the practical at <paramref name="level01"/>
        /// with its realtime light on (budget ignored) and the warm colour.
        /// </summary>
        public void PreviewLevel(float level01)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            CaptureBase();
            Output = Mathf.Max(0f, level01);
            _realtimeWeight = Output > 0f ? 1f : 0f;
            _lastEmission = new Color(-1f, 0f, 0f, 0f);
            ApplyOutput();
        }

        /// <summary>Snaps the output to the current target (no fade), e.g. after a checkpoint.</summary>
        public void Snap() => _level = TargetLevel();

        /// <summary>Budget decision (called by <see cref="PracticalBudget"/>).</summary>
        internal void SetBudgetAllowed(bool allowed) => _budgetAllowed = allowed;

        /// <summary>Target level 0..1 before flicker.</summary>
        protected virtual float TargetLevel() => _on && !_destroyed ? 1f : 0f;

        /// <summary>Colour factor (sodium warm-up overrides).</summary>
        protected virtual Color ColorFactor() => Color.white;

        /// <summary>Flicker factor for the mode.</summary>
        protected virtual float Flicker(float t)
        {
            switch (_flicker)
            {
                case FlickerMode.Fire: return FlickerNoise.Fire(t, _seed);
                case FlickerMode.Fluorescent: return FlickerNoise.Fluorescent(t, _seed);
                case FlickerMode.Faulty: return FlickerNoise.Faulty(t, _seed);
                case FlickerMode.Flare: return FlickerNoise.Flare(t, _seed);
                case FlickerMode.Pulse1Hz: return 0.15f + 0.85f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI));
                default: return 1f;
            }
        }

        /// <summary>Re-evaluates the switch rule (stage/event changes).</summary>
        protected virtual void EvaluateRule(bool snap)
        {
            var s = LightStageController.Active != null ? LightStageController.Active.S : 0f;
            switch (_switch)
            {
                case PracticalSwitch.AlwaysOn: _on = true; break;
                case PracticalSwitch.AfterStrike: _on = s >= LightStageTable.StrikeS; break;
                case PracticalSwitch.AfterSunset: _on = s >= LightStageTable.SunsetS; break;
            }
            if (snap) Snap();
        }

        private void ApplyOutput()
        {
            var factor = ColorFactor();
            if (_emissive != null && _emissive.Length > 0)
            {
                var e = _emission * factor * Output;
                if (Mathf.Abs(e.r - _lastEmission.r) + Mathf.Abs(e.g - _lastEmission.g) + Mathf.Abs(e.b - _lastEmission.b) > 1e-3f)
                {
                    _lastEmission = e;
                    for (var i = 0; i < _emissive.Length; i++)
                    {
                        var r = _emissive[i];
                        if (r == null) continue;
                        r.GetPropertyBlock(_mpb);
                        _mpb.SetColor(LookShaderIds.EmissionColor, e);
                        r.SetPropertyBlock(_mpb);
                    }
                }
            }
            if (_light == null) return;
            var w = _realtimeWeight * Output;
            var on = w > 1e-3f;
            if (_light.enabled != on) _light.enabled = on;
            if (!on) return;
            _light.intensity = _lightIntensity * w;
            _light.color = _baseLightColor * factor;
        }

        private void OnMissionEvent(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_switch == PracticalSwitch.MissionEvent || _switch == PracticalSwitch.Manual)
            {
                if (id == _onEvent) _on = true;
                else if (id == _offEvent) _on = false;
            }
        }

        private void OnStageChanged(float s) => EvaluateRule(snap: false);

        private void OnDestructibleDestroyed(string id)
        {
            if (!string.IsNullOrEmpty(_destructibleId) && id == _destructibleId) _destroyed = true;
        }

        private void OnCheckpointLoaded(string cp)
        {
            SyncDestroyed();
            EvaluateRule(snap: true);
        }

        private void SyncDestroyed()
        {
            if (string.IsNullOrEmpty(_destructibleId)) return;
            if (Destructibles.TryGet(_destructibleId, out var d)) _destroyed = d != null && d.IsDestroyed;
        }

        private bool NeedsSave() =>
            !string.IsNullOrEmpty(_id) && (_switch == PracticalSwitch.MissionEvent || _switch == PracticalSwitch.Manual);

        private void TryRegisterSave()
        {
            var cp = _checkpoints.Value;
            if (cp == null) return;
            cp.Register(this);
            _registeredSave = true;
        }

        /// <inheritdoc />
        public void Capture(SaveWriter w) => w.Write("on", _on);

        /// <inheritdoc />
        public void Restore(SaveReader r)
        {
            _on = r.ReadBool("on", _on);
            SyncDestroyed();
            Snap();
        }

#if UNITY_EDITOR
        /// <summary>Editor builders: configures the practical.</summary>
        public void EditorConfigure(string id, Light light, Renderer[] emissive, Color hdrEmission, float lightIntensity,
            PracticalSwitch rule, FlickerMode flicker, bool budgeted, string onEvent = null, string offEvent = null,
            string destructibleId = null, float priority = 1f)
        {
            _id = id;
            _light = light;
            _emissive = emissive ?? new Renderer[0];
            _emission = hdrEmission;
            _lightIntensity = lightIntensity;
            _switch = rule;
            _flicker = flicker;
            _budgeted = budgeted;
            _onEvent = onEvent;
            _offEvent = offEvent;
            _destructibleId = destructibleId;
            _priority = priority;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Emissive renderers (validator).</summary>
        public Renderer[] EditorEmissive => _emissive;
#endif
    }
}
