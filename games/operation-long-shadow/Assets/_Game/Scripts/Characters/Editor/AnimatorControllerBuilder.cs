using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Characters.Animation;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Builds the WP05 animator controllers from data (TECH_PLAN §7.8, D19), rebuilt in place so their GUIDs survive:
    /// <list type="bullet">
    /// <item><b>AC_Soldier</b>: L0 Base (2D freeform directional strafe/jog/sprint tree on VelX/VelZ with an in-place turn
    /// blend at the centre, crouch tree, perch idle, airborne, one state per full-body <see cref="ActionTable"/> row, five
    /// deaths), L1 Arms (mask AM_Arms: rifle low-ready/aim, pistol, launcher holds + arm actions), L2 Hits (additive, mask
    /// AM_UpperBody, by HitDir), L3 Hands (mask AM_Hands, finger pose by HandPose).</item>
    /// <item><b>AOC_Legion / AOC_Greycoat / AOC_Squad / AOC_Civilian</b>: override controllers with the role variants of
    /// <see cref="ClipCatalog"/> (an override is skipped when its base clip is shared by a slot the role keeps).</item>
    /// <item><b>AC_ShadowProxy</b>: the player's shadow body, five states (idle/walk/run on Speed, crouch, aim) + airborne,
    /// with the rifle hold on an Arms layer.</item>
    /// <item><b>AC_FPArms</b>: a base arm pose for the IK-driven first-person arms + the finger pose layer.</item>
    /// </list>
    /// Every controller carries all of <see cref="CharacterAnimIds.AllParameters"/>. Locomotion blend positions are the
    /// measured foot speeds of each clip (<see cref="LocomotionSpeedProbe"/>). Slots whose clips have not landed use
    /// generated stand-ins; action states with a stand-in are tagged "Procedural" so HumanoidBody plays its IK gesture.
    /// </summary>
    public static class AnimatorControllerBuilder
    {
        private const float ActionIn = 0.2f;
        private const float ActionOut = 0.25f;

        /// <summary>Result of a build.</summary>
        public sealed class Report
        {
            public readonly List<string> Lines = new List<string>();
            /// <summary>Slot → clip used by AC_Soldier.</summary>
            public readonly Dictionary<string, AnimationClip> SoldierSlots = new Dictionary<string, AnimationClip>();
            public int Placeholders;
            public float MaxNativeSpeed;
            public AnimatorController Soldier, Shadow, FPArms;
            public readonly Dictionary<ControllerRole, AnimatorOverrideController> Overrides = new Dictionary<ControllerRole, AnimatorOverrideController>();

            public void Add(string line) => Lines.Add(line);
        }

        /// <summary>Builds every controller (masks and finger poses first). Writes Logs/characters_controllers.txt.</summary>
        public static Report BuildAll()
        {
            var report = new Report();
            var masks = AvatarMaskBuilder.BuildAll();
            var resolver = ClipResolver.Scan();
            if (MissingFingerPose(resolver))
            {
                FingerPoseAuthoring.BuildAll();
                resolver = ClipResolver.Scan();
            }
            report.Add("Clips indexed: " + resolver.Count);
            var probe = ProbeBody.Create();
            try
            {
                report.Soldier = BuildSoldier(resolver, probe, masks, report);
                foreach (ControllerRole role in Enum.GetValues(typeof(ControllerRole)))
                {
                    if (role == ControllerRole.Soldier) continue;
                    report.Overrides[role] = BuildOverride(role, report.Soldier, report.SoldierSlots, resolver, report);
                }
                report.Shadow = BuildShadow(resolver, probe, masks, report);
                report.FPArms = BuildFPArms(resolver, masks, report);
            }
            finally
            {
                ProbeBody.Destroy(probe);
            }
            AssetDatabase.SaveAssets();
            WriteReport(report);
            LSLog.Info(LogCategory.Characters, "AnimatorControllerBuilder: controllers built (" + report.Placeholders +
                                               " slot(s) on generated stand-ins, max native speed " + report.MaxNativeSpeed.ToString("0.00") + " m/s).");
            return report;
        }

        // ================================================================== AC_Soldier

        private static AnimatorController BuildSoldier(ClipResolver r, GameObject probe, AvatarMask[] masks, Report report)
        {
            var c = LoadOrReset(CharacterPaths.SoldierController);
            AddParameters(c);
            report.Add("== AC_Soldier");

            // ---- L0 Base
            var sm = AddLayer(c, CharacterAnimIds.LayerBase, 1f, null, AnimatorLayerBlendingMode.Override);
            var locoTree = NewTree(c, "Locomotion", BlendTreeType.FreeformDirectional2D, AnimatorParams.VelX, AnimatorParams.VelZ);
            AddIdleWithTurns(c, locoTree, r, probe, report);
            var maxSpeed = 0f;
            foreach (var key in new[] { ClipKeys.WalkFwd, ClipKeys.WalkBack, ClipKeys.WalkLeft, ClipKeys.WalkRight, ClipKeys.JogFwd,
                                        ClipKeys.JogBack, ClipKeys.JogLeft, ClipKeys.JogRight, ClipKeys.Sprint })
                maxSpeed = Mathf.Max(maxSpeed, AddDirectional(locoTree, key, r, probe, report, required: key == ClipKeys.WalkFwd || key == ClipKeys.JogFwd));
            report.MaxNativeSpeed = maxSpeed;
            var crouchTree = NewTree(c, "Crouch", BlendTreeType.FreeformDirectional2D, AnimatorParams.VelX, AnimatorParams.VelZ);
            crouchTree.AddChild(Slot(r, ClipKeys.CrouchIdle, ControllerRole.Soldier, report), Vector2.zero);
            foreach (var key in new[] { ClipKeys.CrouchFwd, ClipKeys.CrouchBack, ClipKeys.CrouchLeft, ClipKeys.CrouchRight })
                AddDirectional(crouchTree, key, r, probe, report, required: key == ClipKeys.CrouchFwd);

            var loco = NewState(sm, "Locomotion", locoTree, new Vector3(300, 0), CharacterAnimIds.TagLocomotion, footIk: true);
            var crouch = NewState(sm, "Crouch", crouchTree, new Vector3(300, 120), CharacterAnimIds.TagLocomotion, footIk: true);
            var perch = NewState(sm, "Perch", Slot(r, ClipKeys.PerchIdle, ControllerRole.Soldier, report), new Vector3(300, -120),
                                 CharacterAnimIds.TagLocomotion, footIk: true);
            var air = NewState(sm, "Airborne", Slot(r, ClipKeys.Airborne, ControllerRole.Soldier, report), new Vector3(560, 0));
            sm.defaultState = loco;
            Cond(Tr(loco, crouch, 0.25f), AnimatorConditionMode.If, 0, AnimatorParams.Crouch);
            Cond(Tr(crouch, loco, 0.25f), AnimatorConditionMode.IfNot, 0, AnimatorParams.Crouch);
            Cond(Tr(loco, perch, 0.3f), AnimatorConditionMode.If, 0, AnimatorParams.Perch);
            Cond(Tr(perch, loco, 0.3f), AnimatorConditionMode.IfNot, 0, AnimatorParams.Perch);
            foreach (var s in new[] { loco, crouch, perch }) Cond(Tr(s, air, 0.15f), AnimatorConditionMode.If, 0, AnimatorParams.Airborne);
            Cond(Tr(air, loco, 0.2f), AnimatorConditionMode.IfNot, 0, AnimatorParams.Airborne);

            // Deaths first: AnyState transitions are evaluated in order.
            for (var k = 1; k <= CharacterAnimIds.DeathCount; k++)
            {
                var key = ClipKeys.ForDeath(k);
                var st = NewState(sm, "Death_" + key, Slot(r, key, ControllerRole.Soldier, report), new Vector3(-250, 60 * k), CharacterAnimIds.TagDeath);
                Cond(Any(sm, st, 0.12f), AnimatorConditionMode.Equals, k, AnimatorParams.Death);
            }
            AddActionStates(sm, loco, ActionLayer.FullBody, r, report, new Vector3(800, 0));

            // ---- L1 Arms
            var arms = AddLayer(c, CharacterAnimIds.LayerArms, 0f, masks[1], AnimatorLayerBlendingMode.Override);
            var low = AddHoldGraph(arms, r, ControllerRole.Soldier, report);
            AddActionStates(arms, low, ActionLayer.Arms, r, report, new Vector3(800, 0));

            // ---- L2 Hits (additive)
            var hits = AddLayer(c, CharacterAnimIds.LayerHits, 1f, masks[0], AnimatorLayerBlendingMode.Additive);
            var empty = NewState(hits, "Empty", null, new Vector3(300, 0), writeDefaults: false);
            hits.defaultState = empty;
            for (var d = 0; d < 4; d++)
            {
                var key = ClipKeys.ForHitDir(d);
                var st = NewState(hits, "Hit_" + key, Slot(r, key, ControllerRole.Soldier, report), new Vector3(560, 70 * d), writeDefaults: false);
                var t = Any(hits, st, 0.05f);
                t.canTransitionToSelf = true;
                Cond(t, AnimatorConditionMode.If, 0, AnimatorParams.Hit);
                Cond(t, AnimatorConditionMode.Equals, d, AnimatorParams.HitDir);
                Tr(st, empty, 0.2f, exitTime: 0.8f);
            }

            // ---- L3 Hands
            AddHandsLayer(c, masks[2], r, report);
            EditorUtility.SetDirty(c);
            return c;
        }

        private static void AddIdleWithTurns(AnimatorController c, BlendTree loco, ClipResolver r, GameObject probe, Report report)
        {
            var idle = Slot(r, ClipKeys.Idle, ControllerRole.Soldier, report);
            var left = r.Resolve(ClipKeys.TurnLeft);
            var right = r.Resolve(ClipKeys.TurnRight);
            if (left == null || right == null)
            {
                loco.AddChild(idle, Vector2.zero);
                report.Add("  idle turns: none (turn clips missing)");
                return;
            }
            report.SoldierSlots[ClipKeys.TurnLeft] = left;
            report.SoldierSlots[ClipKeys.TurnRight] = right;
            var turn = loco.CreateBlendTreeChild(Vector2.zero);
            turn.name = "IdleTurn";
            turn.blendType = BlendTreeType.Simple1D;
            turn.blendParameter = CharacterAnimIds.Turn;
            turn.useAutomaticThresholds = false;
            turn.AddChild(left, -1f);
            turn.AddChild(idle, 0f);
            turn.AddChild(right, 1f);
            report.Add("  idle turns: " + left.name + " / " + right.name);
        }

        /// <summary>Adds a locomotion clip at its measured velocity. Returns its speed (0 when skipped).</summary>
        private static float AddDirectional(BlendTree tree, string key, ClipResolver r, GameObject probe, Report report, bool required)
        {
            var clip = r.Resolve(key);
            var placeholder = false;
            if (clip == null)
            {
                if (!required)
                {
                    report.Add("  " + key + ": (none, slot left out of the tree)");
                    return 0f;
                }
                clip = r.ResolveOrPlaceholder(key, ControllerRole.Soldier, out placeholder);
                report.Placeholders++;
            }
            report.SoldierSlots[key] = clip;
            var measured = placeholder ? 0f : LocomotionSpeedProbe.Measure(probe, clip);
            var pos = LocomotionSpeedProbe.PositionFor(key, measured, clip.name);
            foreach (var ch in tree.children)
                if ((ch.position - pos).sqrMagnitude < 0.01f)
                {
                    report.Add("  " + key + ": " + clip.name + " skipped (same blend position as " + (ch.motion != null ? ch.motion.name : "?") + ")");
                    return 0f;
                }
            tree.AddChild(clip, pos);
            report.Add("  " + key + ": " + clip.name + (placeholder ? " [stand-in]" : "") + " at (" + pos.x.ToString("0.00") + ", " +
                       pos.y.ToString("0.00") + ") m/s" + (measured > 0f ? " measured " + measured.ToString("0.00") : ""));
            return pos.magnitude;
        }

        // ================================================================== shared pieces

        private static AnimatorState AddHoldGraph(AnimatorStateMachine sm, ClipResolver r, ControllerRole role, Report report)
        {
            var low = NewState(sm, "Rifle_LowReady", Slot(r, ClipKeys.ArmsLowReady, role, report), new Vector3(300, 0));
            var aim = NewState(sm, "Rifle_Aim", Slot(r, ClipKeys.ArmsAim, role, report), new Vector3(300, 80));
            var pistol = NewState(sm, "Pistol", Slot(r, ClipKeys.ArmsPistol, role, report), new Vector3(300, 160));
            var pistolAim = NewState(sm, "Pistol_Aim", Slot(r, ClipKeys.ArmsPistolAim, role, report), new Vector3(300, 240));
            var launcher = NewState(sm, "Launcher", Slot(r, ClipKeys.ArmsLauncher, role, report), new Vector3(300, 320));
            sm.defaultState = low;
            var holds = new[]
            {
                (low, (int)WeaponHold.Rifle, (bool?)false), (aim, (int)WeaponHold.Rifle, (bool?)true),
                (pistol, (int)WeaponHold.Pistol, (bool?)false), (pistolAim, (int)WeaponHold.Pistol, (bool?)true),
                (launcher, (int)WeaponHold.Launcher, (bool?)null),
            };
            foreach (var from in holds)
            foreach (var to in holds)
            {
                if (from.Item1 == to.Item1) continue;
                var t = Tr(from.Item1, to.Item1, 0.2f);
                Cond(t, AnimatorConditionMode.Equals, to.Item2, CharacterAnimIds.Hold);
                if (to.Item3.HasValue) Cond(t, to.Item3.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, AnimatorParams.Aim);
            }
            return low;
        }

        private static void AddActionStates(AnimatorStateMachine sm, AnimatorState back, ActionLayer layer, ClipResolver r, Report report, Vector3 origin)
        {
            var arms = layer == ActionLayer.Arms;
            var trigger = arms ? CharacterAnimIds.ArmsTrigger : AnimatorParams.ActionTrigger;
            var index = arms ? CharacterAnimIds.ArmsAction : AnimatorParams.Action;
            var hold = arms ? CharacterAnimIds.ArmsHold : CharacterAnimIds.ActionHold;
            var row = 0;
            foreach (var def in ActionTable.All)
            {
                if (def.Layer != layer || !def.HasState) continue;
                var clip = r.ResolveOrPlaceholder(def.ClipKey, ControllerRole.Soldier, out var placeholder);
                if (placeholder) report.Placeholders++;
                if (!report.SoldierSlots.ContainsKey(def.ClipKey)) report.SoldierSlots[def.ClipKey] = clip;
                var speed = def.StateSpeedFor(clip.length, clip.isLooping);
                var st = NewState(sm, "Act_" + def.Id, clip, origin + new Vector3(0, 55 * row++),
                                  placeholder ? CharacterAnimIds.TagProcedural : CharacterAnimIds.TagAction, speed);
                var tin = Any(sm, st, ActionIn);
                Cond(tin, AnimatorConditionMode.If, 0, trigger);
                Cond(tin, AnimatorConditionMode.Equals, def.Index, index);
                if (def.Held)
                    Cond(Tr(st, back, ActionOut), AnimatorConditionMode.IfNot, 0, hold);
                else
                {
                    Tr(st, back, ActionOut, exitTime: 0.9f);
                    Cond(Tr(st, back, 0.15f), AnimatorConditionMode.NotEqual, def.Index, index);
                }
                report.Add("  " + (arms ? "arms " : "body ") + def.Id + " #" + def.Index + ": " + clip.name + (placeholder ? " [stand-in]" : "") +
                           (Mathf.Abs(speed - 1f) > 0.01f ? " speed x" + speed.ToString("0.00") : ""));
            }
        }

        private static void AddHandsLayer(AnimatorController c, AvatarMask handsMask, ClipResolver r, Report report)
        {
            var hands = AddLayer(c, CharacterAnimIds.LayerHands, 1f, handsMask, AnimatorLayerBlendingMode.Override);
            var y = 0;
            foreach (HandPose pose in Enum.GetValues(typeof(HandPose)))
            {
                var key = ClipKeys.ForHandPose(pose);
                var st = NewState(hands, "Pose_" + pose, Slot(r, key, ControllerRole.Soldier, report), new Vector3(300, 60 * y++));
                if (pose == HandPose.Relaxed) hands.defaultState = st;
                Cond(Any(hands, st, 0.12f), AnimatorConditionMode.Equals, (int)pose, CharacterAnimIds.HandPose);
            }
        }

        private static AnimationClip Slot(ClipResolver r, string key, ControllerRole role, Report report)
        {
            var clip = r.ResolveOrPlaceholder(key, role, out var placeholder);
            if (placeholder) report.Placeholders++;
            if (role == ControllerRole.Soldier && !report.SoldierSlots.ContainsKey(key)) report.SoldierSlots[key] = clip;
            return clip;
        }

        private static bool MissingFingerPose(ClipResolver r)
        {
            foreach (HandPose p in Enum.GetValues(typeof(HandPose)))
                if (r.Find(ClipKeys.ForHandPose(p)) == null) return true;
            return false;
        }

        // ================================================================== overrides

        private static AnimatorOverrideController BuildOverride(ControllerRole role, AnimatorController soldier, Dictionary<string, AnimationClip> slots,
                                                                 ClipResolver r, Report report)
        {
            var path = OverridePath(role);
            BuilderUtil.EnsureFolder(CharacterPaths.ControllersFolder);
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            var created = aoc == null;
            if (created) aoc = new AnimatorOverrideController(soldier) { name = Path.GetFileNameWithoutExtension(path) };
            else aoc.runtimeAnimatorController = soldier;
            var list = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
            aoc.GetOverrides(list);
            for (var i = 0; i < list.Count; i++) list[i] = new KeyValuePair<AnimationClip, AnimationClip>(list[i].Key, null);
            report.Add("== " + Path.GetFileNameWithoutExtension(path));
            foreach (var key in ClipCatalog.VariantKeys(role))
            {
                if (!slots.TryGetValue(key, out var baseClip) || baseClip == null) continue;
                var v = r.Resolve(key, role);
                if (v == null || v == baseClip) continue;
                // The override replaces the clip everywhere; skip it when another slot shares the base clip but keeps it.
                var conflict = false;
                foreach (var kv in slots)
                {
                    if (kv.Key == key || kv.Value != baseClip) continue;
                    var other = r.Resolve(kv.Key, role) ?? kv.Value;
                    if (other != v) conflict = true;
                }
                if (conflict)
                {
                    report.Add("  " + key + ": " + v.name + " skipped (" + baseClip.name + " is shared with another slot)");
                    continue;
                }
                for (var i = 0; i < list.Count; i++)
                    if (list[i].Key == baseClip) list[i] = new KeyValuePair<AnimationClip, AnimationClip>(baseClip, v);
                report.Add("  " + key + ": " + baseClip.name + " -> " + v.name);
            }
            aoc.ApplyOverrides(list);
            if (created) AssetDatabase.CreateAsset(aoc, path);
            else EditorUtility.SetDirty(aoc);
            return aoc;
        }

        /// <summary>Asset path of a role's override controller (AC_Soldier for <see cref="ControllerRole.Soldier"/>).</summary>
        public static string OverridePath(ControllerRole role)
        {
            switch (role)
            {
                case ControllerRole.Legion: return CharacterPaths.LegionOverride;
                case ControllerRole.Greycoat: return CharacterPaths.GreycoatOverride;
                case ControllerRole.Squad: return CharacterPaths.SquadOverride;
                case ControllerRole.Civilian: return CharacterPaths.CivilianOverride;
                default: return CharacterPaths.SoldierController;
            }
        }

        /// <summary>The controller asset of a role (AC_Soldier or its override), or null before the build.</summary>
        public static RuntimeAnimatorController ControllerFor(ControllerRole role) =>
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OverridePath(role));

        // ================================================================== AC_ShadowProxy (D19)

        private static AnimatorController BuildShadow(ClipResolver r, GameObject probe, AvatarMask[] masks, Report report)
        {
            var c = LoadOrReset(CharacterPaths.ShadowController);
            AddParameters(c);
            report.Add("== AC_ShadowProxy");
            var sm = AddLayer(c, CharacterAnimIds.LayerBase, 1f, null, AnimatorLayerBlendingMode.Override);
            var loco = Speed1D(c, "Locomotion", r, probe, report, ClipKeys.Idle, ClipKeys.WalkFwd, ClipKeys.JogFwd, ClipKeys.Sprint);
            var crouch = Speed1D(c, "Crouch", r, probe, report, ClipKeys.CrouchIdle, ClipKeys.CrouchFwd);
            var aim = Speed1D(c, "Aim", r, probe, report, ClipKeys.AimIdle, ClipKeys.AimWalk);
            var sLoco = NewState(sm, "Locomotion", loco, new Vector3(300, 0), CharacterAnimIds.TagLocomotion, footIk: true);
            var sCrouch = NewState(sm, "Crouch", crouch, new Vector3(300, 120), CharacterAnimIds.TagLocomotion, footIk: true);
            var sAim = NewState(sm, "Aim", aim, new Vector3(300, -120), CharacterAnimIds.TagLocomotion, footIk: true);
            var sAir = NewState(sm, "Airborne", Slot(r, ClipKeys.Airborne, ControllerRole.Soldier, report), new Vector3(560, 0));
            sm.defaultState = sLoco;
            Cond(Tr(sLoco, sCrouch, 0.25f), AnimatorConditionMode.If, 0, AnimatorParams.Crouch);
            Cond(Tr(sCrouch, sLoco, 0.25f), AnimatorConditionMode.IfNot, 0, AnimatorParams.Crouch);
            var toAim = Tr(sLoco, sAim, 0.2f);
            Cond(toAim, AnimatorConditionMode.If, 0, AnimatorParams.Aim);
            Cond(toAim, AnimatorConditionMode.IfNot, 0, AnimatorParams.Crouch);
            Cond(Tr(sAim, sLoco, 0.2f), AnimatorConditionMode.IfNot, 0, AnimatorParams.Aim);
            Cond(Tr(sAim, sCrouch, 0.25f), AnimatorConditionMode.If, 0, AnimatorParams.Crouch);
            foreach (var s in new[] { sLoco, sCrouch, sAim }) Cond(Tr(s, sAir, 0.15f), AnimatorConditionMode.If, 0, AnimatorParams.Airborne);
            Cond(Tr(sAir, sLoco, 0.2f), AnimatorConditionMode.IfNot, 0, AnimatorParams.Airborne);
            var arms = AddLayer(c, CharacterAnimIds.LayerArms, 1f, masks[1], AnimatorLayerBlendingMode.Override);
            AddHoldGraph(arms, r, ControllerRole.Soldier, report);
            EditorUtility.SetDirty(c);
            return c;
        }

        private static BlendTree Speed1D(AnimatorController c, string name, ClipResolver r, GameObject probe, Report report, params string[] keys)
        {
            var tree = NewTree(c, name, BlendTreeType.Simple1D, AnimatorParams.Speed, null);
            var last = -1f;
            for (var i = 0; i < keys.Length; i++)
            {
                var clip = i == 0 ? Slot(r, keys[i], ControllerRole.Soldier, report) : r.Resolve(keys[i]);
                if (clip == null) continue;
                var threshold = i == 0 ? 0f : LocomotionSpeedProbe.PositionFor(keys[i], LocomotionSpeedProbe.Measure(probe, clip), clip.name).magnitude;
                if (threshold <= last + 0.05f) continue;
                tree.AddChild(clip, threshold);
                last = threshold;
                report.Add("  " + name + " " + keys[i] + ": " + clip.name + " @" + threshold.ToString("0.00"));
            }
            return tree;
        }

        // ================================================================== AC_FPArms

        private static AnimatorController BuildFPArms(ClipResolver r, AvatarMask[] masks, Report report)
        {
            var c = LoadOrReset(CharacterPaths.FPArmsController);
            AddParameters(c);
            report.Add("== AC_FPArms");
            var sm = AddLayer(c, CharacterAnimIds.LayerBase, 1f, null, AnimatorLayerBlendingMode.Override);
            sm.defaultState = NewState(sm, "ArmsBase", Slot(r, ClipKeys.FPArmsBase, ControllerRole.Soldier, report), new Vector3(300, 0));
            AddHandsLayer(c, masks[2], r, report);
            EditorUtility.SetDirty(c);
            return c;
        }

        // ================================================================== utilities

        /// <summary>Fastest child speed of the base layer's Locomotion tree (HumanoidBody playback scaling), 6 m/s if unknown.</summary>
        public static float MaxNativeSpeed(AnimatorController c)
        {
            if (c == null || c.layers.Length == 0) return 6f;
            foreach (var s in c.layers[0].stateMachine.states)
            {
                if (s.state.name != "Locomotion" || !(s.state.motion is BlendTree tree)) continue;
                var max = 0f;
                foreach (var ch in tree.children) max = Mathf.Max(max, ch.position.magnitude);
                return max > 0.5f ? max : 6f;
            }
            return 6f;
        }

        private static AnimatorController LoadOrReset(string path)
        {
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c == null) c = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o != null && o != c) UnityEngine.Object.DestroyImmediate(o, true);
            c.layers = new AnimatorControllerLayer[0];
            c.parameters = new AnimatorControllerParameter[0];
            return c;
        }

        private static void AddParameters(AnimatorController c)
        {
            foreach (var p in CharacterAnimIds.AllParameters) c.AddParameter(p.Name, p.Type);
        }

        private static AnimatorStateMachine AddLayer(AnimatorController c, string name, float weight, AvatarMask mask, AnimatorLayerBlendingMode mode)
        {
            var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, c);
            c.AddLayer(new AnimatorControllerLayer
            {
                name = name, stateMachine = sm, defaultWeight = weight, avatarMask = mask, blendingMode = mode, iKPass = false,
            });
            return sm;
        }

        private static BlendTree NewTree(AnimatorController c, string name, BlendTreeType type, string paramX, string paramY)
        {
            var tree = new BlendTree { name = name, blendType = type, blendParameter = paramX, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            if (paramY != null) tree.blendParameterY = paramY;
            AssetDatabase.AddObjectToAsset(tree, c);
            return tree;
        }

        private static AnimatorState NewState(AnimatorStateMachine sm, string name, Motion motion, Vector3 pos, string tag = null, float speed = 1f,
                                              bool writeDefaults = true, bool footIk = false)
        {
            var s = sm.AddState(name, pos);
            s.motion = motion;
            if (tag != null) s.tag = tag;
            s.speed = speed;
            s.writeDefaultValues = writeDefaults;
            s.iKOnFeet = footIk;
            return s;
        }

        private static AnimatorStateTransition Tr(AnimatorState from, AnimatorState to, float duration, float exitTime = -1f)
        {
            var t = from.AddTransition(to);
            t.duration = duration;
            t.hasFixedDuration = true;
            t.hasExitTime = exitTime >= 0f;
            if (exitTime >= 0f) t.exitTime = exitTime;
            t.interruptionSource = TransitionInterruptionSource.None;
            return t;
        }

        private static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to, float duration)
        {
            var t = sm.AddAnyStateTransition(to);
            t.duration = duration;
            t.hasFixedDuration = true;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
            return t;
        }

        private static AnimatorStateTransition Cond(AnimatorStateTransition t, AnimatorConditionMode mode, float threshold, string parameter)
        {
            t.AddCondition(mode, threshold, parameter);
            return t;
        }

        private static void WriteReport(Report report)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("WP05 controller build " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                foreach (var l in report.Lines) sb.AppendLine(l);
                var file = Path.Combine(BuilderUtil.ProjectRoot, "Logs/characters_controllers.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(file) ?? ".");
                File.WriteAllText(file, sb.ToString());
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Characters, "AnimatorControllerBuilder: report not written: " + e.Message);
            }
        }
    }
}
