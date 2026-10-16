namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// A prefab variant: archetype + body (WP05 <c>CHR_*</c> body prefab) + extras. EnemyPrefabBuilder makes
    /// <see cref="PrefabName"/> under Assets/_Game/Prefabs/Enemies and lists it in LIB_Enemies by <see cref="Id"/>.
    /// </summary>
    public sealed class EnemyVariant
    {
        /// <summary>Creates a variant.</summary>
        public EnemyVariant(string id, ArchetypeId archetype, string bodyManifestId, string prefabName, string[] bodyNameHints, bool nvg = false)
        {
            Id = id;
            Archetype = archetype;
            BodyManifestId = bodyManifestId;
            PrefabName = prefabName;
            BodyNameHints = bodyNameHints ?? new string[0];
            Nvg = nvg;
        }

        /// <summary>Variant id (LIB_Enemies key).</summary>
        public string Id { get; }
        /// <summary>Archetype.</summary>
        public ArchetypeId Archetype { get; }
        /// <summary>ASSET_MANIFEST id of the body (CHR_LEGION_A ...).</summary>
        public string BodyManifestId { get; }
        /// <summary>Prefab file name without extension (PF_Legion_Rifleman_A).</summary>
        public string PrefabName { get; }
        /// <summary>Substrings that identify WP05's body prefab in Assets/_Game/Prefabs/Bodies (case-insensitive).</summary>
        public string[] BodyNameHints { get; }
        /// <summary>NVG lens glow (E10-04).</summary>
        public bool Nvg { get; }
    }
}
