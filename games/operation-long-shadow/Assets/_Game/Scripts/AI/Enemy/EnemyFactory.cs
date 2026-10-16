using System;
using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// Assembles enemies from a variant: the WP04 components every enemy prefab carries (<see cref="EnemyHealth"/>,
    /// <see cref="EnemyActor"/>, <see cref="EnemyBrain"/>) plus the archetype's special behaviours. Shared by
    /// EnemyPrefabBuilder (on WP05 bodies or capsules) and by the runtime fallback (<see cref="BuildRuntimeTemplate"/>)
    /// used when LIB_Enemies has no prefab for a variant.
    /// </summary>
    public static class EnemyFactory
    {
        /// <summary>
        /// Adds (idempotently) health, actor, brain and the behaviours of the variant's archetype. The root must
        /// already carry an <see cref="ICharacterBody"/>; a plain Core <see cref="Health"/> must be removed by the caller
        /// first (it would block <see cref="EnemyHealth"/>).
        /// </summary>
        public static void AddBrainComponents(GameObject root, EnemyVariant variant, Func<Color, Material> material = null)
        {
            if (root == null || variant == null) return;
            var archetype = ArchetypeTable.Get(variant.Archetype);
            var health = root.GetComponent<EnemyHealth>();
            if (health == null) health = root.AddComponent<EnemyHealth>();
            health.ApplyArchetype(archetype.Hp, archetype.TorsoArmour, archetype.Team);
            var actor = root.GetComponent<EnemyActor>();
            if (actor == null) actor = root.AddComponent<EnemyActor>();
            actor.Setup(variant.Id, variant.Archetype);
            if (root.GetComponent<EnemyBrain>() == null) root.AddComponent<EnemyBrain>();
            switch (variant.Archetype)
            {
                case ArchetypeId.LegionRifleman:
                    Ensure<SentryBehaviour>(root);
                    break;
                case ArchetypeId.TechnicalGunner:
                    Ensure<TechnicalGunnerBehaviour>(root);
                    break;
                case ArchetypeId.GreycoatMarksman:
                    var marksman = Ensure<MarksmanBehaviour>(root);
                    var glint = root.GetComponentInChildren<ScopeGlint>(true);
                    if (glint == null)
                    {
                        var body = root.GetComponent<ICharacterBody>();
                        var head = body?.Head;
                        var go = new GameObject("ScopeGlint");
                        go.transform.SetParent(head != null && head != root.transform ? head : root.transform, false);
                        go.transform.localPosition = head != null && head != root.transform ? new Vector3(0.04f, 0f, 0.35f) : new Vector3(0.04f, 1.62f, 0.35f);
                        glint = go.AddComponent<ScopeGlint>();
                    }
                    marksman.Configure(glint);
                    break;
                case ArchetypeId.LegionRPG:
                    Ensure<RpgBehaviour>(root);
                    break;
                case ArchetypeId.RooftopTarget:
                    Ensure<RooftopTargetBehaviour>(root);
                    break;
            }
            if (variant.Nvg) Ensure<NvgBehaviour>(root);
        }

        /// <summary>
        /// A hidden, inactive capsule-soldier template for a variant (<see cref="DebugCapsuleBody"/>), parented under
        /// <paramref name="parent"/>; spawners instantiate it like a prefab.
        /// </summary>
        public static GameObject BuildRuntimeTemplate(EnemyVariant variant, Transform parent)
        {
            var go = new GameObject(variant.PrefabName + " (capsule)");
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            var team = ArchetypeTable.Get(variant.Archetype).Team;
            DebugCapsuleBody.Build(go, team, Placeholders.Material);
            AddBrainComponents(go, variant);
            // Agents are enabled by EnemyActor.Prepare at the spawn point (no "not close enough to the NavMesh" warnings).
            var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            return go;
        }

        private static T Ensure<T>(GameObject root) where T : Component
        {
            var c = root.GetComponent<T>();
            return c != null ? c : root.AddComponent<T>();
        }
    }
}
