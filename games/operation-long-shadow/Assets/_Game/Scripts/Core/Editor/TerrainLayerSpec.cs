namespace LongShadow.Core.Editor
{
    /// <summary>A Unity TerrainLayer to build next to a material (the "terrain_layer" object of a material spec).</summary>
    public sealed class TerrainLayerSpec
    {
        /// <summary>TerrainLayer asset path ("Assets/_Game/Art/Materials/Env/TL_AerialSand.terrainlayer").</summary>
        public string AssetPath;
        /// <summary>Base colour texture.</summary>
        public string Diffuse;
        /// <summary>Normal map.</summary>
        public string Normal;
        /// <summary>Mask map (D4 + B height: R metal, G AO, B height, A smoothness).</summary>
        public string Mask;
        /// <summary>World tile size in metres.</summary>
        public float TileSizeMeters = 2f;
        /// <summary>Normal strength.</summary>
        public float NormalScale = 1f;
    }
}
