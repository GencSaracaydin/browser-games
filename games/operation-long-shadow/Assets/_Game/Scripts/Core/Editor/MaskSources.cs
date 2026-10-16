namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Inputs for <see cref="MaskPacker"/>: any combination of packed (Poly Haven ARM, glTF metallic-roughness) and
    /// single-channel maps (project-relative or absolute paths; PNG or JPG). Missing channels use the constant values.
    /// Single-channel maps are read from their red channel.
    /// </summary>
    public sealed class MaskSources
    {
        /// <summary>Poly Haven ARM: R occlusion, G roughness, B metallic.</summary>
        public string Arm;
        /// <summary>glTF metallicRoughness: G roughness, B metallic (R ignored).</summary>
        public string MetallicRoughness;
        /// <summary>Metallic map.</summary>
        public string Metallic;
        /// <summary>Ambient occlusion map.</summary>
        public string Occlusion;
        /// <summary>Roughness map (smoothness = 1 − roughness).</summary>
        public string Roughness;
        /// <summary>Smoothness/gloss map (wins over roughness).</summary>
        public string Smoothness;
        /// <summary>Height map for B (terrain layers' height blend); B = 0 when absent.</summary>
        public string Height;
        /// <summary>Metallic when no map gives it.</summary>
        public float MetallicValue = 0f;
        /// <summary>Occlusion when no map gives it.</summary>
        public float OcclusionValue = 1f;
        /// <summary>Smoothness when no map gives it.</summary>
        public float SmoothnessValue = 0.5f;
        /// <summary>Height when no map gives it.</summary>
        public float HeightValue = 0f;

        /// <summary>True when at least one map is set.</summary>
        public bool Any => Arm != null || MetallicRoughness != null || Metallic != null || Occlusion != null ||
                           Roughness != null || Smoothness != null || Height != null;
    }
}
