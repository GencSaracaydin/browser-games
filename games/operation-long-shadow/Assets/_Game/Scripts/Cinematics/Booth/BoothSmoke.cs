using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// Cigarette smoke curling through the CRT light (GDD §8.2 shot 3: "smoke drifts in from behind the left shoulder";
    /// CINEMATIC_DIRECTION §3: lit particles in the CRT glow). Configures its ParticleSystem on Awake: slow rising
    /// wisps with curl noise, soft particles, unscaled simulation (the smoke keeps drifting during inserts), lit by the
    /// booth lights through URP Particles/Simple Lit. The puff texture is generated procedurally when the material has none.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public sealed class BoothSmoke : MonoBehaviour
    {
        [SerializeField, Tooltip("Hand-held cigarette (denser) or the one resting in the ashtray.")] private bool _handHeld = true;
        [SerializeField] private bool _playOnAwake = true;

        private static Texture2D s_puff;
        private ParticleSystem _ps;
        private Material _runtimeMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_puff = null;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            Configure(_ps, _handHeld);
            var r = GetComponent<ParticleSystemRenderer>();
            if (r != null) EnsureMaterial(r);
            if (_playOnAwake) _ps.Play();
        }

        private void OnDestroy()
        {
            if (_runtimeMat != null) Destroy(_runtimeMat);
        }

        /// <summary>Starts or stops emitting (existing wisps finish).</summary>
        public void SetEmitting(bool on)
        {
            if (_ps == null) return;
            if (on && !_ps.isEmitting) _ps.Play();
            else if (!on && _ps.isEmitting) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>Particle settings of a booth smoke source.</summary>
        public static void Configure(ParticleSystem ps, bool handHeld)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.useUnscaledTime = true;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(handHeld ? 6f : 4.5f, handHeld ? 8.5f : 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.82f, 0.84f, 0.86f, handHeld ? 0.32f : 0.22f);
            main.gravityModifier = -0.0035f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = handHeld ? 4.5f : 2.5f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.004f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // Drift toward the CRTs (+Z) and slightly right: the booth's air handling.
            vel.x = new ParticleSystem.MinMaxCurve(0.01f, 0.035f);
            vel.y = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            vel.z = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.07f);
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.15f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.4f, 1.6f), new Keyframe(1f, 6.5f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.9f, 0.92f, 0.95f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        }

        private void EnsureMaterial(ParticleSystemRenderer r)
        {
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var m = r.sharedMaterial;
            if (m == null)
            {
                var lib = CinematicLibrary.Current;
                m = lib != null ? lib.Find<Material>(CinematicAssetIds.MatSmoke) : null;
                if (m == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
                    if (shader == null)
                    {
                        LSLog.WarnOnce("smoke-shader", LogCategory.Cinematics, "Particles/Simple Lit shader missing; booth smoke hidden.", this);
                        r.enabled = false;
                        return;
                    }
                    _runtimeMat = new Material(shader) { name = "M_BoothSmoke_Runtime" };
                    _runtimeMat.SetFloat("_Surface", 1f);
                    _runtimeMat.SetFloat("_Blend", 0f);
                    _runtimeMat.SetFloat("_SoftParticlesEnabled", 1f);
                    _runtimeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _runtimeMat.EnableKeyword("_SOFTPARTICLES_ON");
                    _runtimeMat.SetOverrideTag("RenderType", "Transparent");
                    _runtimeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _runtimeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _runtimeMat.SetInt("_ZWrite", 0);
                    _runtimeMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    m = _runtimeMat;
                }
                r.sharedMaterial = m;
            }
            if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null)
            {
                if (m != _runtimeMat)
                {
                    _runtimeMat = new Material(m) { name = m.name + "_Runtime" };
                    m = _runtimeMat;
                    r.sharedMaterial = m;
                }
                m.SetTexture("_BaseMap", Puff());
            }
        }

        /// <summary>A 64 × 64 soft, noisy puff (generated once, GPU-only).</summary>
        public static Texture2D Puff()
        {
            if (s_puff != null) return s_puff;
            s_puff = CreatePuff(true);
            return s_puff;
        }

        /// <summary>Creates a new puff texture (builders save a readable one as an asset).</summary>
        public static Texture2D CreatePuff(bool makeNoLongerReadable)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "T_SmokePuff", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var u = (x + 0.5f) / n * 2f - 1f;
                var v = (y + 0.5f) / n * 2f - 1f;
                var d = Mathf.Sqrt(u * u + v * v);
                var falloff = Mathf.Clamp01(1f - d);
                falloff *= falloff;
                var noise = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.6f + Mathf.PerlinNoise(x * 0.27f + 7f, y * 0.27f + 3f) * 0.4f;
                var a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(falloff * (0.45f + noise)));
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(true, makeNoLongerReadable);
            return tex;
        }
    }
}
