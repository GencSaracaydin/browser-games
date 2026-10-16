using TMPro;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Fonts, text materials, sprites and look switches of the HUD and menus (TECH_PLAN §7.13). The HUD is built at
    /// runtime from this asset so WP08 stays code-only; <c>LongShadow/UI/Build Theme</c> (UIThemeBuilder) fills it from
    /// the manifest target paths (<see cref="UIAssetPaths.Theme"/>). Every empty slot falls back: fonts to TMP's default
    /// font (LiberationSans SDF, manifest UI_FONT_FALLBACK), sprites and textures to the procedural shapes in
    /// <see cref="UISprites"/>, materials to runtime copies.
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/UI/Theme", fileName = "UI_Theme")]
    public sealed class UITheme : ScriptableObject
    {
        private const int FontRoleCount = (int)FontRole.Fallback + 1;

        [Header("Fonts (indexed by FontRole)")]
        [SerializeField] private TMP_FontAsset[] _fonts = new TMP_FontAsset[FontRoleCount];
        [Tooltip("Soft dark underlay per font (HUD text over a sunlit desert). Saved as assets so the UNDERLAY_ON shader " +
                 "variant ships in WebGL builds.")]
        [SerializeField] private Material[] _shadowMaterials = new Material[FontRoleCount];
        [Tooltip("Soft phosphor glow underlay per font (CRT and title-card text); tinted per use at runtime.")]
        [SerializeField] private Material[] _glowMaterials = new Material[FontRoleCount];

        [Header("Sprite / texture overrides (indexed by UISpriteId / UITextureId; empty = procedural)")]
        [SerializeField] private Sprite[] _sprites = new Sprite[UISpriteIds.Count];
        [SerializeField] private Texture2D[] _textures = new Texture2D[UISpriteIds.TextureCount];

        [Header("Look")]
        [Tooltip("Phosphor glow on CRT and title-card text.")]
        [SerializeField] private bool _phosphorGlow = true;
        [Tooltip("Scanline darkness on CRT panels, 0..1.")]
        [SerializeField, Range(0f, 1f)] private float _scanlineStrength = 0.6f;
        [Tooltip("Brightness flicker of CRT panels, 0..1.")]
        [SerializeField, Range(0f, 1f)] private float _crtFlicker = 0.35f;
        [Tooltip("Global HUD opacity.")]
        [SerializeField, Range(0.2f, 1f)] private float _hudOpacity = 0.95f;

        /// <summary>True when CRT and title text use the phosphor glow.</summary>
        public bool PhosphorGlow => _phosphorGlow;
        /// <summary>Scanline darkness 0..1.</summary>
        public float ScanlineStrength => _scanlineStrength;
        /// <summary>CRT flicker 0..1.</summary>
        public float CrtFlicker => _crtFlicker;
        /// <summary>Global HUD opacity.</summary>
        public float HudOpacity => _hudOpacity;

        /// <summary>Font of a role, or null.</summary>
        public TMP_FontAsset GetFont(FontRole role) => Get(_fonts, (int)role);

        /// <summary>Saved shadow (dark underlay) material of a role, or null.</summary>
        public Material GetShadow(FontRole role) => Get(_shadowMaterials, (int)role);

        /// <summary>Saved glow (soft underlay) material of a role, or null.</summary>
        public Material GetGlow(FontRole role) => Get(_glowMaterials, (int)role);

        /// <summary>Sprite override, or null.</summary>
        public Sprite GetSprite(UISpriteId id) => Get(_sprites, (int)id);

        /// <summary>Texture override, or null.</summary>
        public Texture2D GetTexture(UITextureId id) => Get(_textures, (int)id);

        /// <summary>Builder API: sets a font slot and its saved materials (null keeps runtime copies).</summary>
        public void SetFont(FontRole role, TMP_FontAsset font, Material shadow, Material glow)
        {
            Ensure();
            _fonts[(int)role] = font;
            _shadowMaterials[(int)role] = shadow;
            _glowMaterials[(int)role] = glow;
        }

        /// <summary>Builder API: sets a sprite override (null = procedural).</summary>
        public void SetSprite(UISpriteId id, Sprite sprite)
        {
            Ensure();
            _sprites[(int)id] = sprite;
        }

        /// <summary>Builder API: sets a texture override (null = procedural).</summary>
        public void SetTexture(UITextureId id, Texture2D texture)
        {
            Ensure();
            _textures[(int)id] = texture;
        }

        /// <summary>A theme with every slot empty (pure fallbacks); used when no theme asset is assigned.</summary>
        public static UITheme CreateRuntimeDefault()
        {
            var t = CreateInstance<UITheme>();
            t.name = "UI_Theme (runtime default)";
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        private void OnEnable() => Ensure();

        private void Ensure()
        {
            if (_fonts == null || _fonts.Length != FontRoleCount) System.Array.Resize(ref _fonts, FontRoleCount);
            if (_shadowMaterials == null || _shadowMaterials.Length != FontRoleCount) System.Array.Resize(ref _shadowMaterials, FontRoleCount);
            if (_glowMaterials == null || _glowMaterials.Length != FontRoleCount) System.Array.Resize(ref _glowMaterials, FontRoleCount);
            if (_sprites == null || _sprites.Length != UISpriteIds.Count) System.Array.Resize(ref _sprites, UISpriteIds.Count);
            if (_textures == null || _textures.Length != UISpriteIds.TextureCount) System.Array.Resize(ref _textures, UISpriteIds.TextureCount);
        }

        private static T Get<T>(T[] array, int i) where T : Object =>
            array != null && i >= 0 && i < array.Length ? array[i] : null;
    }
}
