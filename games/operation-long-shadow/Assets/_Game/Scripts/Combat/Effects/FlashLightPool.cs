using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Short point-light pulses for muzzle flashes and explosions (TECH_PLAN §7.5: 50 ms point lights, at most 4 live,
    /// range 4 m; GDD §9.5 light budget: unshadowed). The oldest pulse is stolen when all are busy. Lights use the default
    /// rendering layer (World), which the view model also receives, so a flash lights the arms and nearby walls
    /// (CINEMATIC_DIRECTION §4).
    /// </summary>
    public sealed class FlashLightPool
    {
        private readonly Light[] _lights;
        private readonly float[] _t;
        private readonly float[] _dur;
        private readonly float[] _peak;
        private int _next;

        /// <summary>Creates <paramref name="count"/> disabled point lights.</summary>
        public FlashLightPool(Transform root, int count)
        {
            _lights = new Light[count];
            _t = new float[count];
            _dur = new float[count];
            _peak = new float[count];
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("FlashLight_" + i);
                go.transform.SetParent(root, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                l.enabled = false;
                _lights[i] = l;
            }
        }

        /// <summary>Starts a pulse.</summary>
        public void Flash(Vector3 position, Color color, float intensity, float range, float duration)
        {
            if (_lights.Length == 0) return;
            var i = FindSlot();
            var l = _lights[i];
            if (l == null) return;
            l.transform.position = position;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.enabled = true;
            _t[i] = 0f;
            _dur[i] = Mathf.Max(0.016f, duration);
            _peak[i] = intensity;
        }

        /// <summary>Decays pulses.</summary>
        public void Tick(float dt)
        {
            for (var i = 0; i < _lights.Length; i++)
            {
                var l = _lights[i];
                if (l == null || !l.enabled) continue;
                _t[i] += dt;
                var k = 1f - _t[i] / _dur[i];
                if (k <= 0f)
                {
                    l.enabled = false;
                    continue;
                }
                l.intensity = _peak[i] * k * k;
            }
        }

        /// <summary>Turns every pulse off.</summary>
        public void Clear()
        {
            for (var i = 0; i < _lights.Length; i++)
                if (_lights[i] != null) _lights[i].enabled = false;
        }

        private int FindSlot()
        {
            for (var i = 0; i < _lights.Length; i++)
                if (_lights[i] != null && !_lights[i].enabled) return i;
            var s = _next;
            _next = (_next + 1) % _lights.Length;
            return s;
        }
    }
}
