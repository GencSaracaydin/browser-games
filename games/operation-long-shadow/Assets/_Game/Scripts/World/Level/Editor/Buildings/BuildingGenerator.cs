using System.Collections.Generic;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LongShadow.World.Level.Editor.Buildings
{
    /// <summary>
    /// BuildingGenerator (TECH_PLAN §7.15): turns a <see cref="BuildingSpec"/> (GDD §6.6 BLD-xx and the catalogue's
    /// infill) into a scene object — LOD0 lightmapped (hero scale for BLD landmarks, house scale for infill, GDD §9.4),
    /// LOD1 probe-lit, simplified box colliders with surface tags, PlayerClip roof lids, and the interior rooms the spec
    /// lists — or into the cheap far-proxy shell for <c>S9_TownFar</c>. Geometry is local to the footprint centre and
    /// rotated by the spec's yaw.
    /// </summary>
    public static class BuildingGenerator
    {
        /// <summary>Builds the full building under <paramref name="parent"/>; meshes go to <paramref name="folder"/>.</summary>
        public static GameObject Build(BuildingSpec spec, Transform parent, string folder)
        {
            var mb = new MeshBuilder();
            BuildingGeometry.Build(spec, mb);
            var scale = !string.IsNullOrEmpty(spec.Bld) ? WorldConst.LightmapScaleHero * 0.5f : WorldConst.LightmapScaleHouse;
            var o = LevelObjectOptions.Architecture(folder, scale);
            var go = LevelObjects.Build(NameOf(spec), parent, BuildingGeometry.Origin(spec), Quaternion.Euler(0f, spec.Yaw, 0f), mb, o);
            return go;
        }

        /// <summary>Builds the far-proxy shell (Baked Lit, layer FarProxy, no colliders).</summary>
        public static GameObject BuildProxy(BuildingSpec spec, Transform parent, string folder)
        {
            var mb = new MeshBuilder();
            BuildingGeometry.BuildProxy(spec, mb);
            return LevelObjects.Build("PX_" + NameOf(spec), parent, BuildingGeometry.Origin(spec), Quaternion.Euler(0f, spec.Yaw, 0f), mb, LevelObjectOptions.FarProxy(folder));
        }

        /// <summary>Appends the proxy shell of a building into a world-space builder (merged S9 blocks).</summary>
        public static void AppendProxy(BuildingSpec spec, MeshBuilder target)
        {
            var mb = new MeshBuilder();
            BuildingGeometry.BuildProxy(spec, mb);
            target.Append(mb, Matrix4x4.TRS(BuildingGeometry.Origin(spec), Quaternion.Euler(0f, spec.Yaw, 0f), Vector3.one));
        }

        /// <summary>Scene object name of a spec ("BLD-04_Cafe" style ids are kept verbatim, sanitised).</summary>
        public static string NameOf(BuildingSpec spec) => MeshAssets.SafeName(spec.Id);

        /// <summary>
        /// Builds every catalogue building into a scratch scene (<c>Assets/_Game/Scenes/Dev/Dev_Buildings.unity</c>) laid out
        /// at their real positions, for art review without the section scenes.
        /// </summary>
        [MenuItem("LongShadow/Level/Kit/Build Building Review Scene", false, 120)]
        public static void BuildReviewScene()
        {
            var path = Sections.DevFolder + "/Dev_Buildings.unity";
            BuilderUtil.EnsureFolder(Sections.DevFolder);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
            var root = new GameObject("Buildings").transform;
            var folder = LevelPaths.SectionMeshRoot + "/Dev_Buildings";
            BuilderUtil.EnsureFolder(folder);
            var log = new StringBuilder();
            var n = 0;
            foreach (var spec in BuildingCatalog.All)
            {
                Build(spec, root, folder);
                n++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path);
            log.Append("Built ").Append(n).Append(" buildings into ").Append(path);
            LSLog.Info(LogCategory.Level, log.ToString());
            SceneManager.SetActiveScene(scene);
        }

        /// <summary>Catalogue buildings of a section that are not far-only.</summary>
        public static IEnumerable<BuildingSpec> OfSection(SectionId section)
        {
            foreach (var spec in BuildingCatalog.Of(section))
                if (!spec.FarOnly) yield return spec;
        }
    }
}
