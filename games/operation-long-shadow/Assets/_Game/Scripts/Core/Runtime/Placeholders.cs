using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Greybox stand-ins for assets that have not landed yet. Every system must stay playable with these: log with
    /// <see cref="LSLog.MissingAsset"/> once, then build a primitive. Materials are shared per colour (URP/Lit) and are
    /// runtime objects: editor builders that save prefabs should use <c>LongShadow.Core.Editor.BuilderUtil.PlaceholderMaterial</c>
    /// (a material asset) instead of <see cref="Material"/>.
    /// </summary>
    public static class Placeholders
    {
        /// <summary>Neutral greybox grey.</summary>
        public static readonly Color Greybox = new Color(0.55f, 0.55f, 0.53f);
        /// <summary>Highly visible tint for missing assets.</summary>
        public static readonly Color Missing = new Color(0.85f, 0.2f, 0.85f);
        /// <summary>Friendly placeholder tint (squad).</summary>
        public static readonly Color Friendly = new Color(0.25f, 0.55f, 0.3f);
        /// <summary>Hostile placeholder tint (enemies).</summary>
        public static readonly Color Hostile = new Color(0.65f, 0.25f, 0.2f);

        private static readonly Dictionary<Color32, Material> s_materials = new Dictionary<Color32, Material>();
        private static Sprite s_whiteSprite;
        private static Shader s_shader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_materials.Clear();
            s_whiteSprite = null;
            s_shader = null;
        }

        /// <summary>A shared lit material of a colour (URP/Lit, falling back to Simple Lit/Unlit).</summary>
        public static Material Material(Color color)
        {
            Color32 key = color;
            if (s_materials.TryGetValue(key, out var m) && m != null) return m;
            if (s_shader == null)
            {
                s_shader = Shader.Find("Universal Render Pipeline/Lit");
                if (s_shader == null) s_shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (s_shader == null) s_shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (s_shader == null) s_shader = Shader.Find("Hidden/InternalErrorShader");
            }
            m = new Material(s_shader) { name = "M_Placeholder_" + ColorUtility.ToHtmlStringRGB(color), color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            s_materials[key] = m;
            return m;
        }

        /// <summary>
        /// Creates a primitive placeholder named "PH_&lt;name&gt;" with a shared tinted material. The collider is removed unless
        /// <paramref name="keepCollider"/> is true. <paramref name="size"/> scales the primitive (metres).
        /// </summary>
        public static GameObject Create(string name, PrimitiveType type, Vector3 size, Color color, Transform parent = null,
                                        bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "PH_" + name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Material(color);
            if (!keepCollider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null)
                {
                    if (Application.isPlaying) Object.Destroy(c);
                    else Object.DestroyImmediate(c);
                }
            }
            return go;
        }

        /// <summary>Logs the missing asset once and returns a placeholder primitive in the "missing" tint.</summary>
        public static GameObject ForMissingAsset(string assetId, LogCategory category, PrimitiveType type, Vector3 size,
                                                 Transform parent = null, string expectedPath = null)
        {
            LSLog.MissingAsset(category, assetId, expectedPath);
            return Create(assetId, type, size, Missing, parent);
        }

        /// <summary>A capsule humanoid stand-in (1.8 m), feet at the origin.</summary>
        public static GameObject Humanoid(string name, Color color, Transform parent = null)
        {
            var root = new GameObject("PH_" + name);
            if (parent != null) root.transform.SetParent(parent, false);
            var body = Create(name + "_Body", PrimitiveType.Capsule, new Vector3(0.5f, 0.9f, 0.5f), color, root.transform);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var head = Create(name + "_Visor", PrimitiveType.Cube, new Vector3(0.3f, 0.08f, 0.1f), Color.black, root.transform);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0.2f);
            return root;
        }

        /// <summary>A white 4x4 sprite for UI placeholders.</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (s_whiteSprite == null)
                    s_whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                return s_whiteSprite;
            }
        }

        /// <summary>A silent clip of a given length (audio placeholder so timing code still works).</summary>
        public static AudioClip SilentClip(string name, float seconds, int frequency = 22050)
        {
            var samples = Mathf.Max(1, Mathf.CeilToInt(seconds * frequency));
            var clip = AudioClip.Create("PH_" + name, samples, 1, frequency, false);
            return clip;
        }
    }
}
