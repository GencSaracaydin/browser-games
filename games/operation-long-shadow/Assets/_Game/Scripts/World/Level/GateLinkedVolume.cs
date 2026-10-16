using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// Arms a trigger volume (normally the <see cref="OutOfBoundsVolume"/> "OOB.Block.*") only while a gate or blocker is
    /// closed (GDD §6.7: each backtrack blocker sits behind an OOB volume so the player is warned first). Blockers are
    /// passable until their beat starts, so the warning must not fire before. Follows <see cref="GameEvents.GateChanged"/>
    /// and reads the gate's state on start through <see cref="IMarkerRegistry"/>.
    /// </summary>
    [RequireComponent(typeof(TriggerVolume))]
    public sealed class GateLinkedVolume : MonoBehaviour
    {
        [SerializeField, Tooltip("GateMarker id (\"Block.MarketSouth\").")]
        private string _gateId;
        [SerializeField, Tooltip("Armed while the gate is closed (true) or while open (false).")]
        private bool _armWhenClosed = true;

        private TriggerVolume _volume;

        /// <summary>The linked gate id.</summary>
        public string GateId => _gateId;

        /// <summary>Configures the link (editor builders).</summary>
        public void Configure(string gateId, bool armWhenClosed)
        {
            _gateId = gateId;
            _armWhenClosed = armWhenClosed;
        }

        private void Awake() => _volume = GetComponent<TriggerVolume>();

        private void OnEnable()
        {
            GameEvents.GateChanged += OnGateChanged;
            GameEvents.SectionLoaded += OnSectionLoaded;
        }

        private void OnDisable()
        {
            GameEvents.GateChanged -= OnGateChanged;
            GameEvents.SectionLoaded -= OnSectionLoaded;
        }

        private void Start() => Sync();

        private void OnSectionLoaded(SectionId id) => Sync();

        private void OnGateChanged(string id, bool open)
        {
            if (id == _gateId) Arm(open);
        }

        private void Sync()
        {
            if (MarkerRegistry.Instance.TryGet(_gateId, out GateMarker gate) && gate != null) Arm(gate.IsOpen);
            else Arm(true);
        }

        private void Arm(bool gateOpen)
        {
            if (_volume == null) _volume = GetComponent<TriggerVolume>();
            if (_volume != null) _volume.SetArmed(_armWhenClosed ? !gateOpen : gateOpen);
        }
    }
}
