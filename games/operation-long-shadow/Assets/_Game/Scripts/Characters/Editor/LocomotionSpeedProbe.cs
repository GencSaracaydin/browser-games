using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Measures the travel speed an in-place locomotion clip implies (TECH_PLAN §7.8 "in-place clips and speed matching"):
    /// samples the clip on a Humanoid body (<see cref="ClipSampler"/>) and integrates the horizontal slide of the
    /// planted (lower) foot, which moves backwards at exactly the speed the body must travel for the feet not to skate.
    /// The result places the clip in the 2D blend tree; implausible results fall back to the nominal speed.
    /// </summary>
    public static class LocomotionSpeedProbe
    {
        /// <summary>Samples per second.</summary>
        public const float SampleRate = 60f;

        /// <summary>
        /// Planted-foot speed of <paramref name="clip"/> on <paramref name="body"/> (a scene instance with an Animator and a
        /// Humanoid avatar), in m/s; 0 when it cannot be measured.
        /// </summary>
        public static float Measure(GameObject body, AnimationClip clip)
        {
            if (body == null || clip == null || clip.length < 0.05f) return 0f;
            var animator = body.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return 0f;
            var lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (lf == null || rf == null) return 0f;
            var steps = Mathf.Clamp(Mathf.CeilToInt(clip.length * SampleRate), 8, 2000);
            var dt = clip.length / steps;
            var distance = 0f;
            var time = 0f;
            try
            {
                using (var sampler = new ClipSampler(animator, clip))
                {
                    if (!sampler.IsValid) return 0f;
                    Vector3 prevL = default, prevR = default;
                    for (var i = 0; i <= steps; i++)
                    {
                        sampler.Sample(i * dt);
                        var pl = body.transform.InverseTransformPoint(lf.position);
                        var pr = body.transform.InverseTransformPoint(rf.position);
                        if (i > 0)
                        {
                            // The lower foot is the planted one; its backward slide is the travel speed.
                            var d = pl.y <= pr.y ? pl - prevL : pr - prevR;
                            d.y = 0f;
                            distance += d.magnitude;
                            time += dt;
                        }
                        prevL = pl;
                        prevR = pr;
                    }
                }
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Characters, "LocomotionSpeedProbe: sampling " + clip.name + " failed: " + e.Message);
                return 0f;
            }
            return time > 0f ? distance / time : 0f;
        }

        /// <summary>
        /// Blend position for a slot: the measured speed along the slot's nominal direction when it is within 0.5-2x of the
        /// nominal speed, else the nominal position (and a log line).
        /// </summary>
        public static Vector2 PositionFor(string key, float measured, string clipName)
        {
            var nominal = ClipCatalog.NominalVelocity(key);
            var n = nominal.magnitude;
            if (n < 1e-3f) return Vector2.zero;
            if (measured >= n * 0.5f && measured <= n * 2f) return nominal / n * measured;
            if (measured > 0f)
                LSLog.Info(LogCategory.Characters, "LocomotionSpeedProbe: " + clipName + " measured " + measured.ToString("0.00") +
                                                   " m/s for " + key + "; using the nominal " + n.ToString("0.00") + " m/s.");
            return nominal;
        }
    }
}
