using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Buildings;
using LongShadow.World.Level.Geometry;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>
    /// State of one section build (TECH_PLAN §7.15): the open scene and its generated root, the fixed child groups,
    /// far-proxy block roots (<see cref="SectionBlock"/>, D7), world-space mesh batches merged per block, the markers placed
    /// so far (by id) and the build log written to <c>Logs/level_build_&lt;scene&gt;.txt</c>.
    /// </summary>
    public sealed class SectionBuildContext
    {
        /// <summary>Group names under the generated root.</summary>
        public const string GArchitecture = "Architecture", GGround = "Ground", GProps = "Props", GMarkup = "Markup", GBlockers = "Blockers",
            GNav = "Nav", GVolumes = "Volumes", GVehicles = "Vehicles", GLights = "Lights", GDynamic = "Dynamic";

        private readonly Dictionary<string, Transform> _groups = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Transform> _blocks = new Dictionary<string, Transform>();
        private readonly Dictionary<string, MeshBuilder> _batches = new Dictionary<string, MeshBuilder>();
        private readonly Dictionary<string, float> _batchLightmap = new Dictionary<string, float>();
        private readonly List<string> _batchOrder = new List<string>();
        private int _warnings;

        /// <summary>The section being built.</summary>
        public SectionId Section { get; }
        /// <summary>The section scene.</summary>
        public Scene Scene { get; }
        /// <summary>The generated root (rebuilt every run).</summary>
        public SectionRoot Root { get; }
        /// <summary>Mesh asset folder of the section.</summary>
        public string MeshFolder { get; }
        /// <summary>Appendix A table (Assets/_Game/Data/markup_ids.csv).</summary>
        public MarkupIdTable Markup { get; }
        /// <summary>Markers placed in this build, by id.</summary>
        public Dictionary<string, GameObject> Markers { get; } = new Dictionary<string, GameObject>();
        /// <summary>Anchors left by structure generators (cinema, Relay Hall, ...).</summary>
        public StructureAnchors Anchors { get; } = new StructureAnchors();
        /// <summary>Build log.</summary>
        public StringBuilder Log { get; } = new StringBuilder();
        /// <summary>Warnings logged so far.</summary>
        public int Warnings => _warnings;

        /// <summary>Opens the section scene, resets its generated root and loads the markup table.</summary>
        public SectionBuildContext(SectionId section, Scene scene, SectionRoot root, MarkupIdTable markup)
        {
            Section = section;
            Scene = scene;
            Root = root;
            Markup = markup;
            MeshFolder = LevelPaths.SectionMeshes(section);
            BuilderUtil.EnsureFolder(MeshFolder);
            Info("Build " + Sections.SceneName(section) + " (generator v" + LevelBuildInfo.GeneratorVersion + ")");
        }

        /// <summary>A fixed group under the generated root.</summary>
        public Transform Group(string name)
        {
            if (_groups.TryGetValue(name, out var t) && t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(Root.transform, false);
            _groups[name] = t;
            return t;
        }

        /// <summary>A named child of a group (created once).</summary>
        public Transform Child(string group, string name)
        {
            var g = Group(group);
            var c = g.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform;
            c.SetParent(g, false);
            return c;
        }

        /// <summary>
        /// Root for the real geometry of far-proxy block <paramref name="blockId"/> (under Architecture) with its
        /// <see cref="SectionBlock"/>; null id = the Architecture group itself.
        /// </summary>
        public Transform BlockRoot(string blockId)
        {
            if (string.IsNullOrEmpty(blockId)) return Group(GArchitecture);
            if (_blocks.TryGetValue(blockId, out var t) && t != null) return t;
            var def = ProxyBlocks.Get(blockId);
            t = new GameObject("Block_" + blockId).transform;
            t.SetParent(Group(GArchitecture), false);
            var sb = t.gameObject.AddComponent<SectionBlock>();
            sb.Configure(blockId, def != null ? def.Rect : default, def != null ? def.SwapDistance : WorldConst.FarProxySwapDistance, null);
            _blocks[blockId] = t;
            return t;
        }

        /// <summary>The block id of a point in this section (null when outside every block).</summary>
        public string BlockAt(Vector3 p)
        {
            var b = ProxyBlocks.Find(Section, p);
            return b != null ? b.Id : null;
        }

        /// <summary>The root a piece at <paramref name="p"/> belongs under (its block, or Architecture).</summary>
        public Transform RootAt(Vector3 p) => BlockRoot(BlockAt(p));

        /// <summary>
        /// A world-space batch merged into one object per (category, block) on <see cref="Flush"/>; the block is the one
        /// containing <paramref name="at"/>.
        /// </summary>
        public MeshBuilder Batch(string category, Vector3 at, float lightmapScale = WorldConst.LightmapScaleGround)
        {
            var key = category + "|" + (BlockAt(at) ?? string.Empty);
            if (_batches.TryGetValue(key, out var mb)) return mb;
            mb = new MeshBuilder();
            _batches[key] = mb;
            _batchLightmap[key] = lightmapScale;
            _batchOrder.Add(key);
            return mb;
        }

        /// <summary>Writes one object from a builder (meshes saved in the section mesh folder).</summary>
        public GameObject Mesh(string name, Transform parent, Vector3 position, Quaternion rotation, MeshBuilder mb, float lightmapScale, bool lods = true)
        {
            var o = LevelObjectOptions.Architecture(MeshFolder, lightmapScale);
            o.Lods = lods;
            if (lightmapScale <= 0f) o.Flags = LevelObjects.PropFlags;
            return LevelObjects.Build(name, parent, position, rotation, mb, o);
        }

        /// <summary>Builds a catalogue building into its block (or Architecture).</summary>
        public GameObject Building(BuildingSpec spec)
        {
            var origin = BuildingGeometry.Origin(spec);
            var parent = string.IsNullOrEmpty(spec.Block) ? RootAt(origin) : BlockRoot(spec.Block);
            return BuildingGenerator.Build(spec, parent, MeshFolder);
        }

        /// <summary>Writes every batch.</summary>
        public void Flush()
        {
            foreach (var key in _batchOrder)
            {
                var mb = _batches[key];
                if (mb.IsEmpty && mb.Colliders.Count == 0) continue;
                var bar = key.IndexOf('|');
                var category = key.Substring(0, bar);
                var block = key.Substring(bar + 1);
                var parent = BlockRoot(block.Length == 0 ? null : block);
                var name = category + (block.Length > 0 ? "_" + block : string.Empty);
                Mesh(Sections.SceneName(Section) + "_" + name, parent, Vector3.zero, Quaternion.identity, mb, _batchLightmap[key]);
            }
            _batches.Clear();
            _batchLightmap.Clear();
            _batchOrder.Clear();
        }

        /// <summary>Highest collider surface below <paramref name="from"/> (Default layer), or <paramref name="fallback"/>.</summary>
        public float GroundY(float x, float z, float fallback, float from = 60f)
        {
            Physics.SyncTransforms();
            var scene = Scene.GetPhysicsScene();
            return scene.Raycast(new Vector3(x, from, z), Vector3.down, out var hit, from + 40f, 1 << LayerIds.Default, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : fallback;
        }

        /// <summary>Logs an info line.</summary>
        public void Info(string line) => Log.AppendLine(line);

        /// <summary>Logs a warning (also to the console).</summary>
        public void Warn(string line)
        {
            _warnings++;
            Log.AppendLine("WARN " + line);
            LSLog.Warn(LogCategory.Level, "[" + Sections.SceneName(Section) + "] " + line);
        }

        /// <summary>Writes the log file and stamps the root with a one-line report.</summary>
        public string Finish()
        {
            var report = Markers.Count + " markers, " + _warnings + " warnings";
            Root.Stamp(Section, LevelBuildInfo.GeneratorVersion, System.DateTime.UtcNow.ToString("u"), report);
            var path = Path.Combine(BuilderUtil.ProjectRoot, LevelPaths.SectionLog(Section));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Log.ToString());
            EditorUtility.SetDirty(Root);
            return report;
        }
    }
}
