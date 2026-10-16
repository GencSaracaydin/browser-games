using System.Collections.Generic;
using LongShadow.AI.Common;
using LongShadow.AI.Encounters;
using LongShadow.AI.Enemy;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.AI.Editor
{
    /// <summary>
    /// Builds every enemy prefab of <see cref="EnemyVariants"/> (TECH_PLAN §7.6 EnemyPrefabBuilder) into
    /// Assets/_Game/Prefabs/Enemies, LIB_Enemies, and PF_AIServices (AIDirector + TargetRegistry + EncounterSpawner).
    /// Each enemy is a variant of its WP05 body prefab (found by name or by the CHR_* model folder it uses) with
    /// <see cref="EnemyHealth"/>, <see cref="EnemyActor"/>, <see cref="EnemyBrain"/>, the archetype's special behaviours,
    /// and WP04's extras (scope glint, NVG lens); while the body has not landed a <see cref="DebugCapsuleBody"/> soldier
    /// stands in. Re-running after WP05 lands upgrades the prefabs in place (GUIDs kept). Menu:
    /// LongShadow/AI/Build Enemy Prefabs.
    /// </summary>
    public static class EnemyPrefabBuilder
    {
        /// <summary>PF_AIServices path.</summary>
        public const string ServicesPrefabPath = AIBuilderUtil.EnemiesFolder + "/PF_AIServices.prefab";

        /// <summary>Menu entry.</summary>
        [MenuItem("LongShadow/AI/Build Enemy Prefabs", priority = 100)]
        public static void BuildMenu()
        {
            Build();
            BuildServices();
        }

        /// <summary>Builds the enemy prefabs and LIB_Enemies; returns the library.</summary>
        public static EnemyLibrary Build()
        {
            BuilderUtil.EnsureFolder(AIBuilderUtil.EnemiesFolder);
            var entries = new List<EnemyPrefabEntry>();
            var onBodies = 0;
            foreach (var v in EnemyVariants.All)
            {
                var body = AIBuilderUtil.FindBody(v.BodyNameHints, v.BodyManifestId);
                var root = CreateRoot(v, body);
                var path = AIBuilderUtil.EnemiesFolder + "/" + v.PrefabName + ".prefab";
                var saved = BuilderUtil.SavePrefab(root, path);
                if (saved == null) continue;
                if (body != null) onBodies++;
                entries.Add(new EnemyPrefabEntry(v.Id, saved, body != null ? AssetDatabase.GetAssetPath(body) : "DebugCapsuleBody"));
            }
            var lib = BuilderUtil.LoadOrCreateAsset<EnemyLibrary>(EnemyLibrary.AssetPath);
            lib.EditorSetEntries(entries);
            AssetDatabase.SaveAssets();
            LSLog.Info(LogCategory.AI, "EnemyPrefabBuilder: " + entries.Count + " enemy prefabs (" + onBodies + " on WP05 bodies, " +
                                       (entries.Count - onBodies) + " on DebugCapsuleBody) + " + EnemyLibrary.AssetPath);
            return lib;
        }

        private static GameObject CreateRoot(EnemyVariant v, GameObject body)
        {
            var archetype = ArchetypeTable.Get(v.Archetype);
            GameObject root;
            if (body != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(body);
                root.name = v.PrefabName;
                AIBuilderUtil.EnsureHealth<EnemyHealth>(root);
            }
            else
            {
                root = new GameObject(v.PrefabName);
                DebugCapsuleBody.Build(root, archetype.Team, BuilderUtil.PlaceholderMaterial);
            }
            EnemyFactory.AddBrainComponents(root, v);
            var agent = root.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;                                                   // EnemyActor.Prepare enables it on the NavMesh
                agent.radius = DebugCapsuleBody.Radius;                                  // TECH_PLAN §7.15
                agent.height = DebugCapsuleBody.Height;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;   // TECH_PLAN §7.16
                agent.updateRotation = false;
            }
            if (v.Archetype == ArchetypeId.GreycoatMarksman) BuildGlint(root);
            if (v.Nvg) BuildNvg(root);
            return root;
        }

        private static void BuildGlint(GameObject root)
        {
            var marksman = root.GetComponent<MarksmanBehaviour>();
            var glint = root.GetComponentInChildren<ScopeGlint>(true);
            if (glint == null)
            {
                var head = root.GetComponent<ICharacterBody>()?.Head;
                var go = new GameObject("ScopeGlint");
                go.transform.SetParent(head != null && head != root.transform ? head : root.transform, false);
                go.transform.localPosition = head != null && head != root.transform ? new Vector3(0.04f, 0f, 0.35f) : new Vector3(0.04f, 1.62f, 0.35f);
                glint = go.AddComponent<ScopeGlint>();
            }
            var quadT = glint.transform.Find("GlintQuad");
            GameObject quad;
            if (quadT == null)
            {
                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "GlintQuad";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(glint.transform, false);
            }
            else quad = quadT.gameObject;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = AIBuilderUtil.UnlitMaterial("M_AI_ScopeGlint", new Color(6f, 5.6f, 4.8f, 1f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            glint.Configure(r);
            marksman?.Configure(glint);
        }

        private static void BuildNvg(GameObject root)
        {
            var nvg = root.GetComponent<NvgBehaviour>();
            if (nvg == null) return;
            var head = root.GetComponent<ICharacterBody>()?.Head;
            var parent = head != null && head != root.transform ? head : root.transform;
            var glowT = parent.Find("NVG_Glow");
            var glow = glowT != null ? glowT.gameObject : new GameObject("NVG_Glow");
            glow.transform.SetParent(parent, false);
            glow.transform.localPosition = parent == root.transform ? new Vector3(0f, 1.66f, 0.16f) : new Vector3(0f, 0.04f, 0.16f);
            var lensT = glow.transform.Find("NVG_Lens");
            GameObject lens;
            if (lensT == null)
            {
                lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lens.name = "NVG_Lens";
                Object.DestroyImmediate(lens.GetComponent<Collider>());
                lens.transform.SetParent(glow.transform, false);
                lens.transform.localScale = new Vector3(0.05f, 0.05f, 0.03f);
            }
            else lens = lensT.gameObject;
            var r = lens.GetComponent<Renderer>();
            r.sharedMaterial = AIBuilderUtil.UnlitMaterial("M_AI_NvgLens", NvgBehaviour.GlowColor);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = glow.GetComponent<Light>();
            if (light == null) light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.35f, 1f, 0.45f);
            light.range = 1.2f;
            light.intensity = 0.6f;
            light.shadows = LightShadows.None;
            nvg.Configure(glow);
        }

        /// <summary>Builds PF_AIServices (AIDirector, TargetRegistry, EncounterSpawner wired to LIB_Enemies).</summary>
        [MenuItem("LongShadow/AI/Build AI Services Prefab", priority = 101)]
        public static GameObject BuildServices()
        {
            var lib = AssetDatabase.LoadAssetAtPath<EnemyLibrary>(EnemyLibrary.AssetPath);
            if (lib == null) lib = Build();
            var root = new GameObject("PF_AIServices");
            root.AddComponent<TargetRegistry>();
            root.AddComponent<AIDirector>();
            var spawner = root.AddComponent<EncounterSpawner>();
            AIBuilderUtil.SetObject(spawner, "_library", lib);
            var prefab = BuilderUtil.SavePrefab(root, ServicesPrefabPath);
            AssetDatabase.SaveAssets();
            LSLog.Info(LogCategory.AI, "Built " + ServicesPrefabPath);
            return prefab;
        }
    }
}
