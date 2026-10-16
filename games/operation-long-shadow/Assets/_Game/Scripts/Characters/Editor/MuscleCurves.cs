using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Writes Humanoid muscle curves into an <see cref="AnimationClip"/> (generated placeholders and finger poses). Muscle
    /// names are Unity's <see cref="HumanTrait.MuscleName"/> ("Left Upper Leg Front-Back"); finger muscles use the
    /// clip attribute form ("Left Index 1 Stretched" → "LeftHand.Index.1 Stretched"). Unknown names are ignored with a
    /// warning so a typo can never produce a broken clip.
    /// </summary>
    public static class MuscleCurves
    {
        private static readonly string[] s_fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        private static HashSet<string> s_known;

        /// <summary>Clip attribute name of a muscle.</summary>
        public static string AttributeName(string muscle)
        {
            if (string.IsNullOrEmpty(muscle)) return muscle;
            foreach (var side in new[] { "Left", "Right" })
            {
                if (!muscle.StartsWith(side + " ", StringComparison.Ordinal)) continue;
                var rest = muscle.Substring(side.Length + 1);
                foreach (var f in s_fingers)
                {
                    if (!rest.StartsWith(f + " ", StringComparison.Ordinal)) continue;
                    var tail = rest.Substring(f.Length + 1); // "1 Stretched" | "Spread"
                    return side + "Hand." + f + "." + tail;
                }
            }
            return muscle;
        }

        /// <summary>True when Unity knows the muscle.</summary>
        public static bool IsMuscle(string muscle)
        {
            if (s_known == null) s_known = new HashSet<string>(HumanTrait.MuscleName);
            return s_known.Contains(muscle);
        }

        /// <summary>Sets a constant muscle value.</summary>
        public static void Constant(AnimationClip clip, string muscle, float value, float length) =>
            Set(clip, muscle, AnimationCurve.Constant(0f, length, value));

        /// <summary>Sets a sine-driven muscle (offset + amplitude·sin(2π(t/period + phase))) sampled at 8 keys per period.</summary>
        public static void Sine(AnimationClip clip, string muscle, float offset, float amplitude, float period, float phase, float length)
        {
            var curve = new AnimationCurve();
            var steps = Mathf.Max(8, Mathf.CeilToInt(length / period * 8f));
            for (var i = 0; i <= steps; i++)
            {
                var t = length * i / steps;
                curve.AddKey(t, offset + amplitude * Mathf.Sin(2f * Mathf.PI * (t / period + phase)));
            }
            for (var i = 0; i < curve.length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            Set(clip, muscle, curve);
        }

        /// <summary>Sets a muscle curve.</summary>
        public static void Set(AnimationClip clip, string muscle, AnimationCurve curve)
        {
            if (!IsMuscle(muscle))
            {
                LSLog.WarnOnce("muscle:" + muscle, LogCategory.Characters, "MuscleCurves: unknown muscle '" + muscle + "' skipped.");
                return;
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), AttributeName(muscle)), curve);
        }

        /// <summary>Root pose curves (RootT in normalised human units, RootQ as a quaternion).</summary>
        public static void Root(AnimationClip clip, float height, Quaternion rotation, float length)
        {
            void C(string attr, float v) => AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), attr),
                                                                           AnimationCurve.Constant(0f, length, v));
            C("RootT.x", 0f);
            C("RootT.y", height);
            C("RootT.z", 0f);
            C("RootQ.x", rotation.x);
            C("RootQ.y", rotation.y);
            C("RootQ.z", rotation.z);
            C("RootQ.w", rotation.w);
        }

        /// <summary>Marks a clip as looping (or not).</summary>
        public static void SetLoop(AnimationClip clip, bool loop)
        {
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop;
            s.loopBlend = loop;
            s.keepOriginalOrientation = false;
            s.keepOriginalPositionXZ = false;
            s.keepOriginalPositionY = false;
            s.loopBlendOrientation = true;
            s.loopBlendPositionY = true;
            s.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
        }

        /// <summary>Finger curl for one hand: <paramref name="curl"/> per finger (thumb..little; -1 curled, +1 stretched).</summary>
        public static void Hand(AnimationClip clip, string side, float[] curl, float spread, float length)
        {
            for (var f = 0; f < s_fingers.Length; f++)
            {
                var c = curl != null && f < curl.Length ? curl[f] : 0f;
                var name = side + " " + s_fingers[f];
                Constant(clip, name + " 1 Stretched", c, length);
                Constant(clip, name + " 2 Stretched", f == 0 ? c * 0.8f : c, length);
                Constant(clip, name + " 3 Stretched", f == 0 ? c * 0.7f : c * 0.9f, length);
                Constant(clip, name + " Spread", f == 0 ? spread * 0.5f : spread, length);
            }
        }
    }
}
