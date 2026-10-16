using System;
using LongShadow.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LongShadow.World.Look.Editor
{
    /// <summary>Small helpers shared by the Look prefab builders.</summary>
    public static class LookBuildUtil
    {
        /// <summary>A child primitive without collider, with a material.</summary>
        public static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
            Material material, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var r = go.GetComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = shadows;
            return go;
        }

        /// <summary>A child quad renderer (no collider, no shadows, no probes): glow sprites and cards.</summary>
        public static MeshRenderer Quad(string name, Transform parent, Vector3 localPos, float size, Material material)
        {
            var go = Primitive(name, PrimitiveType.Quad, parent, localPos, new Vector3(size, size, size), material, ShadowCastingMode.Off);
            var r = go.GetComponent<MeshRenderer>();
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        /// <summary>A child light.</summary>
        public static Light Light(string name, Transform parent, LightType type, Color color, float intensity, float range,
            LightShadows shadows = LightShadows.None, float spotAngle = 60f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows;
            if (type == LightType.Spot)
            {
                l.spotAngle = spotAngle;
                l.innerSpotAngle = spotAngle * 0.6f;
            }
            l.lightmapBakeType = LightmapBakeType.Realtime;
            l.renderingLayerMask = (int)RenderingLayerIds.WorldMask;
            return l;
        }

        /// <summary>Linear-intensity colour from a colour temperature (sRGB like Light.color).</summary>
        public static Color Kelvin(float k) => Mathf.CorrelatedColorTemperatureToRGB(k);

        /// <summary>A child particle system configured for soft FX (world space, no shadows).</summary>
        public static ParticleSystem Particles(string name, Transform parent, Material material, float rate, float lifetime,
            float startSize, float startSpeed, Color color, int maxParticles = 200)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSize = startSize;
            main.startSpeed = startSpeed;
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = rate;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }

        /// <summary>Texture sheet animation (flipbook) on a particle system.</summary>
        public static void Flipbook(ParticleSystem ps, int tilesX, int tilesY, float fps = 0f)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = tilesX;
            tsa.numTilesY = tilesY;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            if (fps > 0f)
            {
                tsa.timeMode = ParticleSystemAnimationTimeMode.FPS;
                tsa.fps = fps;
            }
        }

        /// <summary>Size over lifetime from <paramref name="from"/> to <paramref name="to"/> (fractions of start size).</summary>
        public static void Grow(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        /// <summary>Alpha fade in and out over the lifetime.</summary>
        public static void FadeInOut(ParticleSystem ps, float peakAlpha = 1f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.15f), new GradientAlphaKey(peakAlpha * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        /// <summary>Lets the WindZone push the particles (External Forces).</summary>
        public static void Wind(ParticleSystem ps, float multiplier)
        {
            var ef = ps.externalForces;
            ef.enabled = true;
            ef.multiplier = multiplier;
        }

        /// <summary>Instantiates a model asset (prefab link kept) or returns null.</summary>
        public static GameObject InstantiateModel(GameObject model, Transform parent)
        {
            if (model == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (go == null) go = Object.Instantiate(model);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// Adds a component of another assembly by type name (editor-only reflection; R5 allows it outside runtime code)
        /// and configures it through SerializedObject. Returns null when the type does not exist yet.
        /// </summary>
        public static Component AddByTypeName(GameObject go, string assemblyQualifiedName, Action<SerializedObject> configure)
        {
            var type = Type.GetType(assemblyQualifiedName, false);
            if (type == null) return null;
            var c = go.GetComponent(type) ?? go.AddComponent(type);
            if (configure != null)
            {
                var so = new SerializedObject(c);
                configure(so);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return c;
        }

        /// <summary>Sets a serialized property if it exists.</summary>
        public static void Set(SerializedObject so, string path, Action<SerializedProperty> set)
        {
            var p = so.FindProperty(path);
            if (p != null) set(p);
        }
    }
}
