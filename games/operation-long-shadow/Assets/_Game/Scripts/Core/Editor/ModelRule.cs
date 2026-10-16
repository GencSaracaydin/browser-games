using System.Text;
using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>Resolved ModelImporter settings for one file (output of <see cref="ModelRules.Resolve"/>).</summary>
    public sealed class ModelRule
    {
        /// <summary>Rule id ("Props", "Weapons/FP", "Characters", "Clips").</summary>
        public string Name = "";
        /// <summary>None for static meshes, Human for characters and clips.</summary>
        public ModelImporterAnimationType Animation = ModelImporterAnimationType.None;
        /// <summary>Import animation clips.</summary>
        public bool ImportAnimation;
        /// <summary>Material import: None (characters, clips) or ImportViaMaterialDescription (slots kept for
        /// <see cref="ModelMaterialBinder"/>).</summary>
        public ModelImporterMaterialImportMode Materials = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        /// <summary>Mesh compression.</summary>
        public ModelImporterMeshCompression Compression = ModelImporterMeshCompression.Medium;
        /// <summary>Generate lightmap UVs (UV2).</summary>
        public bool LightmapUVs;
        /// <summary>Generate Unity Mesh LODs.</summary>
        public bool MeshLods;
        /// <summary>Highest generated Mesh LOD index (2 = LOD0 + 2 generated levels).</summary>
        public int MaxMeshLod = 2;
        /// <summary>Import blend shapes.</summary>
        public bool BlendShapes;
        /// <summary>Normals.</summary>
        public ModelImporterNormals Normals = ModelImporterNormals.Import;
        /// <summary>Tangents.</summary>
        public ModelImporterTangents Tangents = ModelImporterTangents.CalculateMikk;
        /// <summary>Animation compression (clips).</summary>
        public ModelImporterAnimationCompression AnimCompression = ModelImporterAnimationCompression.Optimal;
        /// <summary>Keyframe reduction error, degrees (rotation) and percent (position, scale).</summary>
        public float RotationError = 0.5f, PositionError = 0.5f, ScaleError = 0.5f;
        /// <summary>The source carries authored LODs (Mesh LOD generation forced off).</summary>
        public bool AuthoredLods;

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Name).Append(": anim ").Append(Animation).Append(ImportAnimation ? " (+clips)" : "");
            sb.Append(" materials ").Append(Materials).Append(" compression ").Append(Compression);
            sb.Append(" normals ").Append(Normals).Append(" tangents ").Append(Tangents);
            sb.Append(LightmapUVs ? " UV2" : "").Append(MeshLods ? " MeshLOD<=" + MaxMeshLod : "");
            if (AuthoredLods) sb.Append(" authored-LODs");
            if (ImportAnimation) sb.Append(" ").Append(AnimCompression).Append(" err ").Append(RotationError).Append("/").Append(PositionError);
            return sb.ToString();
        }
    }
}
