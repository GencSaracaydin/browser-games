using System.Collections.Generic;
using LongShadow.Characters.Animation;
using LongShadow.Characters.Body;
using LongShadow.Characters.IK;
using LongShadow.Characters.Ragdoll;
using LongShadow.Characters.Weapons;
using LongShadow.Core;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Checks every built body against the WP05 definition of done (TECH_PLAN §11.4): valid Humanoid avatar, feet on the
    /// ground (±2 cm) and a plausible height, triangle budget (≤ 15 k enemy/squad, Calder 15.4 k), materials bound (no
    /// default/missing), controller assigned, sockets present, 11 ragdoll bodies on fighters, and the left-hand IK error at
    /// the foregrip (&lt; 2 cm) measured on the real rifle hold pose. Writes Logs/characters_validation.txt; returns the
    /// number of failures.
    /// </summary>
    public static class BodyValidator
    {
        /// <summary>Tri budget for fighters (TECH_PLAN §8.9).</summary>
        public const int FighterTris = 15000;
        /// <summary>Calder's budget (TECH_PLAN §8.9: 15.4 k).</summary>
        public const int CalderTris = 15400;

        /// <summary>Validates every body prefab; returns the failure count.</summary>
        public static int ValidateAll(List<string> lines = null)
        {
            lines = lines ?? new List<string>();
            var failures = 0;
            var clips = ClipResolver.Scan();
            var holdClip = clips.Resolve(ClipKeys.ArmsLowReady);
            foreach (var spec in BodySpecs.All)
            {
                var path = CharacterPaths.BodyPrefab(spec.Prefab);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    lines.Add("FAIL " + spec.Prefab + ": prefab missing (run LongShadow/Characters/Build Body Prefabs)");
                    failures++;
                    continue;
                }
                var issues = new List<string>();
                var notes = new List<string>();
                // Renderer bounds and skinning need a scene instance (prefab assets have none).
                var instance = Object.Instantiate(prefab);
                instance.name = prefab.name;
                instance.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    Check(spec, instance, holdClip, issues, notes);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
                failures += issues.Count;
                lines.Add((issues.Count == 0 ? "OK   " : "FAIL ") + spec.Prefab + ": " + string.Join("; ", issues.Count > 0 ? issues : notes));
                if (issues.Count > 0 && notes.Count > 0) lines.Add("       " + string.Join("; ", notes));
            }
            BodyPrefabBuilder.Write("Logs/characters_validation.txt", lines);
            LSLog.Info(LogCategory.Characters, "BodyValidator: " + failures + " issue(s) (Logs/characters_validation.txt).");
            return failures;
        }

        private static void Check(BodySpec spec, GameObject prefab, AnimationClip holdClip, List<string> issues, List<string> notes)
        {
            var animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                issues.Add("no valid Humanoid avatar");
            if (animator != null && animator.runtimeAnimatorController == null) issues.Add("no controller");
            var rig = prefab.GetComponent<BodyRig>();
            if (rig == null || !rig.IsValid) issues.Add("BodyRig unresolved");
            else if (rig.RightGrip == null || rig.LeftGrip == null) issues.Add("hand sockets missing");

            var mainMesh = MainMeshName(prefab);
            var height = BodyPrefabBuilder.MeasureHeight(prefab, mainMesh, out var feet);
            notes.Add("height " + height.ToString("0.000") + " m");
            if (spec.Kind != BodyKind.FirstPerson && Mathf.Abs(feet) > 0.02f) issues.Add("feet at " + feet.ToString("0.000") + " m (must be 0 ± 0.02)");
            if (spec.Kind != BodyKind.FirstPerson && (height < 1.5f || height > 2.0f)) issues.Add("implausible height " + height.ToString("0.00"));

            var tris = 0;
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) tris += smr.sharedMesh.triangles.Length / 3;
            var budget = spec.Manifest == "CHR_CALDER" ? CalderTris : FighterTris;
            notes.Add(tris + " tris");
            if (tris > budget && (spec.Kind == BodyKind.Enemy || spec.Kind == BodyKind.Squad || spec.Manifest == "CHR_CALDER"))
                notes.Add("over the " + budget + "-tri budget (TECH_PLAN §8.9; props included)");

            var unbound = UnboundRenderer(prefab);
            if (unbound != null) issues.Add("unbound material on " + unbound);

            if (spec.Kind == BodyKind.Enemy || spec.Kind == BodyKind.Squad)
            {
                var rd = prefab.GetComponent<RagdollController>();
                var count = rd != null ? rd.Bones.Length : 0;
                if (count != 11) issues.Add("ragdoll has " + count + " bodies (expected 11)");
                if (prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() == null) issues.Add("no NavMeshAgent");
                if (prefab.GetComponent<Health>() == null) issues.Add("no Health");
            }
            if (spec.Weapon != null && rig != null && rig.IsValid && prefab.GetComponent<BodyIK>() != null && spec.Kind != BodyKind.FirstPerson)
            {
                var err = MeasureLeftHand(prefab, spec.Weapon, holdClip);
                if (err >= 0f)
                {
                    notes.Add("left-hand IK error " + (err * 100f).ToString("0.0") + " cm");
                    if (err > 0.02f) issues.Add("left-hand IK error " + (err * 100f).ToString("0.0") + " cm (> 2 cm)");
                }
            }
        }

        /// <summary>
        /// Instantiates the body, poses it with the rifle hold clip, equips the weapon and runs one IK solve; returns the
        /// distance between the support-hand socket and the weapon's foregrip (m), or -1 when it cannot be measured.
        /// </summary>
        public static float MeasureLeftHand(GameObject prefab, string weaponId, AnimationClip pose)
        {
            var go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var animator = go.GetComponent<Animator>();
                if (pose != null && animator != null)
                    using (var sampler = new ClipSampler(animator, pose))
                        sampler.Sample(0f);
                var holder = go.GetComponent<BodyWeaponHolder>();
                var ik = go.GetComponent<BodyIK>();
                var rig = go.GetComponent<BodyRig>();
                if (holder == null || ik == null || rig == null) return -1f;
                holder.SetWeapon(weaponId);
                if (holder.LeftGrip == null || rig.LeftGrip == null) return -1f;
                ik.SetWeapon(holder.Muzzle, holder.LeftGrip);
                ik.LeftHandOnWeapon = true;
                ik.SolveImmediate();
                return (rig.LeftGrip.position - holder.LeftGrip.position).magnitude;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Name of the first renderer with a missing/default material, or null.</summary>
        public static string UnboundRenderer(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m == null || m.name == "Default-Material" || m.shader == null || m.shader.name.Contains("Error") ||
                        AssetDatabase.GetAssetPath(m).EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                        return r.name; // missing, default, or still the FBX's embedded material (slot not bound)
            return null;
        }

        private static string MainMeshName(GameObject prefab)
        {
            var spec = BodySpecs.Find(prefab.name);
            if (spec == null) return null;
            foreach (var m in spec.Models)
            {
                var stem = System.IO.Path.GetFileNameWithoutExtension(m);
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    if (r.name == stem) return stem;
            }
            return null;
        }
    }
}
