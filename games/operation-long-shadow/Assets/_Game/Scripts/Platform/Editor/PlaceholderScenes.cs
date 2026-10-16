using System.IO;
using System.Linq;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Dev placeholder scenes so the web build, the boot flow and the streamer work before WP11's S_Persistent and
    /// WP09/WP10's section scenes exist (WP01 DoD: "S_Boot + a placeholder addressable S_Persistent + one dummy section").
    /// They live in <see cref="SectionStreamer.PlaceholderFolder"/> as <c>PH_&lt;scene&gt;.unity</c>, never replace a
    /// real scene (<see cref="AddressablesGroupRules"/> drops a placeholder as soon as the real scene exists) and are
    /// addressed by the real scene name, so <c>Sections.AddressKey</c> works unchanged.
    /// <list type="bullet">
    /// <item><c>PH_S_Persistent</c>: a greybox yard, a realtime sun (az 258°, TECH_PLAN §3.8), a camera with an
    /// AudioListener (the real S_Persistent gets its only listener from WP06's AudioService) and a
    /// <see cref="LibraryRegistry"/> holding every <c>LIB_*</c> asset in Data/Libraries;</item>
    /// <item><c>PH_S0_Booth</c> … <c>PH_S9_TownFar</c>: one labelled greybox pillar each, offset per section, so a
    /// screenshot shows which sections are loaded.</item>
    /// </list>
    /// </summary>
    public static class PlaceholderScenes
    {
        /// <summary>Where the LIB_* assets live (TECH_PLAN §4.1 Data/Libraries, WP11).</summary>
        public const string LibrariesFolder = "Assets/_Game/Data/Libraries";

        /// <summary>Project path of the S_Persistent placeholder.</summary>
        public static string PersistentPlaceholderPath => SectionStreamer.PlaceholderPath(Sections.PersistentScene);

        /// <summary>Project path of a section placeholder.</summary>
        public static string SectionPlaceholderPath(SectionId id) => SectionStreamer.PlaceholderPath(Sections.SceneName(id));

        /// <summary>True when the real S_Persistent exists.</summary>
        public static bool RealPersistentExists => File.Exists(Path.Combine(BuilderUtil.ProjectRoot, SectionStreamer.PersistentScenePath));

        /// <summary>True when the real section scene exists.</summary>
        public static bool RealSectionExists(SectionId id) => File.Exists(Path.Combine(BuilderUtil.ProjectRoot, Sections.ScenePath(id)));

        /// <summary>Menu: (re)build every placeholder whose real scene does not exist yet.</summary>
        [MenuItem("LongShadow/Platform/Build Placeholder Scenes", priority = 205)]
        public static void BuildMenu()
        {
            var log = new StringBuilder();
            var n = EnsureAll(true, log);
            Debug.Log("[LS:Platform] Placeholder scenes: " + n + " built\n" + log);
        }

        /// <summary>
        /// Creates (or with <paramref name="rebuild"/> re-creates) the placeholder of every scene that does not exist.
        /// Returns the number of scenes written.
        /// </summary>
        public static int EnsureAll(bool rebuild, StringBuilder log)
        {
            log = log ?? new StringBuilder();
            var written = 0;
            if (EnsurePersistent(rebuild, log)) written++;
            for (var i = 0; i < Sections.Count; i++)
                if (EnsureSection((SectionId)i, rebuild, log)) written++;
            return written;
        }

        /// <summary>Builds the S_Persistent placeholder when the real scene is missing. Returns true if written.</summary>
        public static bool EnsurePersistent(bool rebuild, StringBuilder log)
        {
            if (RealPersistentExists)
            {
                log?.AppendLine("S_Persistent exists; no placeholder needed");
                return false;
            }
            var path = PersistentPlaceholderPath;
            if (!rebuild && Exists(path)) return false;
            using (EditorScenes.OpenEmpty(path))
            {
                EditorScenes.NoSkybox(new Color(0.32f, 0.30f, 0.28f));
                var root = new GameObject("PH_S_Persistent").transform;

                var sunGo = new GameObject("PH_SUN");
                sunGo.transform.SetParent(root, false);
                sunGo.transform.rotation = Quaternion.Euler(12f, 78f, 0f); // elevation, azimuth 258° (TECH_PLAN §3.8)
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.82f, 0.62f);
                sun.intensity = 1.6f;
                sun.shadows = LightShadows.Soft;
                sun.lightmapBakeType = LightmapBakeType.Realtime;
                RenderSettings.sun = sun;

                var camGo = new GameObject("PH_Camera") { tag = "MainCamera" };
                camGo.transform.SetParent(root, false);
                camGo.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, -9f), Quaternion.Euler(6f, 0f, 0f));
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.36f, 0.30f, 0.26f);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 500f;
                cam.fieldOfView = 60f;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camGo.AddComponent<AudioListener>(); // placeholder only: the real listener is WP06's

                var yard = new GameObject("PH_Yard").transform;
                yard.SetParent(root, false);
                BuilderUtil.PlaceholderPrimitive("Ground", PrimitiveType.Cube, new Vector3(60f, 0.2f, 60f), Placeholders.Greybox, yard, true)
                           .transform.localPosition = new Vector3(0f, -0.1f, 20f);
                var crate = BuilderUtil.PlaceholderPrimitive("Crate", PrimitiveType.Cube, new Vector3(1.2f, 1.2f, 1.2f), Placeholders.Friendly, yard, true);
                crate.transform.localPosition = new Vector3(-2.2f, 0.6f, 0f);
                crate.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);
                var wall = BuilderUtil.PlaceholderPrimitive("Wall", PrimitiveType.Cube, new Vector3(8f, 3f, 0.4f), new Color(0.7f, 0.63f, 0.52f), yard, true);
                wall.transform.localPosition = new Vector3(1.5f, 1.5f, 6f);
                var figure = BuilderUtil.PlaceholderPrimitive("Figure", PrimitiveType.Capsule, new Vector3(0.5f, 0.9f, 0.5f), Placeholders.Hostile, yard, true);
                figure.transform.localPosition = new Vector3(2.4f, 0.9f, 2f);

                var registryGo = new GameObject("LibraryRegistry");
                registryGo.transform.SetParent(root, false);
                var registry = registryGo.AddComponent<LibraryRegistry>();
                var libs = FindLibraries();
                registry.EditorSetLibraries(libs);

                log?.AppendLine("Built " + path + " (" + libs.Length + " LIB_* assets in its LibraryRegistry)");
            }
            return true;
        }

        /// <summary>Builds a section placeholder when the real section scene is missing. Returns true if written.</summary>
        public static bool EnsureSection(SectionId id, bool rebuild, StringBuilder log)
        {
            var name = Sections.SceneName(id);
            if (RealSectionExists(id))
            {
                log?.AppendLine(name + " exists; no placeholder needed");
                return false;
            }
            var path = SectionPlaceholderPath(id);
            if (!rebuild && Exists(path)) return false;
            using (EditorScenes.OpenEmpty(path))
            {
                EditorScenes.NoSkybox(new Color(0.32f, 0.30f, 0.28f));
                var root = new GameObject("PH_" + name).transform;
                var i = (int)id;
                // A row of pillars along x in front of the placeholder camera: section n at x = -6.75 + 1.5 n.
                var pillar = BuilderUtil.PlaceholderPrimitive(name, PrimitiveType.Cube, new Vector3(0.5f, 1f + 0.25f * i, 0.5f),
                                                              Color.HSVToRGB(i / (float)Sections.Count, 0.45f, 0.8f), root, true);
                pillar.transform.localPosition = new Vector3(-6.75f + 1.5f * i, 0.5f + 0.125f * i, 9f);
                log?.AppendLine("Built " + path);
            }
            return true;
        }

        /// <summary>Every LIB_* ScriptableObject in <see cref="LibrariesFolder"/> (sorted by path).</summary>
        public static ScriptableObject[] FindLibraries()
        {
            if (!AssetDatabase.IsValidFolder(LibrariesFolder)) return new ScriptableObject[0];
            return AssetDatabase.FindAssets("t:ScriptableObject", new[] { LibrariesFolder })
                                .Select(AssetDatabase.GUIDToAssetPath)
                                .Where(p => Path.GetFileName(p).StartsWith("LIB_", System.StringComparison.Ordinal))
                                .OrderBy(p => p, System.StringComparer.Ordinal)
                                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                                .Where(a => a != null)
                                .ToArray();
        }

        private static bool Exists(string projectPath) => File.Exists(Path.Combine(BuilderUtil.ProjectRoot, projectPath));
    }
}
