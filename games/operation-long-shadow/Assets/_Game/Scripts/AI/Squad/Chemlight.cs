using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// A cracked green chemlight on a squadmate's webbing (GDD G7, Beat 6 "All crack green chemlights on D043"): a small
    /// emissive stick and a faint unshadowed point light (range 2.5 m) so the squad reads in the afterglow without
    /// lighting the scene. Built by SquadPrefabBuilder (or at runtime on first use).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Chemlight : MonoBehaviour
    {
        /// <summary>Chemlight green (cyalume, ~520 nm).</summary>
        public static readonly Color Green = new Color(0.35f, 1f, 0.3f);

        [SerializeField] private Renderer _stick;
        [SerializeField] private Light _light;
        [SerializeField] private float _intensity = 0.55f;

        /// <summary>True while lit.</summary>
        public bool IsOn { get; private set; }

        /// <summary>Binds the parts (builders).</summary>
        public void Configure(Renderer stick, Light light)
        {
            _stick = stick;
            _light = light;
        }

        private void Awake()
        {
            EnsureParts();
            Apply();
        }

        /// <summary>Turns the chemlight on or off.</summary>
        public void SetOn(bool on)
        {
            IsOn = on;
            EnsureParts();
            Apply();
        }

        private void Apply()
        {
            if (_stick != null) _stick.enabled = IsOn;
            if (_light != null) _light.enabled = IsOn;
        }

        private void EnsureParts()
        {
            if (_stick == null)
            {
                var stick = Placeholders.Create("Chemlight", PrimitiveType.Capsule, new Vector3(0.025f, 0.06f, 0.025f), Green, transform);
                _stick = stick.GetComponent<Renderer>();
                if (_stick != null) _stick.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (_light == null)
            {
                _light = gameObject.GetComponent<Light>();
                if (_light == null) _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = Green;
                _light.range = 2.5f;
                _light.intensity = _intensity;
                _light.shadows = LightShadows.None;
            }
        }
    }
}
