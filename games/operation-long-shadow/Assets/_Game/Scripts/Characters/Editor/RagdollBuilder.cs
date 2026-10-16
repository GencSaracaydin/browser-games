using System.Collections.Generic;
using LongShadow.Characters.Body;
using LongShadow.Characters.Ragdoll;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Builds the ragdoll of a body from its Humanoid bones (TECH_PLAN §7.8, GDD §14.3 "ragdoll from the Ragdoll Wizard on
    /// the Biped bones"): the Ragdoll Wizard's eleven bodies (pelvis, chest, head, upper/lower arms, upper/lower legs) with
    /// its joint layout, but real masses (≈ 76 kg: pelvis 12, chest 21, head 5.5, upper arm 2.5, forearm + hand 1.8,
    /// thigh 9, calf + foot 4.6), collider sizes from the bone lengths and the measured height, and exact joint axes
    /// instead of snapped ones. The same colliders are the alive hitboxes (<see cref="Hitbox"/>: head = Head, pelvis and
    /// chest = Torso, limbs = Limb; kinematic on Hitbox or SquadHitbox), wired into <see cref="RagdollController"/>.
    /// Idempotent: existing ragdoll components on the bones are replaced.
    /// </summary>
    public static class RagdollBuilder
    {
        private sealed class Part
        {
            public HumanBodyBones Bone;
            public HumanBodyBones End; // child bone defining the length (or the bone itself)
            public HumanBodyBones Parent; // ragdoll parent body
            public float Mass;
            public float Radius; // capsule radius, or box half-width/sphere radius (m, at 1.78 m)
            public HitZone Zone;
            public int Shape; // 0 capsule, 1 box, 2 sphere
            public Vector3 TwistWorld; // primary flex axis in body space (x right, y up, z forward)
            public Vector3 SwingWorld;
            public float Low, High, Swing;
        }

        private static readonly Part[] s_parts =
        {
            new Part { Bone = HumanBodyBones.Hips, End = HumanBodyBones.Spine, Parent = HumanBodyBones.LastBone, Mass = 12f, Radius = 0.16f, Zone = HitZone.Torso, Shape = 1 },
            new Part { Bone = HumanBodyBones.LeftUpperLeg, End = HumanBodyBones.LeftLowerLeg, Parent = HumanBodyBones.Hips, Mass = 9f, Radius = 0.075f,
                       Zone = HitZone.Limb, TwistWorld = Vector3.right, SwingWorld = Vector3.forward, Low = -20f, High = 70f, Swing = 30f },
            new Part { Bone = HumanBodyBones.LeftLowerLeg, End = HumanBodyBones.LeftFoot, Parent = HumanBodyBones.LeftUpperLeg, Mass = 4.6f, Radius = 0.055f,
                       Zone = HitZone.Limb, TwistWorld = Vector3.right, SwingWorld = Vector3.forward, Low = -80f, High = 0f, Swing = 0f },
            new Part { Bone = HumanBodyBones.RightUpperLeg, End = HumanBodyBones.RightLowerLeg, Parent = HumanBodyBones.Hips, Mass = 9f, Radius = 0.075f,
                       Zone = HitZone.Limb, TwistWorld = Vector3.right, SwingWorld = Vector3.forward, Low = -20f, High = 70f, Swing = 30f },
            new Part { Bone = HumanBodyBones.RightLowerLeg, End = HumanBodyBones.RightFoot, Parent = HumanBodyBones.RightUpperLeg, Mass = 4.6f, Radius = 0.055f,
                       Zone = HitZone.Limb, TwistWorld = Vector3.right, SwingWorld = Vector3.forward, Low = -80f, High = 0f, Swing = 0f },
            new Part { Bone = HumanBodyBones.Chest, End = HumanBodyBones.Neck, Parent = HumanBodyBones.Hips, Mass = 21f, Radius = 0.18f, Zone = HitZone.Torso,
                       Shape = 1, TwistWorld = Vector3.right, SwingWorld = Vector3.forward, Low = -20f, High = 20f, Swing = 10f },
            new Part { Bone = HumanBodyBones.Head, End = HumanBodyBones.Head, Parent = HumanBodyBones.Chest, Mass = 5.5f, Radius = 0.11f, Zone = HitZone.Head,
                       Shape = 2, TwistWorld = Vector3.right, SwingWorld = Vector3.forward, Low = -40f, High = 25f, Swing = 25f },
            new Part { Bone = HumanBodyBones.LeftUpperArm, End = HumanBodyBones.LeftLowerArm, Parent = HumanBodyBones.Chest, Mass = 2.5f, Radius = 0.05f,
                       Zone = HitZone.Limb, TwistWorld = Vector3.up, SwingWorld = Vector3.forward, Low = -70f, High = 10f, Swing = 50f },
            new Part { Bone = HumanBodyBones.LeftLowerArm, End = HumanBodyBones.LeftHand, Parent = HumanBodyBones.LeftUpperArm, Mass = 1.8f, Radius = 0.042f,
                       Zone = HitZone.Limb, TwistWorld = Vector3.forward, SwingWorld = Vector3.up, Low = -90f, High = 0f, Swing = 0f },
            new Part { Bone = HumanBodyBones.RightUpperArm, End = HumanBodyBones.RightLowerArm, Parent = HumanBodyBones.Chest, Mass = 2.5f, Radius = 0.05f,
                       Zone = HitZone.Limb, TwistWorld = Vector3.down, SwingWorld = Vector3.forward, Low = -70f, High = 10f, Swing = 50f },
            new Part { Bone = HumanBodyBones.RightLowerArm, End = HumanBodyBones.RightHand, Parent = HumanBodyBones.RightUpperArm, Mass = 1.8f, Radius = 0.042f,
                       Zone = HitZone.Limb, TwistWorld = Vector3.back, SwingWorld = Vector3.up, Low = -90f, High = 0f, Swing = 0f },
        };

        /// <summary>
        /// Builds the ragdoll on <paramref name="rig"/>'s bones (call in the bind pose). <paramref name="aliveLayer"/> is
        /// Hitbox for enemies and SquadHitbox for the squad. Returns the controller (added to the rig's GameObject).
        /// </summary>
        public static RagdollController Build(BodyRig rig, Animator animator, int aliveLayer, Collider[] aliveOnly)
        {
            if (rig == null || animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                LSLog.Warn(LogCategory.Characters, "RagdollBuilder: needs a BodyRig and a Humanoid Animator.");
                return null;
            }
            var root = rig.transform;
            var scale = rig.Height / 1.78f;
            var bones = new List<RagdollBone>();
            var bodies = new Dictionary<HumanBodyBones, Rigidbody>();
            foreach (var p in s_parts)
            {
                var t = animator.GetBoneTransform(p.Bone);
                if (t == null && p.Bone == HumanBodyBones.Chest) t = animator.GetBoneTransform(HumanBodyBones.Spine);
                if (t == null) continue;
                Clean(t.gameObject);
                var end = p.End == p.Bone ? null : animator.GetBoneTransform(p.End);
                if (end == null && p.End == HumanBodyBones.Neck) end = animator.GetBoneTransform(HumanBodyBones.Head);
                // The chest box reaches down to the lumbar spine so there is no gap above the pelvis box.
                var below = p.Bone == HumanBodyBones.Chest ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
                var collider = AddCollider(t, end, below, p, root, scale);
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = p.Mass;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.3f;
                bodies[p.Bone] = rb;
                CharacterJoint joint = null;
                if (p.Parent != HumanBodyBones.LastBone && bodies.TryGetValue(p.Parent, out var parentBody))
                {
                    joint = t.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = parentBody;
                    joint.axis = t.InverseTransformDirection(root.TransformDirection(p.TwistWorld)).normalized;
                    joint.swingAxis = t.InverseTransformDirection(root.TransformDirection(p.SwingWorld)).normalized;
                    joint.lowTwistLimit = new SoftJointLimit { limit = p.Low };
                    joint.highTwistLimit = new SoftJointLimit { limit = p.High };
                    joint.swing1Limit = new SoftJointLimit { limit = p.Swing };
                    joint.swing2Limit = new SoftJointLimit { limit = 0f };
                    joint.enableProjection = true;
                    joint.enablePreprocessing = false;
                }
                var hitbox = t.gameObject.AddComponent<Hitbox>();
                hitbox.Configure(p.Zone, 0f, SurfaceType.Flesh);
                t.gameObject.layer = aliveLayer;
                bones.Add(new RagdollBone(p.Bone, t, rb, collider, joint, hitbox));
            }
            var controller = rig.GetComponent<RagdollController>();
            if (controller == null) controller = rig.gameObject.AddComponent<RagdollController>();
            controller.EditorSetup(bones.ToArray(), aliveLayer, animator, aliveOnly);
            return controller;
        }

        /// <summary>Removes ragdoll components from a bone (rebuilds).</summary>
        private static void Clean(GameObject go)
        {
            foreach (var j in go.GetComponents<CharacterJoint>()) Object.DestroyImmediate(j);
            foreach (var h in go.GetComponents<Hitbox>()) Object.DestroyImmediate(h);
            foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponents<Rigidbody>()) Object.DestroyImmediate(r);
        }

        private static Collider AddCollider(Transform bone, Transform end, Transform below, Part p, Transform root, float scale)
        {
            var r = p.Radius * scale;
            switch (p.Shape)
            {
                case 2:
                {
                    // Head: sphere centred 8 cm above the head pivot (the Biped head bone sits at the skull base).
                    var s = bone.gameObject.AddComponent<SphereCollider>();
                    s.radius = r / Mathf.Max(1e-4f, bone.lossyScale.x);
                    s.center = bone.InverseTransformPoint(bone.position + root.up * (0.08f * scale) + root.forward * (0.01f * scale));
                    return s;
                }
                case 1:
                {
                    // Pelvis/chest box: width along body right, height to the next bone, depth along body forward.
                    var hips = p.Bone == HumanBodyBones.Hips;
                    var bottom = below != null ? below.position : bone.position;
                    var top = end != null ? end.position : bone.position + root.up * (0.2f * scale);
                    var height = hips ? 0.22f * scale : Mathf.Max(0.12f * scale, Vector3.Dot(top - bottom, root.up));
                    var centreWorld = hips ? bone.position + root.up * (0.015f * scale) : bottom + root.up * (height * 0.5f);
                    var box = bone.gameObject.AddComponent<BoxCollider>();
                    box.center = bone.InverseTransformPoint(centreWorld);
                    box.size = LocalSize(bone, root.right * (r * 2f), root.up * height, root.forward * ((hips ? 0.21f : 0.24f) * scale));
                    return box;
                }
                default:
                {
                    var tip = end != null ? end.position : bone.position - root.up * (0.3f * scale);
                    var local = bone.InverseTransformPoint(tip);
                    var cap = bone.gameObject.AddComponent<CapsuleCollider>();
                    var axis = DominantAxis(local);
                    cap.direction = axis;
                    var len = Mathf.Abs(local[axis]);
                    // Forearms and calves also cover the hand/foot.
                    var extend = p.Bone == HumanBodyBones.LeftLowerArm || p.Bone == HumanBodyBones.RightLowerArm ? 1.25f
                        : p.Bone == HumanBodyBones.LeftLowerLeg || p.Bone == HumanBodyBones.RightLowerLeg ? 1.05f : 1f;
                    var c = Vector3.zero;
                    c[axis] = local[axis] * 0.5f * extend;
                    cap.center = c;
                    var s = Mathf.Max(1e-4f, bone.lossyScale.x);
                    cap.radius = r / s;
                    cap.height = Mathf.Max(len * extend, 2f * r / s);
                    return cap;
                }
            }
        }

        /// <summary>Index (0 x, 1 y, 2 z) of the largest component.</summary>
        public static int DominantAxis(Vector3 v)
        {
            var ax = Mathf.Abs(v.x);
            var ay = Mathf.Abs(v.y);
            var az = Mathf.Abs(v.z);
            return ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;
        }

        private static Vector3 LocalSize(Transform bone, Vector3 w, Vector3 h, Vector3 d)
        {
            var size = Vector3.zero;
            foreach (var world in new[] { w, h, d })
            {
                var local = bone.InverseTransformVector(world);
                var a = DominantAxis(local);
                size[a] = Mathf.Max(size[a], Mathf.Abs(local[a]));
            }
            for (var i = 0; i < 3; i++)
                if (size[i] < 1e-3f) size[i] = 0.1f;
            return size;
        }
    }
}
