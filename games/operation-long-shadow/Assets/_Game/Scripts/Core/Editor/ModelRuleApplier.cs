using System.Collections.Generic;
using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>Writes a <see cref="ModelRule"/> into a ModelImporter and compares an importer against a rule.</summary>
    public static class ModelRuleApplier
    {
        /// <summary>Applies the rule. Safe inside OnPreprocessModel. Never touches clip lists, avatar bone mappings or
        /// material remaps (owned by WP05's importers and <see cref="ModelMaterialBinder"/>).</summary>
        public static void Apply(ModelImporter mi, ModelRule r)
        {
            // Scene / transform (ORCHESTRATION §3: Blender FBX in metres with axes baked).
            mi.useFileScale = true;
            mi.globalScale = 1f;
            mi.bakeAxisConversion = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importConstraints = false;
            mi.sortHierarchyByName = true;

            // Meshes.
            mi.meshCompression = r.Compression;
            mi.isReadable = false;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.keepQuads = false;
            mi.addCollider = false; // builders add colliders
            mi.importBlendShapes = r.BlendShapes;
            mi.importNormals = r.Normals;
            mi.importTangents = r.Tangents;
            mi.generateSecondaryUV = r.LightmapUVs;
            if (r.LightmapUVs)
            {
                mi.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
                mi.secondaryUVMinLightmapResolution = 16f; // hero texel density (GDD §9.4) -> safe padding
                mi.secondaryUVMinObjectScale = 1f;
            }
            mi.generateMeshLods = r.MeshLods;
            if (r.MeshLods) mi.maximumMeshLod = r.MaxMeshLod;

            // Materials.
            mi.materialImportMode = r.Materials;
            if (r.Materials != ModelImporterMaterialImportMode.None) mi.materialLocation = ModelImporterMaterialLocation.InPrefab;

            // Rig and animation.
            mi.animationType = r.Animation;
            if (r.Animation == ModelImporterAnimationType.Human)
            {
                if (mi.avatarSetup == ModelImporterAvatarSetup.NoAvatar) mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.autoGenerateAvatarMappingIfUnspecified = true;
            }
            mi.importAnimation = r.ImportAnimation;
            if (r.ImportAnimation)
            {
                mi.animationCompression = r.AnimCompression;
                mi.animationRotationError = r.RotationError;
                mi.animationPositionError = r.PositionError;
                mi.animationScaleError = r.ScaleError;
                mi.importAnimatedCustomProperties = false;
            }
        }

        /// <summary>Differences between an importer and a rule; empty when it matches.</summary>
        public static List<string> Diff(ModelImporter mi, ModelRule r)
        {
            var diffs = new List<string>();
            void Check<T>(string name, T actual, T expected)
            {
                if (!Equals(actual, expected)) diffs.Add(name + ": " + actual + " != " + expected);
            }
            Check("animationType", mi.animationType, r.Animation);
            Check("importAnimation", mi.importAnimation, r.ImportAnimation);
            Check("materialImportMode", mi.materialImportMode, r.Materials);
            Check("meshCompression", mi.meshCompression, r.Compression);
            Check("generateSecondaryUV", mi.generateSecondaryUV, r.LightmapUVs);
            Check("generateMeshLods", mi.generateMeshLods, r.MeshLods);
            Check("importNormals", mi.importNormals, r.Normals);
            Check("importTangents", mi.importTangents, r.Tangents);
            Check("isReadable", mi.isReadable, false);
            Check("bakeAxisConversion", mi.bakeAxisConversion, false);
            Check("useFileScale", mi.useFileScale, true);
            if (r.Animation == ModelImporterAnimationType.Human && mi.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
                diffs.Add("avatarSetup: NoAvatar");
            if (r.ImportAnimation) Check("animationCompression", mi.animationCompression, r.AnimCompression);
            return diffs;
        }
    }
}
