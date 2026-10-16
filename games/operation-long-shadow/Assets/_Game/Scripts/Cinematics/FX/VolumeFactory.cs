using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Cinematics.FX
{
    /// <summary>
    /// Creates the runtime URP volumes owned by WP09 (FX volumes of TECH_PLAN §3.7, the cinematic lens volume, the booth
    /// grade driver). A global volume on layer L affects every camera whose volume mask contains L: layer Default reaches
    /// the player and cinematic world cameras, layer Booth reaches Cam_Booth only. When a profile asset is given (built by
    /// the FX profile builder so its effects survive post-processing variant stripping) the volume gets a runtime clone,
    /// so play mode never dirties the asset.
    /// </summary>
    public static class VolumeFactory
    {
        /// <summary>Creates a global volume child with a profile (cloned from <paramref name="template"/> when given).</summary>
        public static Volume Create(Transform parent, string name, int layer, float priority, VolumeProfile template)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = priority;
            v.weight = 0f;
            v.sharedProfile = template != null ? Object.Instantiate(template) : ScriptableObject.CreateInstance<VolumeProfile>();
            v.sharedProfile.name = name + "_Runtime";
            return v;
        }

        /// <summary>Gets an override from the volume's profile or adds it (all parameters non-overridden).</summary>
        public static T Get<T>(Volume v) where T : VolumeComponent
        {
            if (v == null || v.sharedProfile == null) return null;
            if (v.sharedProfile.TryGet(out T c)) return c;
            return v.sharedProfile.Add<T>(false);
        }

        /// <summary>Destroys the runtime profile and the volume object.</summary>
        public static void Destroy(Volume v)
        {
            if (v == null) return;
            var p = v.sharedProfile;
            v.sharedProfile = null;
            if (p != null) Object.Destroy(p);
            Object.Destroy(v.gameObject);
        }
    }
}
