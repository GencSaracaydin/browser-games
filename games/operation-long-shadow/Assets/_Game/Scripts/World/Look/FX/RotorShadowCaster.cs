using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.FX
{
    /// <summary>
    /// The rotor-blade shadow strobe (CINEMATIC_DIRECTION §2 I-2: "rotor-blade shadows strobe across the cabin"): a
    /// shadows-only blade mesh spun at the real rotor speed while the visible rotor shows its blur disc, so the low sun
    /// throws a strobing blade shadow through the cabin and onto the ground. Put the blade renderer (2-blade main rotor
    /// mesh, <c>SM_HeliMainRotor</c>) under this transform at the hub; its own mesh renderer is switched to ShadowsOnly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotorShadowCaster : MonoBehaviour
    {
        [SerializeField, Tooltip("Blade renderers (made ShadowsOnly).")]
        private Renderer[] _blades = new Renderer[0];
        [SerializeField, Tooltip("Rotor speed in rpm (Bell 212 class main rotor: 324).")]
        private float _rpm = 324f;
        [SerializeField, Tooltip("Local spin axis.")]
        private Vector3 _axis = Vector3.up;
        [SerializeField, Range(0f, 1f)] private float _power = 1f;

        /// <summary>Spin fraction 0..1 (timelines spool it).</summary>
        public float Power
        {
            get => _power;
            set => _power = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            foreach (var r in _blades)
            {
                if (r == null) continue;
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                r.receiveShadows = false;
            }
        }

        private void Update() => transform.Rotate(_axis, _rpm * 6f * _power * Time.deltaTime, Space.Self);

#if UNITY_EDITOR
        /// <summary>Editor builders.</summary>
        public void EditorSetup(Renderer[] blades, float rpm)
        {
            _blades = blades ?? new Renderer[0];
            _rpm = rpm;
        }
#endif
    }
}
