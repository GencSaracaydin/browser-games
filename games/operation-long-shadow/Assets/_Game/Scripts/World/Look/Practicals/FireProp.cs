using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// A fire that lights on a mission event (PROC_FIRE_PROP: the burning technical at <c>B4.FireProp</c> on
    /// <c>B4.TechnicalIgnited</c>, WP04 request; the S8_Heli copy at <c>B11.FireProp</c> starts lit): flipbook flames and
    /// smoke particle systems plus a fire-flicker <see cref="PracticalLight"/> (budgeted like any near practical, GDD §9.5).
    /// Saves its lit state at checkpoints.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FireProp : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("Save id (marker id, e.g. B4.FireProp). Empty = the parent marker's id.")]
        private string _id;
        [SerializeField, Tooltip("Mission event that ignites it (empty = only scripts/startLit).")]
        private string _igniteEvent = "B4.TechnicalIgnited";
        [SerializeField] private bool _startLit;
        [SerializeField, Tooltip("Flames, embers, smoke.")]
        private ParticleSystem[] _particles = new ParticleSystem[0];
        [SerializeField, Tooltip("Fire light (FlickerMode.Fire, PracticalSwitch.Manual).")]
        private PracticalLight _light;

        private ServiceRef<ICheckpointService> _checkpoints;
        private bool _lit;
        private bool _registered;

        /// <summary>True while burning.</summary>
        public bool IsLit => _lit;

        /// <inheritdoc />
        public string SaveKey => "Fire:" + _id;

        private void Awake()
        {
            if (string.IsNullOrEmpty(_id))
            {
                var marker = GetComponentInParent<SceneMarker>();
                if (marker != null && !string.IsNullOrEmpty(marker.Id)) _id = marker.Id;
            }
            Apply(_startLit, true);
        }

        private void OnEnable() => GameEvents.MissionEvent += OnMissionEvent;

        private void OnDisable()
        {
            GameEvents.MissionEvent -= OnMissionEvent;
            if (_registered) _checkpoints.Value?.Unregister(this);
            _registered = false;
        }

        private void Update()
        {
            if (_registered || string.IsNullOrEmpty(_id)) return;
            var cp = _checkpoints.Value;
            if (cp == null) return;
            cp.Register(this);
            _registered = true;
        }

        /// <summary>Lights or puts out the fire.</summary>
        public void SetLit(bool lit) => Apply(lit, false);

        private void OnMissionEvent(string id)
        {
            if (!string.IsNullOrEmpty(_igniteEvent) && id == _igniteEvent) Apply(true, false);
        }

        private void Apply(bool lit, bool force)
        {
            if (!force && lit == _lit) return;
            _lit = lit;
            foreach (var ps in _particles)
            {
                if (ps == null) continue;
                if (lit) ps.Play(true);
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_light != null) _light.SetOn(lit);
        }

        /// <inheritdoc />
        public void Capture(SaveWriter w) => w.Write("lit", _lit);

        /// <inheritdoc />
        public void Restore(SaveReader r)
        {
            Apply(r.ReadBool("lit", _lit), true);
            if (_light != null) _light.Snap();
        }

#if UNITY_EDITOR
        /// <summary>Editor builders.</summary>
        public void EditorSetup(string id, string igniteEvent, bool startLit, ParticleSystem[] particles, PracticalLight light)
        {
            _id = id;
            _igniteEvent = igniteEvent;
            _startLit = startLit;
            _particles = particles ?? new ParticleSystem[0];
            _light = light;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
