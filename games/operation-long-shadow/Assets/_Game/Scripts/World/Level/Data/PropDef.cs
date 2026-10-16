using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// A prop the level places, referenced by manifest id (TECH_PLAN D12/D13): which model to load (the manifest target
    /// folder plus a file-name filter), its real size for the greybox stand-in while the model has not landed, its surface,
    /// collider and lighting (D8: small and medium props use light probes, big ones are lightmapped).
    /// </summary>
    public sealed class PropDef
    {
        /// <summary>Key used by the builders ("Sandbag").</summary>
        public string Key;
        /// <summary>ASSET_MANIFEST id ("FAL_SANDBAG").</summary>
        public string ManifestId;
        /// <summary>Model file name filter inside the manifest folder ("SM_Sandbag").</summary>
        public string NameContains;
        /// <summary>Real-world size (x width, y height, z depth) of the greybox stand-in.</summary>
        public Vector3 Size;
        /// <summary>Greybox colour.</summary>
        public Color Color = new Color(0.5f, 0.48f, 0.44f);
        /// <summary>Surface (impacts, footsteps).</summary>
        public SurfaceType Surface = SurfaceType.Default;
        /// <summary>Collider: 0 none, 1 box, 2 capsule (trees), 3 sphere.</summary>
        public int Collider = 1;
        /// <summary>Lightmapped (big props, D8) instead of light-probe lit.</summary>
        public bool Lightmapped;
        /// <summary>Physics layer for the collider (Default, or Debris for physics props).</summary>
        public int Layer = LayerIds.Default;
        /// <summary>Greybox primitive.</summary>
        public PrimitiveType Primitive = PrimitiveType.Cube;
        /// <summary>Usage note.</summary>
        public string Notes;
    }
}
