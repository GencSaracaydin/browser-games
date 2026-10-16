using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Exposure
{
    /// <summary>Runtime helpers for volume profiles that Look drives per frame (never modifying shared assets).</summary>
    public static class VolumeProfileUtil
    {
        /// <summary>
        /// Deep copy of a profile: every component is instantiated, so changing the copy never touches the asset (a plain
        /// <c>Instantiate(profile)</c> would share the component sub-assets). Returns an empty runtime profile for null.
        /// </summary>
        public static VolumeProfile DeepCopy(VolumeProfile source, string name)
        {
            var copy = ScriptableObject.CreateInstance<VolumeProfile>();
            copy.name = name;
            copy.hideFlags = HideFlags.DontSave;
            if (source == null) return copy;
            foreach (var c in source.components)
            {
                if (c == null) continue;
                var cc = Object.Instantiate(c);
                cc.name = c.name;
                cc.hideFlags = HideFlags.DontSave;
                copy.components.Add(cc);
            }
            return copy;
        }

        /// <summary>Gets or adds a component on a runtime profile.</summary>
        public static T GetOrAdd<T>(VolumeProfile profile, bool overrides = false) where T : VolumeComponent
        {
            if (profile.TryGet(out T c)) return c;
            c = profile.Add<T>(overrides);
            c.hideFlags = HideFlags.DontSave;
            return c;
        }

        /// <summary>Destroys a runtime profile and its components.</summary>
        public static void DestroyRuntime(VolumeProfile profile)
        {
            if (profile == null) return;
            foreach (var c in profile.components)
                if (c != null) Destroy(c);
            Destroy(profile);
        }

        private static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
