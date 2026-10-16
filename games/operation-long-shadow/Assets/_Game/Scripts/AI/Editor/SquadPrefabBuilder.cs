using LongShadow.AI.Common;
using LongShadow.AI.Squad;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.AI.Editor
{
    /// <summary>
    /// Builds PF_Squad_Hollis/Sparrow/Tinker and PF_Squad (SquadService + the three as nested prefabs) into
    /// Assets/_Game/Prefabs/Squad (TECH_PLAN §7.7, owned by WP04). Each member is a variant of its WP05 body prefab
    /// (found by name or by its CHR_* model folder) with an invulnerable Health (team Squad), <see cref="SquadMember"/>
    /// and a chemlight on the chest; a <see cref="DebugCapsuleBody"/> squad soldier stands in until WP05 lands. WP11
    /// places PF_Squad in S_Persistent. Menu: LongShadow/AI/Build Squad Prefabs.
    /// </summary>
    public static class SquadPrefabBuilder
    {
        /// <summary>PF_Squad path.</summary>
        public const string SquadPrefabPath = AIBuilderUtil.SquadFolder + "/PF_Squad.prefab";

        private struct MemberSpec
        {
            public string Id;
            public string Name;
            public string ManifestId;
            public Vector3 Offset;
        }

        private static readonly MemberSpec[] s_members =
        {
            new MemberSpec { Id = SquadIds.Hollis, Name = "Hollis", ManifestId = "CHR_HOLLIS", Offset = new Vector3(-1.5f, 0f, 0f) },
            new MemberSpec { Id = SquadIds.Sparrow, Name = "Sparrow", ManifestId = "CHR_SPARROW", Offset = new Vector3(0f, 0f, 0f) },
            new MemberSpec { Id = SquadIds.Tinker, Name = "Tinker", ManifestId = "CHR_TINKER", Offset = new Vector3(1.5f, 0f, 0f) },
        };

        /// <summary>Path of a member prefab.</summary>
        public static string MemberPath(string name) => AIBuilderUtil.SquadFolder + "/PF_Squad_" + name + ".prefab";

        /// <summary>Menu entry.</summary>
        [MenuItem("LongShadow/AI/Build Squad Prefabs", priority = 102)]
        public static GameObject Build()
        {
            BuilderUtil.EnsureFolder(AIBuilderUtil.SquadFolder);
            var prefabs = new GameObject[s_members.Length];
            for (var i = 0; i < s_members.Length; i++)
            {
                var spec = s_members[i];
                var body = AIBuilderUtil.FindBody(new[] { spec.Name }, spec.ManifestId);
                var root = CreateMember(spec, body);
                prefabs[i] = BuilderUtil.SavePrefab(root, MemberPath(spec.Name));
                LSLog.Info(LogCategory.Squad, "Built " + MemberPath(spec.Name) + (body != null ? " on " + AssetDatabase.GetAssetPath(body) : " on DebugCapsuleBody"));
            }
            var squad = new GameObject("PF_Squad");
            squad.AddComponent<SquadService>();
            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], squad.transform);
                inst.transform.localPosition = s_members[i].Offset;
            }
            var saved = BuilderUtil.SavePrefab(squad, SquadPrefabPath);
            AssetDatabase.SaveAssets();
            return saved;
        }

        private static GameObject CreateMember(MemberSpec spec, GameObject body)
        {
            GameObject root;
            if (body != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(body);
                root.name = "PF_Squad_" + spec.Name;
            }
            else
            {
                root = new GameObject("PF_Squad_" + spec.Name);
                DebugCapsuleBody.Build(root, Team.Squad, BuilderUtil.PlaceholderMaterial, true);
            }
            var health = root.GetComponent<Health>();
            if (health == null) health = root.AddComponent<Health>();
            AIBuilderUtil.SetBool(health, "_invulnerable", true);                                // GDD §15
            AIBuilderUtil.SetString(health, "_actorId", spec.Id);
            AIBuilderUtil.SetEnum(health, "_team", (int)Team.Squad);
            var member = root.GetComponent<SquadMember>();
            if (member == null) member = root.AddComponent<SquadMember>();
            var chem = BuildChemlight(root);
            member.Setup(spec.Id, WeaponIds.Commando, chem);
            var agent = root.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;                                                   // SquadMember enables it on a NavMesh
                agent.radius = DebugCapsuleBody.Radius;
                agent.height = DebugCapsuleBody.Height;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;   // TECH_PLAN §7.16: squad Medium
                agent.avoidancePriority = 40;
                agent.updateRotation = false;
            }
            return root;
        }

        private static Chemlight BuildChemlight(GameObject root)
        {
            var existing = root.GetComponentInChildren<Chemlight>(true);
            if (existing != null) return existing;
            var chest = AIBuilderUtil.FindBone(root.transform, "Spine2", "Chest", "Spine1");
            var go = new GameObject("Chemlight");
            go.transform.SetParent(chest != null ? chest : root.transform, false);
            go.transform.localPosition = chest != null ? new Vector3(-0.12f, 0.08f, 0.12f) : new Vector3(-0.14f, 1.32f, 0.12f);
            var stick = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            stick.name = "Stick";
            Object.DestroyImmediate(stick.GetComponent<Collider>());
            stick.transform.SetParent(go.transform, false);
            stick.transform.localScale = new Vector3(0.025f, 0.06f, 0.025f);
            var r = stick.GetComponent<Renderer>();
            r.sharedMaterial = AIBuilderUtil.UnlitMaterial("M_AI_Chemlight", new Color(0.5f, 2.2f, 0.45f, 1f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.enabled = false;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Chemlight.Green;
            light.range = 2.5f;
            light.intensity = 0.55f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            var c = go.AddComponent<Chemlight>();
            c.Configure(r, light);
            return c;
        }
    }
}
