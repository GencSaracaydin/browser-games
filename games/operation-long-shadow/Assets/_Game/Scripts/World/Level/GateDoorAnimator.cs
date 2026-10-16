using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// Swings door or gate leaves when their <see cref="GateMarker"/> opens or closes (GDD §7.2 "the gate creaks open on the
    /// Long Market", §6.7 "the Weighbridge gate swings shut", the postern closing behind the player, the Hall double door
    /// kicked open). The GateMarker owns the blocking colliders; this component only animates the hinged leaves (yaw
    /// about each hinge's local up axis) with an ease-out and a creak. Snaps instead of animating on checkpoint loads.
    /// </summary>
    public sealed class GateDoorAnimator : MonoBehaviour
    {
        [SerializeField, Tooltip("GateMarker id this follows (\"Gate.Weighbridge\").")]
        private string _gateId;
        [SerializeField, Tooltip("Hinge transforms (leaf children rotate with them).")]
        private Transform[] _hinges = new Transform[0];
        [SerializeField, Tooltip("Open yaw per hinge in degrees (relative to closed).")]
        private float[] _openYaw = new float[0];
        [SerializeField] private float _seconds = 1.6f;
        [SerializeField] private string _openCue = "SFX_GATE_CREAK";
        [SerializeField] private string _closeCue = "SFX_GATE_CREAK";
        [SerializeField, Tooltip("A kicked door slams open fast (Hall double door).")]
        private bool _kick;

        private Quaternion[] _closed;
        private float _t;
        private float _target;
        private bool _snapNext;
        private ServiceRef<IAudioService> _audio;

        /// <summary>Configures the animator (editor builders).</summary>
        public void Configure(string gateId, Transform[] hinges, float[] openYaw, float seconds, string openCue, string closeCue, bool kick)
        {
            _gateId = gateId;
            _hinges = hinges ?? new Transform[0];
            _openYaw = openYaw ?? new float[0];
            _seconds = seconds;
            _openCue = openCue;
            _closeCue = closeCue;
            _kick = kick;
        }

        private void Awake()
        {
            _closed = new Quaternion[_hinges.Length];
            for (var i = 0; i < _hinges.Length; i++)
                if (_hinges[i] != null) _closed[i] = _hinges[i].localRotation;
        }

        private void OnEnable()
        {
            GameEvents.GateChanged += OnGateChanged;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.GateChanged -= OnGateChanged;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
        }

        private void Start()
        {
            if (MarkerRegistry.Instance.TryGet(_gateId, out GateMarker gate) && gate != null) Snap(gate.IsOpen ? 1f : 0f);
        }

        private void OnCheckpointLoaded(string id) => _snapNext = true;

        private void OnGateChanged(string id, bool open)
        {
            if (id != _gateId) return;
            var target = open ? 1f : 0f;
            if (_snapNext)
            {
                _snapNext = false;
                Snap(target);
                return;
            }
            if (Mathf.Approximately(target, _target)) return;
            _target = target;
            var cue = open ? _openCue : _closeCue;
            if (!string.IsNullOrEmpty(cue)) _audio.Value?.Play(cue, transform.position);
        }

        private void Snap(float t)
        {
            _t = t;
            _target = t;
            Pose(t);
        }

        private void Update()
        {
            if (Mathf.Approximately(_t, _target)) return;
            var speed = 1f / Mathf.Max(0.05f, _kick && _target > _t ? _seconds * 0.25f : _seconds);
            _t = Mathf.MoveTowards(_t, _target, speed * Time.deltaTime);
            Pose(_t);
        }

        private void Pose(float t)
        {
            if (_closed == null) Awake();
            // Ease-out with a small overshoot settle for kicked doors.
            var e = 1f - (1f - t) * (1f - t);
            if (_kick && _target > 0.5f) e = Mathf.Min(1.04f, e * 1.04f);
            for (var i = 0; i < _hinges.Length; i++)
            {
                if (_hinges[i] == null) continue;
                var yaw = i < _openYaw.Length ? _openYaw[i] : 90f;
                _hinges[i].localRotation = _closed[i] * Quaternion.Euler(0f, yaw * e, 0f);
            }
        }
    }
}
