using LongShadow.Characters.Animation;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Generated Humanoid stand-in clips (Art/Animation/Clips/Generated/PH_*.anim) for controller slots whose real clip
    /// has not landed yet, so every body animates believably in a greybox (TECH_PLAN: degrade gracefully). Muscle values
    /// follow Unity's conventions: arm Down-Up -1 = arm down, forearm/knee Stretch +1 = straight, upper leg Front-Back +1 =
    /// leg forward, finger Stretched -1 = curled. RootT.y is the normalised body-centre height (≈1 standing).
    /// </summary>
    public static class ProceduralClipFactory
    {
        /// <summary>Gets or (re)creates the stand-in clip of a kind.</summary>
        public static AnimationClip GetOrCreate(PlaceholderKind kind)
        {
            var path = CharacterPaths.GeneratedClipsFolder + "/PH_" + kind + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            BuilderUtil.EnsureFolder(CharacterPaths.GeneratedClipsFolder);
            clip = new AnimationClip { name = "PH_" + kind, frameRate = 30f };
            Fill(clip, kind);
            AssetDatabase.CreateAsset(clip, path);
            LSLog.Info(LogCategory.Characters, "ProceduralClipFactory: generated stand-in clip " + path);
            return clip;
        }

        /// <summary>True for generated stand-ins (state tag "Procedural" in the controllers).</summary>
        public static bool IsPlaceholder(AnimationClip clip) =>
            clip != null && AssetDatabase.GetAssetPath(clip).StartsWith(CharacterPaths.GeneratedClipsFolder, System.StringComparison.Ordinal);

        /// <summary>Writes the curves of a kind into a clip (pure; also used by tests).</summary>
        public static void Fill(AnimationClip clip, PlaceholderKind kind)
        {
            switch (kind)
            {
                case PlaceholderKind.Walk: Locomotion(clip, 1.05f, 0.35f, 0.55f, 0.22f, 0.97f, 0f); break;
                case PlaceholderKind.Jog: Locomotion(clip, 0.72f, 0.55f, 0.9f, 0.4f, 0.95f, 0.1f); break;
                case PlaceholderKind.Sprint: Locomotion(clip, 0.6f, 0.7f, 1.1f, 0.55f, 0.93f, 0.2f); break;
                case PlaceholderKind.CrouchWalk: Locomotion(clip, 1.1f, 0.25f, 0.45f, 0.1f, 0.72f, 0.25f, crouch: true); break;
                case PlaceholderKind.CrouchIdle: Static(clip, 0.7f, crouch: 0.75f, spine: 0.25f); break;
                case PlaceholderKind.ArmsRifle: Static(clip, 0.97f, armsRifle: true); break;
                case PlaceholderKind.ArmsPistol: Static(clip, 0.97f, armsRifle: true, pistol: true); break;
                case PlaceholderKind.Sit: Static(clip, 0.55f, sit: true); break;
                case PlaceholderKind.Kneel: Static(clip, 0.62f, crouch: 0.9f, spine: 0.35f, kneel: true); break;
                case PlaceholderKind.Prone: Static(clip, 0.22f, prone: true); break;
                case PlaceholderKind.Signal: Static(clip, 0.97f, signal: true); break;
                case PlaceholderKind.Slump: Static(clip, 0.85f, crouch: 0.3f, spine: 0.4f); break;
                case PlaceholderKind.Airborne: Static(clip, 1.0f, crouch: 0.35f); break;
                case PlaceholderKind.Additive: Static(clip, 0.97f); break;
                default: Static(clip, 0.97f); break;
            }
        }

        private static void Static(AnimationClip c, float height, float crouch = 0f, float spine = 0f, bool armsRifle = false, bool pistol = false,
                                   bool sit = false, bool kneel = false, bool prone = false, bool signal = false)
        {
            const float len = 1f;
            MuscleCurves.Root(c, height, prone ? Quaternion.Euler(80f, 0f, 0f) : Quaternion.identity, len);
            MuscleCurves.Constant(c, "Spine Front-Back", spine * 0.6f, len);
            MuscleCurves.Constant(c, "Chest Front-Back", spine * 0.4f, len);
            foreach (var side in new[] { "Left", "Right" })
            {
                var leg = sit ? 0.95f : crouch * 0.9f;
                if (kneel && side == "Right") leg = 0.2f;
                MuscleCurves.Constant(c, side + " Upper Leg Front-Back", leg, len);
                MuscleCurves.Constant(c, side + " Lower Leg Stretch", sit ? -0.6f : 1f - crouch * 1.6f, len);
                MuscleCurves.Constant(c, side + " Foot Up-Down", crouch * 0.3f, len);
                MuscleCurves.Constant(c, side + " Upper Leg In-Out", 0.05f, len);
                // Arms: relaxed at the sides unless a rifle/pistol/signal pose is asked for.
                var left = side == "Left";
                if (armsRifle)
                {
                    MuscleCurves.Constant(c, side + " Arm Down-Up", left ? 0.05f : -0.25f, len);
                    MuscleCurves.Constant(c, side + " Arm Front-Back", left ? 0.75f : 0.45f, len);
                    MuscleCurves.Constant(c, side + " Forearm Stretch", pistol ? 0.4f : (left ? 0.35f : -0.2f), len);
                    MuscleCurves.Constant(c, side + " Arm Twist In-Out", 0.1f, len);
                }
                else if (signal && left)
                {
                    MuscleCurves.Constant(c, side + " Arm Down-Up", 0.55f, len);
                    MuscleCurves.Constant(c, side + " Arm Front-Back", 0.25f, len);
                    MuscleCurves.Constant(c, side + " Forearm Stretch", -0.65f, len);
                }
                else
                {
                    MuscleCurves.Constant(c, side + " Arm Down-Up", prone ? 0.3f : -0.6f, len);
                    MuscleCurves.Constant(c, side + " Arm Front-Back", prone ? 0.6f : 0.05f, len);
                    MuscleCurves.Constant(c, side + " Forearm Stretch", sit ? 0.2f : 0.85f, len);
                }
            }
            MuscleCurves.SetLoop(c, true);
        }

        private static void Locomotion(AnimationClip c, float period, float legSwing, float kneeBend, float armSwing, float height, float lean,
                                       bool crouch = false)
        {
            var len = period;
            MuscleCurves.Root(c, height, Quaternion.identity, len);
            MuscleCurves.Constant(c, "Spine Front-Back", lean, len);
            MuscleCurves.Constant(c, "Chest Front-Back", lean * 0.5f, len);
            var baseLeg = crouch ? 0.6f : 0f;
            MuscleCurves.Sine(c, "Left Upper Leg Front-Back", baseLeg, legSwing, period, 0f, len);
            MuscleCurves.Sine(c, "Right Upper Leg Front-Back", baseLeg, legSwing, period, 0.5f, len);
            // The knee bends while the leg swings forward (phase +0.25).
            MuscleCurves.Sine(c, "Left Lower Leg Stretch", (crouch ? -0.2f : 1f) - kneeBend * 0.5f, kneeBend * 0.5f, period, 0.25f, len);
            MuscleCurves.Sine(c, "Right Lower Leg Stretch", (crouch ? -0.2f : 1f) - kneeBend * 0.5f, kneeBend * 0.5f, period, 0.75f, len);
            MuscleCurves.Sine(c, "Left Arm Front-Back", 0.05f, armSwing, period, 0.5f, len);
            MuscleCurves.Sine(c, "Right Arm Front-Back", 0.05f, armSwing, period, 0f, len);
            MuscleCurves.Constant(c, "Left Arm Down-Up", -0.6f, len);
            MuscleCurves.Constant(c, "Right Arm Down-Up", -0.6f, len);
            MuscleCurves.Constant(c, "Left Forearm Stretch", 0.6f, len);
            MuscleCurves.Constant(c, "Right Forearm Stretch", 0.6f, len);
            MuscleCurves.SetLoop(c, true);
        }

        /// <summary>Finger pose stand-in for a <see cref="HandPose"/> (A_Pose_* when the content FBX is missing).</summary>
        public static void FillHandPose(AnimationClip c, HandPose pose)
        {
            const float len = 1f / 15f;
            float[] right, left;
            float spread;
            switch (pose)
            {
                case HandPose.RifleGrip:
                    // Right hand on the pistol grip, index along the trigger guard; left hand wrapped on the handguard.
                    right = new[] { -0.35f, -0.25f, -0.75f, -0.8f, -0.85f };
                    left = new[] { -0.3f, -0.55f, -0.6f, -0.65f, -0.7f };
                    spread = -0.2f;
                    break;
                case HandPose.PistolGrip:
                    right = new[] { -0.4f, -0.3f, -0.85f, -0.9f, -0.9f };
                    left = new[] { -0.35f, -0.7f, -0.75f, -0.8f, -0.8f };
                    spread = -0.3f;
                    break;
                case HandPose.Fist:
                    right = left = new[] { -0.75f, -1f, -1f, -1f, -1f };
                    spread = -0.4f;
                    break;
                case HandPose.Flat:
                    right = left = new[] { 0.3f, 0.35f, 0.35f, 0.35f, 0.35f };
                    spread = 0.1f;
                    break;
                default:
                    right = left = new[] { -0.1f, -0.2f, -0.25f, -0.3f, -0.35f };
                    spread = 0f;
                    break;
            }
            MuscleCurves.Hand(c, "Right", right, spread, len);
            MuscleCurves.Hand(c, "Left", left, spread, len);
            MuscleCurves.SetLoop(c, true);
        }
    }
}
