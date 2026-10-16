using UnityEngine;

namespace LongShadow.World.Look.Lightmaps
{
    /// <summary>
    /// One baked set of a section's lightmaps (TECH_PLAN D7, G11; assets under <c>Assets/_Game/Data/Lighting/&lt;Section&gt;/</c>,
    /// written by <c>SectionBaker</c>): slot <c>i</c> holds the texture that replaces slot <c>i</c> of the section's base set.
    /// Sets of one section share the atlas layout because they are baked from the same scene content and settings;
    /// only the lights differ (GOLDEN / SUNSET / DUSK for <c>S9_TownFar</c>; NORMAL / EMERGENCY for the basement).
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Look/Lightmap Set", fileName = "LMS_Section_SET")]
    public sealed class LightmapSet : ScriptableObject
    {
        [SerializeField, Tooltip("GOLDEN, SUNSET, DUSK, NORMAL, EMERGENCY ...")]
        private string _setName;
        [SerializeField, Tooltip("Scene the set belongs to.")]
        private string _sceneName;
        [SerializeField, Tooltip("Lightmap colour textures in slot order.")]
        private Texture2D[] _colors = new Texture2D[0];

        /// <summary>Set name.</summary>
        public string SetName => _setName;
        /// <summary>Scene name.</summary>
        public string SceneName => _sceneName;
        /// <summary>Slots.</summary>
        public int Count => _colors != null ? _colors.Length : 0;

        /// <summary>Colour texture of a slot (null when out of range).</summary>
        public Texture2D Color(int slot) => _colors != null && slot >= 0 && slot < _colors.Length ? _colors[slot] : null;

        /// <summary>Slot of a texture in this set, -1 when absent.</summary>
        public int IndexOf(Texture texture)
        {
            if (texture == null || _colors == null) return -1;
            for (var i = 0; i < _colors.Length; i++)
                if (_colors[i] == texture)
                    return i;
            return -1;
        }

#if UNITY_EDITOR
        /// <summary>Editor baker: fills the set.</summary>
        public void EditorSet(string setName, string sceneName, Texture2D[] colors)
        {
            _setName = setName;
            _sceneName = sceneName;
            _colors = colors ?? new Texture2D[0];
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
