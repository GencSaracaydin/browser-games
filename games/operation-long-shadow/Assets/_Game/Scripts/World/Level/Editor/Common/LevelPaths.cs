using LongShadow.Core;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>Asset locations written by the Level builders (TECH_PLAN §4.1; WP10 owns these folders, §11.1).</summary>
    public static class LevelPaths
    {
        /// <summary>Standalone kit piece meshes (KitGenerator).</summary>
        public const string KitMeshes = "Assets/_Game/Art/Models/Kit/Pieces";
        /// <summary>Kit piece prefabs.</summary>
        public const string KitPrefabs = "Assets/_Game/Prefabs/Kit";
        /// <summary>Per-section generated meshes (buildings, ground, structures).</summary>
        public const string SectionMeshRoot = "Assets/_Game/Art/Models/Kit/Sections";
        /// <summary>Terrain data and layers of S1.</summary>
        public const string Terrain = "Assets/_Game/Art/Models/Terrain";
        /// <summary>Backdrop meshes (ridge silhouette, far hills, salt pan, far ground).</summary>
        public const string Backdrop = "Assets/_Game/Art/Models/Backdrop";
        /// <summary>Generated material variants (lime-wash tints, glass, emissive lenses, Baked Lit proxies).</summary>
        public const string Materials = "Assets/_Game/Art/Materials/Env/Level";
        /// <summary>Env materials built by MaterialFactory from the CC0/fal specs.</summary>
        public const string EnvMaterials = "Assets/_Game/Art/Materials/Env";
        /// <summary>Prop materials.</summary>
        public const string PropMaterials = "Assets/_Game/Art/Materials/Props";
        /// <summary>Decal materials.</summary>
        public const string DecalMaterials = "Assets/_Game/Art/Materials/Decals";
        /// <summary>Prop prefabs built by the level (fruit, lamp heads, ...).</summary>
        public const string PropPrefabs = "Assets/_Game/Prefabs/Props";
        /// <summary>Vehicle prefabs.</summary>
        public const string VehiclePrefabs = "Assets/_Game/Prefabs/Vehicles";
        /// <summary>FX prefabs (fire prop, shaft cards).</summary>
        public const string FxPrefabs = "Assets/_Game/Prefabs/FX";
        /// <summary>Volume profiles (WP10 Look) used by the eye-adaptation zones.</summary>
        public const string Volumes = "Assets/_Game/Settings/Volumes";
        /// <summary>Build logs.</summary>
        public const string LogFolder = "Logs";

        /// <summary>Generated meshes of a section.</summary>
        public static string SectionMeshes(SectionId id) => SectionMeshRoot + "/" + Sections.SceneName(id);

        /// <summary>Folder next to a section scene for its NavMesh data.</summary>
        public static string SectionDataFolder(SectionId id) => Sections.SectionsFolder + "/" + Sections.SceneName(id);

        /// <summary>Log file of a section build.</summary>
        public static string SectionLog(SectionId id) => LogFolder + "/level_build_" + Sections.SceneName(id) + ".txt";
    }
}
