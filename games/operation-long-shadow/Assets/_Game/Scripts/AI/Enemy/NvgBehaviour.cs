using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// E10-04's night-vision goggles (GDD §7.10: "Green lens glow is his tell"): a small HDR-green lens quad and a faint
    /// unshadowed point light on the goggles, on while he lives. The glow child ("NVG_Glow") is built by
    /// EnemyPrefabBuilder on PF_Greycoat_NVG (or created here at runtime). His BK_GRY_09 at the first flare is a radio
    /// line in WP07's Beat 10 script (he is still in his closet at t 12).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NvgBehaviour : MonoBehaviour, IEnemyBehaviour
    {
        /// <summary>Lens glow colour (HDR).</summary>
        public static readonly Color GlowColor = new Color(0.25f, 2.6f, 0.45f, 1f);

        [SerializeField] private GameObject _glow;

        /// <summary>Binds the glow object (builders).</summary>
        public void Configure(GameObject glow) => _glow = glow;

        /// <inheritdoc/>
        public void OnConfigured(EnemyBrain brain)
        {
            if (_glow == null)
            {
                var t = transform.Find("NVG_Glow");
                if (t == null) t = FindDeep(transform, "NVG_Glow");
                _glow = t != null ? t.gameObject : CreateRuntimeGlow(brain);
            }
            if (_glow != null) _glow.SetActive(true);
        }

        /// <inheritdoc/>
        public bool OverrideTick(EnemyBrain brain, float now, float dt) => false;

        /// <inheritdoc/>
        public void OnDeath(EnemyBrain brain, in DamageInfo d)
        {
            if (_glow != null) _glow.SetActive(false);
        }

        private GameObject CreateRuntimeGlow(EnemyBrain brain)
        {
            var head = brain.Body?.Head;
            var parent = head != null && head != transform ? head : transform;
            var go = new GameObject("NVG_Glow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = parent == transform ? new Vector3(0f, 1.66f, 0.16f) : new Vector3(0f, 0.04f, 0.16f);
            var lens = Placeholders.Create("NVG_Lens", PrimitiveType.Sphere, new Vector3(0.05f, 0.05f, 0.03f), new Color(0.2f, 1f, 0.35f), go.transform);
            var r = lens.GetComponent<Renderer>();
            if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.35f, 1f, 0.45f);
            light.range = 1.2f;
            light.intensity = 0.6f;
            light.shadows = LightShadows.None;
            return go;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
