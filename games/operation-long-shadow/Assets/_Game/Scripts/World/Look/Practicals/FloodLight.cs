using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Look.Common;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// A Relay Yard floodlight F1–F4 (GDD §7.6, §9.5, G4; Appendix A <c>B6.F1..F4</c>): a realtime, unshadowed spot aimed at
    /// (345, 0, 325) plus an emissive lens, dark until the mission event <c>B6.FloodsOn</c>, then an arc "slam" (flash,
    /// sputter, settle over 0.6 s, staggered per flood). While lit it keeps its <see cref="IlluminationZone"/> (owner id
    /// <c>B6.Fx</c>: AI sight ×2 and the ×1.25 accuracy bonus) active and feeds <see cref="FloodGlare"/>. Shooting the head
    /// is WP03's <c>Destructible</c> (same id): on <see cref="GameEvents.DestructibleDestroyed"/> the zone and glare stop and
    /// this component stops driving the light (the Destructible plays the buzzing die-off). Counted as a fixed light by
    /// <see cref="PracticalBudget"/>; saves its on state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloodLight : MonoBehaviour, ISaveable
    {
        private static readonly List<FloodLight> s_all = new List<FloodLight>(4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_all.Clear();

        /// <summary>Enabled floods.</summary>
        public static IReadOnlyList<FloodLight> All => s_all;

        [SerializeField, Tooltip("Appendix A id (B6.F1..F4); also the Destructible id. Empty = the parent marker's id.")]
        private string _id;
        [SerializeField] private Light _spot;
        [SerializeField] private Renderer[] _lens = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] private Color _lensEmission = new Color(30f, 28f, 24f, 1f);
        [SerializeField] private IlluminationZone _zone;
        [SerializeField, Tooltip("Spot intensity when lit (URP inverse-square units; ~50 m throw).")]
        private float _intensity = 900f;
        [SerializeField, Tooltip("Mission event that switches the floods on.")]
        private string _onEvent = LookIds.FloodsOn;
        [SerializeField, Tooltip("Delay after the event (staggers F1..F4).")]
        private float _delay;
        [SerializeField] private bool _startOn;
        [SerializeField] private string _slamCue = LookIds.CueFloodSlamOn;

        private MaterialPropertyBlock _mpb;
        private ServiceRef<ICheckpointService> _checkpoints;
        private ServiceRef<IAudioService> _audio;
        private bool _on;
        private float _onAt = float.PositiveInfinity;
        private bool _destroyed;
        private bool _registeredSave;
        private bool _soundPending;
        private float _lastLevel = -1f;

        /// <summary>Id.</summary>
        public string Id => _id;
        /// <summary>The spot.</summary>
        public Light Spot => _spot;
        /// <summary>True while lit and alive.</summary>
        public bool IsLit => _on && !_destroyed && Time.time >= _onAt;
        /// <summary>True after the head was shot out.</summary>
        public bool IsDestroyed => _destroyed;
        /// <summary>Brightness 0..1.3 (slam overshoot).</summary>
        public float Level { get; private set; }

        /// <inheritdoc />
        public string SaveKey => "Flood:" + _id;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (string.IsNullOrEmpty(_id))
            {
                var marker = GetComponentInParent<SceneMarker>();
                if (marker != null && !string.IsNullOrEmpty(marker.Id)) _id = marker.Id;
            }
            if (_zone != null && !string.IsNullOrEmpty(_id) && _zone.OwnerId != _id)
                _zone.Configure(_zone.Shape, _zone.Range, _zone.ConeAngle, _id); // WP04 reads the "B6.F" owner prefix
            if (_spot != null)
            {
                _spot.shadows = LightShadows.None; // G4: destructible practicals are unshadowed
                _spot.enabled = false;
            }
            _on = _startOn;
            if (_on) _onAt = -10f;
        }

        private void OnEnable()
        {
            if (!s_all.Contains(this)) s_all.Add(this);
            PracticalBudget.RegisterFixed(_spot);
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.DestructibleDestroyed += OnDestroyedEvent;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
            SyncDestroyed();
        }

        private void OnDisable()
        {
            s_all.Remove(this);
            PracticalBudget.UnregisterFixed(_spot);
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.DestructibleDestroyed -= OnDestroyedEvent;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            if (_registeredSave) _checkpoints.Value?.Unregister(this);
            _registeredSave = false;
        }

        /// <summary>Switches the flood on (with the slam) or off.</summary>
        public void SetOn(bool on, bool instant = false)
        {
            if (on == _on && !instant) return;
            _on = on;
            _onAt = on ? (instant ? Time.time - 10f : Time.time + _delay) : float.PositiveInfinity;
            _soundPending = on && !instant;
        }

        /// <summary>Slam curve: arc flash and sputter, settling to 1 by 0.6 s.</summary>
        public static float SlamCurve(float since, float seed)
        {
            if (since < 0f) return 0f;
            if (since < 0.05f) return 1.35f * (since / 0.05f);
            if (since < 0.6f)
            {
                var x = (since - 0.05f) / 0.55f;
                var settle = Mathf.Lerp(1.35f, 1f, x * x * (3f - 2f * x));
                return settle * FlickerNoise.StrikeSputter(since - 0.05f, seed);
            }
            return 1f;
        }

        private void Update()
        {
            if (!_registeredSave && !string.IsNullOrEmpty(_id)) TryRegisterSave();
            if (_destroyed)
            {
                if (_zone != null && _zone.IsActive) _zone.SetActive(false);
                Level = 0f;
                return; // the Destructible owns the dying light
            }

            var since = Time.time - _onAt;
            Level = _on ? SlamCurve(since, _id != null ? _id.Length * 3.1f : 1f) : 0f;
            if (_soundPending && since >= 0f)
            {
                _soundPending = false;
                if (!string.IsNullOrEmpty(_slamCue)) _audio.Value?.Play(_slamCue, transform.position, 1f);
            }

            if (_zone != null)
            {
                var active = Level > 0.5f;
                if (_zone.IsActive != active) _zone.SetActive(active);
            }
            if (Mathf.Abs(Level - _lastLevel) < 1e-3f) return;
            _lastLevel = Level;
            if (_spot != null)
            {
                var on = Level > 1e-3f;
                if (_spot.enabled != on) _spot.enabled = on;
                _spot.intensity = _intensity * Level;
            }
            var e = _lensEmission * Level;
            for (var i = 0; i < _lens.Length; i++)
            {
                var r = _lens[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(LookShaderIds.EmissionColor, e);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void OnMissionEvent(string id)
        {
            if (!string.IsNullOrEmpty(_onEvent) && id == _onEvent) SetOn(true);
        }

        private void OnDestroyedEvent(string id)
        {
            if (id == _id) _destroyed = true;
        }

        private void OnCheckpointLoaded(string cp) => SyncDestroyed();

        private void SyncDestroyed()
        {
            if (string.IsNullOrEmpty(_id)) return;
            if (Destructibles.TryGet(_id, out var d) && d != null)
            {
                var was = _destroyed;
                _destroyed = d.IsDestroyed;
                if (was && !_destroyed) _lastLevel = -1f; // restored intact: drive the light again
            }
        }

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
            SetOn(r.ReadBool("on", _on), instant: true);
            SyncDestroyed();
            _lastLevel = -1f;
        }

#if UNITY_EDITOR
        /// <summary>Editor builders.</summary>
        public void EditorSetup(string id, Light spot, Renderer[] lens, IlluminationZone zone, float delay)
        {
            _id = id;
            _spot = spot;
            _lens = lens ?? new Renderer[0];
            _zone = zone;
            _delay = delay;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
