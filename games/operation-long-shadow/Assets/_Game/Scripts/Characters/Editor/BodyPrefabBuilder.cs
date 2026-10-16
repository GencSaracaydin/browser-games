using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Characters.Body;
using LongShadow.Characters.Booth;
using LongShadow.Characters.FirstPerson;
using LongShadow.Characters.IK;
using LongShadow.Characters.Ragdoll;
using LongShadow.Characters.Shadow;
using LongShadow.Characters.Weapons;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Builds every body prefab of <see cref="BodySpecs"/> into Prefabs/Bodies (TECH_PLAN §8.6 step 3: "BodyPrefabBuilder →
    /// PFB_&lt;Avatar&gt;_&lt;Variant&gt; (body, materials, HumanoidBody, rig, hitboxes + ragdoll from RagdollBuilder, sockets)"),
    /// plus the D19 player bodies PFB_Calder_FP and PFB_Calder_Shadow and the booth bodies. The model is the content FBX
    /// (fallbacks in the spec), or the generated mannequin when nothing has landed (logged), so every role exists and
    /// works in a greybox. Rebuilding replaces the prefab in place (GUID kept): re-run after assets land to upgrade.
    /// Run AnimatorControllerBuilder first (controllers are assigned by path).
    /// </summary>
    public static class BodyPrefabBuilder
    {
        /// <summary>Capsule of AI bodies (GDD §12 capsule 1.8 m; agents radius 0.3).</summary>
        public const float CapsuleHeight = 1.8f;
        public const float CapsuleRadius = 0.3f;

        /// <summary>Builds every body. Returns the report lines (also in Logs/characters_bodies.txt).</summary>
        public static List<string> BuildAll()
        {
            var lines = new List<string>();
            foreach (var spec in BodySpecs.All)
            {
                try
                {
                    lines.Add(Build(spec));
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Characters, e);
                    lines.Add(spec.Prefab + ": FAILED " + e.Message);
                }
            }
            AssetDatabase.SaveAssets();
            Write("Logs/characters_bodies.txt", lines);
            LSLog.Info(LogCategory.Characters, "BodyPrefabBuilder: " + lines.Count + " body prefab(s) processed (Logs/characters_bodies.txt).");
            return lines;
        }

        /// <summary>Builds one body prefab; returns a one-line summary.</summary>
        public static string Build(BodySpec spec)
        {
            var modelPath = FirstExisting(spec.Models);
            GameObject go;
            var placeholder = false;
            if (modelPath != null)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                if (modelPath != CharacterPaths.ModelsRoot + "/" + spec.Models[0])
                    LSLog.Warn(LogCategory.Characters, spec.Prefab + ": " + spec.Models[0] + " missing; using fallback " + modelPath);
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Characters, spec.Manifest + " (" + spec.Prefab + ")", CharacterPaths.ModelsRoot + "/" + spec.Models[0]);
                go = PlaceholderMannequin.CreateInstance(spec.Prefab, spec.Placeholder);
                placeholder = true;
            }
            go.name = spec.Prefab;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            RemoveInactiveMeshes(go);

            var animator = Get<Animator>(go);
            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return spec.Prefab + ": SKIPPED (model has no Humanoid avatar: " + modelPath + ")";
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.updateMode = AnimatorUpdateMode.Normal;

            var height = MeasureHeight(go, modelPath != null ? Path.GetFileNameWithoutExtension(modelPath) : null, out var feet);
            if (spec.Kind == BodyKind.FirstPerson) height = HeightFromSkeleton(animator, height);
            var rig = Get<BodyRig>(go);
            rig.EditorSetAnimator(animator);
            rig.EditorSetHeight(height);
            rig.Resolve(true);
            if (placeholder) SetMannequinHands(rig);

            switch (spec.Kind)
            {
                case BodyKind.FirstPerson: BuildFirstPerson(go, spec, animator, rig); break;
                case BodyKind.Shadow: BuildShadow(go, spec, animator, rig); break;
                case BodyKind.Booth: BuildBooth(go, spec, animator, rig); break;
                default: BuildActor(go, spec, animator, rig); break;
            }

            var path = CharacterPaths.BodyPrefab(spec.Prefab);
            BuilderUtil.SavePrefab(go, path);
            return spec.Prefab + ": " + (placeholder ? "MANNEQUIN" : modelPath) + ", height " + height.ToString("0.000") + " m (feet " +
                   feet.ToString("+0.000;-0.000") + "), " + spec.Kind + " -> " + path;
        }

        // ------------------------------------------------------------------ kinds

        private static void BuildActor(GameObject go, BodySpec spec, Animator animator, BodyRig rig)
        {
            var squad = spec.Kind == BodyKind.Squad;
            var enemy = spec.Kind == BodyKind.Enemy;
            SetRenderers(go, LayerIds.Character, RenderingLayerIds.WorldMask | (spec.Cabin ? RenderingLayerIds.CabinMask : 0u),
                         ShadowCastingMode.On, true, false);
            go.layer = LayerIds.Character;
            animator.runtimeAnimatorController = AnimatorControllerBuilder.ControllerFor(spec.Controller);
            var events = Get<AnimEventRelay>(go);
            var ik = Get<BodyIK>(go);
            ik.EditorSetRig(rig);
            var holder = Get<BodyWeaponHolder>(go);
            holder.EditorSetRig(rig);
            NavMeshAgent agent = null;
            RagdollController ragdoll = null;
            if (enemy || squad)
            {
                var capsule = Get<CapsuleCollider>(go);
                capsule.height = CapsuleHeight;
                capsule.radius = CapsuleRadius;
                capsule.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);
                agent = Get<NavMeshAgent>(go);
                agent.radius = CapsuleRadius;
                agent.height = CapsuleHeight;
                agent.baseOffset = 0f;
                agent.speed = 3.5f;
                agent.angularSpeed = 720f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.3f;
                agent.autoBraking = true;
                agent.updateRotation = false;
                // TECH_PLAN §7.16: obstacle avoidance Low for enemies, Medium for the squad.
                agent.obstacleAvoidanceType = squad ? ObstacleAvoidanceType.MedQualityObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.avoidancePriority = squad ? 40 : 50;
                SetupHealth(go, spec);
                ragdoll = RagdollBuilder.Build(rig, animator, squad ? LayerIds.SquadHitbox : LayerIds.Hitbox, new Collider[] { capsule });
            }
            var body = Get<HumanoidBody>(go);
            body.EditorSetup(squad ? BodyRole.Squad : enemy ? BodyRole.Enemy : BodyRole.Cinematic, animator, rig, ik, holder, ragdoll, events, agent,
                             spec.Weapon, AnimatorControllerBuilder.MaxNativeSpeed(AnimatorControllerBuilder.ControllerFor(ControllerRole.Soldier) as AnimatorController));
            AddJaw(go, spec, rig);
        }

        private static void BuildBooth(GameObject go, BodySpec spec, Animator animator, BodyRig rig)
        {
            // Booth set: layer Booth, rendering layer Booth (the sun never lights the underground room), unscaled time (R3).
            SetRenderers(go, LayerIds.Booth, RenderingLayerIds.BoothMask, ShadowCastingMode.On, true, false);
            BuilderUtil.SetLayerRecursively(go, LayerIds.Booth);
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = AnimatorControllerBuilder.ControllerFor(spec.Controller);
            var events = Get<AnimEventRelay>(go);
            var ik = Get<BodyIK>(go);
            ik.EditorSetRig(rig);
            ik.EditorSetMaxDistance(1000f);
            var holder = Get<BodyWeaponHolder>(go);
            holder.EditorSetRig(rig);
            var body = Get<HumanoidBody>(go);
            body.EditorSetup(BodyRole.Booth, animator, rig, ik, holder, null, events, null, null, 6f);
            var booth = Get<BoothBody>(go);
            Renderer headRenderer = null;
            var headIndex = -1;
            Material head12 = null;
            if (spec.BoothCalder) FindMaterialSlot(go, "M_Calder_Head", out headRenderer, out headIndex, out head12);
            var head11 = spec.BoothCalder ? CharacterMaterialBuilder.Find("M_CalderReel_Head") : null;
            booth.EditorSetup(rig, body, animator, spec.BoothCalder, headRenderer, headIndex, head12, head11, new Renderer[0]);
            AddJaw(go, spec, rig);
        }

        private static void BuildShadow(GameObject go, BodySpec spec, Animator animator, BodyRig rig)
        {
            // D19: full body, shadows only, layer Player; never culled (its shadow can be on screen while it is not).
            SetRenderers(go, LayerIds.Player, RenderingLayerIds.WorldMask | RenderingLayerIds.CabinMask, ShadowCastingMode.ShadowsOnly, false, true);
            BuilderUtil.SetLayerRecursively(go, LayerIds.Player);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var shadowCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterPaths.ShadowController);
            var cinematicCtrl = AnimatorControllerBuilder.ControllerFor(spec.Controller);
            animator.runtimeAnimatorController = shadowCtrl;
            var events = Get<AnimEventRelay>(go);
            var ik = Get<BodyIK>(go);
            ik.EditorSetRig(rig);
            ik.EditorSetMaxDistance(100000f);
            var holder = Get<BodyWeaponHolder>(go);
            holder.EditorSetRig(rig);
            var body = Get<HumanoidBody>(go);
            body.EditorSetup(BodyRole.ShadowProxy, animator, rig, ik, holder, null, events, null, spec.Weapon,
                             AnimatorControllerBuilder.MaxNativeSpeed(AnimatorControllerBuilder.ControllerFor(ControllerRole.Soldier) as AnimatorController));
            body.enabled = false; // player mode; ICharacterBody calls switch it to cinematic mode (insertion)
            var proxy = Get<ShadowProxyBody>(go);
            proxy.EditorSetup(rig, ik, holder, animator, body, shadowCtrl, cinematicCtrl);
        }

        private static void BuildFirstPerson(GameObject go, BodySpec spec, Animator animator, BodyRig rig)
        {
            // D5/D19: view-model layer, receives World + Cabin + Viewmodel light, never casts shadows, never culled.
            SetRenderers(go, LayerIds.Viewmodel, RenderingLayerIds.ViewmodelReceives, ShadowCastingMode.Off, true, true);
            BuilderUtil.SetLayerRecursively(go, LayerIds.Viewmodel);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterPaths.FPArmsController);
            var hidden = new List<Transform>();
            foreach (var b in new[] { HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg })
            {
                var t = animator.GetBoneTransform(b);
                if (t != null) hidden.Add(t);
            }
            var root = go.transform;
            var eyeFromHead = rig.Eyes != null && rig.Head != null ? root.InverseTransformVector(rig.Eyes.position - rig.Head.position) : new Vector3(0f, 0.09f, 0.09f);
            var arms = Get<FPArmsIK>(go);
            arms.EditorSetup(rig, animator, hidden.ToArray(), eyeFromHead);
            WatchBuilder.Attach(rig, LayerIds.Viewmodel, RenderingLayerIds.ViewmodelReceives);
        }

        // ------------------------------------------------------------------ helpers

        private static void SetupHealth(GameObject go, BodySpec spec)
        {
            if (spec.Hp <= 0f) return;
            var health = Get<Health>(go);
            var so = new SerializedObject(health);
            so.FindProperty("_maxHp").floatValue = spec.Hp;
            so.FindProperty("_team").enumValueIndex = (int)spec.Team;
            so.FindProperty("_torsoArmour").floatValue = spec.TorsoArmour;
            so.FindProperty("_invulnerable").boolValue = spec.Kind == BodyKind.Squad; // GDD §15: the squad cannot be hurt
            so.FindProperty("_actorId").stringValue = spec.ActorId ?? "";
            var aim = BuilderUtil.Child(go.transform, "AimPoint");
            aim.localPosition = new Vector3(0f, 1.3f, 0f); // chest (Health default)
            so.FindProperty("_aimPoint").objectReferenceValue = aim;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddJaw(GameObject go, BodySpec spec, BodyRig rig)
        {
            if (string.IsNullOrEmpty(spec.Speaker) || rig.Jaw == null) return;
            var jaw = Get<JawFlap>(go);
            jaw.EditorSetup(rig, spec.Speaker, rig.Jaw.InverseTransformDirection(go.transform.right));
        }

        /// <summary>Layer, light layers and shadow mode for every renderer of a body.</summary>
        public static void SetRenderers(GameObject go, int layer, uint renderingLayers, ShadowCastingMode shadows, bool receiveShadows, bool alwaysUpdate)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.gameObject.layer = layer;
                r.renderingLayerMask = renderingLayers;
                r.shadowCastingMode = shadows;
                r.receiveShadows = receiveShadows;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = alwaysUpdate;
                    smr.quality = SkinQuality.Bone4;
                    smr.skinnedMotionVectors = false;
                }
            }
        }

        /// <summary>
        /// Standing height from the skinned vertices in the bind pose (T-pose); <paramref name="feet"/> = lowest point. Only
        /// the renderer named <paramref name="mainMesh"/> counts when it exists (hats and rigs are separate meshes).
        /// </summary>
        public static float MeasureHeight(GameObject go, string mainMesh, out float feet)
        {
            var any = false;
            var b = new Bounds();
            var renderers = new List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && mainMesh != null && r.name == mainMesh) renderers.Add(r);
            if (renderers.Count == 0)
                foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled) renderers.Add(r);
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    // Bounds of the skinned mesh in its bind pose, in world space.
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true); // scale applied: only position and rotation remain
                    var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    foreach (var v in baked.vertices)
                    {
                        var w = m.MultiplyPoint3x4(v);
                        if (!any)
                        {
                            b = new Bounds(w, Vector3.zero);
                            any = true;
                        }
                        else b.Encapsulate(w);
                    }
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                else
                {
                    if (!any)
                    {
                        b = r.bounds;
                        any = true;
                    }
                    else b.Encapsulate(r.bounds);
                }
            }
            feet = any ? b.min.y : 0f;
            return any ? b.max.y - b.min.y : PlaceholderMannequin.Height;
        }

        /// <summary>
        /// Standing height implied by the head bone (the FP arms mesh has no legs or head to measure): the Rocketbox head
        /// pivot sits at 88 % of the standing height.
        /// </summary>
        public static float HeightFromSkeleton(Animator animator, float fallback)
        {
            var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null) return fallback;
            var h = (head.position.y - animator.transform.position.y) / 0.88f;
            return h > 1.2f && h < 2.2f ? h : fallback;
        }

        private static void SetMannequinHands(BodyRig rig)
        {
            // The mannequin has no finger bones: fingers continue the arm (T-pose), palms down.
            var root = rig.transform;
            rig.EditorSetHandFrames(-root.right, -root.up, root.right, -root.up);
        }

        private static void RemoveInactiveMeshes(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t != go.transform && !t.gameObject.activeSelf && t.GetComponent<Renderer>() != null)
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        private static void FindMaterialSlot(GameObject go, string materialName, out Renderer renderer, out int index, out Material material)
        {
            renderer = null;
            index = -1;
            material = null;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith(materialName, StringComparison.Ordinal)) continue;
                    renderer = r;
                    index = i;
                    material = mats[i];
                    return;
                }
            }
        }

        private static string FirstExisting(string[] models)
        {
            if (models == null) return null;
            foreach (var m in models)
            {
                var p = CharacterPaths.ModelsRoot + "/" + m;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null) return p;
            }
            return null;
        }

        /// <summary>The component, added when missing (explicit null test: editor GetComponent returns a fake null).</summary>
        internal static T Get<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        internal static void Write(string relative, List<string> lines)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("WP05 " + Path.GetFileNameWithoutExtension(relative) + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                foreach (var l in lines) sb.AppendLine(l);
                var file = Path.Combine(BuilderUtil.ProjectRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(file) ?? ".");
                File.WriteAllText(file, sb.ToString());
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Characters, "Report not written (" + relative + "): " + e.Message);
            }
        }
    }
}
