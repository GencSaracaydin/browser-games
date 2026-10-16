using LongShadow.Core;
using LongShadow.World.Look.Common;
using UnityEngine;

namespace LongShadow.World.Look.Atmosphere
{
    /// <summary>
    /// Global wind (GDD §9.7, TECH_PLAN §7.14): west → east at 3–5 m/s with gusts. Writes the shader globals
    /// <c>_LS_WindDirection</c> (xyz toward, w m/s) and <c>_LS_WindParams</c> (x gust, y turbulence, z integrated wind
    /// phase, w flutter phase) used by <c>LongShadow/World/FoliageWind</c> and any Level cloth/awning shader, and drives
    /// an optional <see cref="WindZone"/> so particle systems with External Forces drift east consistently. C# consumers
    /// read <see cref="Velocity"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.LightStage + 1)]
    [DisallowMultipleComponent]
    public sealed class WindController : MonoBehaviour
    {
        [SerializeField, Tooltip("Directional WindZone for particles (optional).")]
        private WindZone _zone;
        [SerializeField, Tooltip("Scales every wind speed (1 = GDD).")]
        private float _strength = 1f;
        [SerializeField, Tooltip("Seed so two scenes do not gust in lock-step.")]
        private float _seed = 3.7f;
        [SerializeField, Tooltip("WindZone main per m/s of wind.")]
        private float _zoneMainPerMps = 0.12f;

        private float _phase;
        private float _flutter;
        private Vector3 _velocity = new Vector3(WindMath.MinSpeed, 0f, 0f);
        private float _gust;

        private static WindController s_active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active = null;

        /// <summary>World wind velocity in m/s (default west → east 3 m/s when no controller is alive).</summary>
        public static Vector3 Velocity => s_active != null ? s_active._velocity : new Vector3(WindMath.MinSpeed, 0f, 0f);

        /// <summary>Current gust amount 0..1.</summary>
        public static float GustAmount => s_active != null ? s_active._gust : 0f;

        private void OnEnable()
        {
            s_active = this;
            Tick(0f);
        }

        private void OnDisable()
        {
            if (s_active == this) s_active = null;
        }

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float dt)
        {
            var t = Time.time;
            var speed = WindMath.Speed(t, _seed) * _strength;
            var dir = WindMath.Direction(t, _seed);
            _gust = WindMath.Gust(t, _seed);
            var turb = WindMath.Turbulence(t, _seed);
            _phase += dt * speed * 0.35f;
            _flutter += dt * (2.5f + 3f * _gust);
            if (_phase > 10000f) _phase -= 10000f;
            if (_flutter > 10000f) _flutter -= 10000f;
            _velocity = dir * speed;

            Shader.SetGlobalVector(LookShaderIds.WindDirection, new Vector4(dir.x, dir.y, dir.z, speed));
            Shader.SetGlobalVector(LookShaderIds.WindParams, new Vector4(_gust, turb, _phase, _flutter));

            if (_zone == null) return;
            _zone.mode = WindZoneMode.Directional;
            _zone.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            _zone.windMain = speed * _zoneMainPerMps;
            _zone.windTurbulence = turb * 0.5f;
            _zone.windPulseMagnitude = 0.3f + 0.4f * _gust;
            _zone.windPulseFrequency = 0.15f;
        }

#if UNITY_EDITOR
        /// <summary>Editor builders: wires the WindZone.</summary>
        public void EditorWire(WindZone zone) => _zone = zone;
#endif
    }
}
