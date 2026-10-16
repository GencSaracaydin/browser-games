using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// Beat 6 flood glare (TECH_PLAN §7.14 <c>FloodGlare</c>, GDD §7.6): when the player looks into a lit flood from inside
    /// its beam, <see cref="IScreenFX.Glare"/> (WP09) gets an amount from the angle between the view ray and the lamp,
    /// the player's position in the beam cone, distance and a line-of-sight check (10 Hz). Several floods combine as
    /// <c>1 - Π(1 - g)</c>. Lives on <c>PF_LookRig</c>; without IScreenFX it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloodGlare : MonoBehaviour
    {
        [SerializeField, Tooltip("Glare starts within this angle of the lamp (degrees).")]
        private float _outerAngle = 25f;
        [SerializeField, Tooltip("Full glare within this angle.")]
        private float _innerAngle = 3f;
        [SerializeField, Tooltip("Distance at which glare halves (metres).")]
        private float _halfDistance = 35f;
        [SerializeField, Tooltip("Line-of-sight blockers.")]
        private LayerMask _occluders = 1 << LayerIds.Default;
        [SerializeField, Tooltip("Seconds between line-of-sight checks.")]
        private float _losInterval = 0.1f;

        private ServiceRef<IScreenFX> _fx;
        private ServiceRef<IPlayerRig> _rig;
        private readonly bool[] _visible = new bool[8];
        private float _nextLos;
        private float _sent = -1f;

        /// <summary>Glare amount sent last (0..1).</summary>
        public float Amount { get; private set; }

        private void OnDisable()
        {
            if (_sent > 0f) _fx.Value?.Glare(0f);
            _sent = 0f;
            Amount = 0f;
        }

        private void Update()
        {
            var floods = FloodLight.All;
            var cam = ViewTransform();
            if (cam == null || floods.Count == 0)
            {
                Send(0f);
                return;
            }

            var checkLos = Time.time >= _nextLos;
            if (checkLos) _nextLos = Time.time + _losInterval;

            var camPos = cam.position;
            var fwd = cam.forward;
            var cosOuter = Mathf.Cos(_outerAngle * Mathf.Deg2Rad);
            var cosInner = Mathf.Cos(_innerAngle * Mathf.Deg2Rad);
            var keep = 1f;
            for (var i = 0; i < floods.Count && i < _visible.Length; i++)
            {
                var f = floods[i];
                if (f == null || !f.IsLit || f.Spot == null) continue;
                var lamp = f.Spot.transform;
                var v = lamp.position - camPos;
                var d = v.magnitude;
                if (d < 0.01f) continue;
                var dir = v / d;
                if (checkLos)
                    _visible[i] = !Physics.Raycast(camPos, dir, Mathf.Max(0f, d - 0.8f), _occluders, QueryTriggerInteraction.Ignore);
                if (!_visible[i]) continue;
                var g = Glare(Vector3.Dot(fwd, dir), Vector3.Dot(lamp.forward, -dir), f.Spot.spotAngle, d, cosOuter, cosInner, _halfDistance);
                keep *= 1f - Mathf.Clamp01(g * Mathf.Min(1f, f.Level));
            }
            Send(1f - keep);
        }

        /// <summary>
        /// Glare of one lamp: view alignment (<paramref name="viewDot"/> = dot(view, toLamp)), position in the beam
        /// (<paramref name="beamDot"/> = dot(lamp forward, toCamera)), spot angle and distance.
        /// </summary>
        public static float Glare(float viewDot, float beamDot, float spotAngle, float distance, float cosOuter, float cosInner,
            float halfDistance)
        {
            var view = Mathf.InverseLerp(cosOuter, cosInner, viewDot);
            view *= view;
            var half = 0.5f * spotAngle * Mathf.Deg2Rad;
            var beam = Mathf.InverseLerp(Mathf.Cos(Mathf.Min(1.5f, half + 0.2f)), Mathf.Cos(half * 0.5f), beamDot);
            var dist = 1f / (1f + (distance / Mathf.Max(1f, halfDistance)) * (distance / Mathf.Max(1f, halfDistance)));
            return Mathf.Clamp01(view * beam * (0.35f + 0.65f * dist) * 1.2f);
        }

        private void Send(float amount)
        {
            Amount = amount;
            if (Mathf.Abs(amount - _sent) < 0.01f && !(amount == 0f && _sent > 0f)) return;
            _sent = amount;
            _fx.Value?.Glare(amount);
        }

        private Transform ViewTransform()
        {
            var rig = _rig.Value;
            if (rig != null && rig.WorldCamera != null && rig.WorldCamera.isActiveAndEnabled) return rig.WorldCamera.transform;
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }
}
