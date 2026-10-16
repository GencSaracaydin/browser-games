using LongShadow.Characters.IK;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Re-poses a Rocketbox skeleton copy into a Unity Humanoid T-pose (what the avatar inspector's "Enforce T-Pose"
    /// does): upper arms, forearms and hands horizontal and straight out to the sides with the palms down, fingers 1-4
    /// straight along the hand, legs straight down. The facing comes from the clavicles, so any export orientation works.
    /// Raw Max exports bind in an A-pose; without this, every retargeted clip would hold the arms ~40° too high.
    /// </summary>
    public static class TPoseSolver
    {
        /// <summary>Applies the T-pose. Returns false (skeleton untouched) when the arm/leg chains are missing.</summary>
        public static bool Apply(PoseSkeleton s)
        {
            if (s == null) return false;
            const string l = "Bip01 L ", r = "Bip01 R ";
            if (!s.Has(l + "UpperArm") || !s.Has(r + "UpperArm") || !s.Has(l + "Thigh") || !s.Has(r + "Thigh")) return false;
            var right = s.WorldPosition(r + "UpperArm") - s.WorldPosition(l + "UpperArm");
            right.y = 0f;
            if (right.sqrMagnitude < 1e-6f) return false;
            right.Normalize();
            var up = Vector3.up;

            foreach (var side in new[] { l, r })
            {
                var dir = side == l ? -right : right;
                Align(s, side + "UpperArm", side + "Forearm", dir);
                Align(s, side + "Forearm", side + "Hand", dir);
                Align(s, side + "Hand", side + "Finger2", dir);
                PalmDown(s, side, dir);
                for (var f = 1; f <= 4; f++)
                {
                    Align(s, side + "Finger" + f, side + "Finger" + f + "1", dir);
                    Align(s, side + "Finger" + f + "1", side + "Finger" + f + "2", dir);
                }
                Align(s, side + "Thigh", side + "Calf", -up);
                Align(s, side + "Calf", side + "Foot", -up);
            }
            return true;
        }

        /// <summary>Rotates <paramref name="bone"/> so the direction to <paramref name="child"/> becomes <paramref name="dir"/>.</summary>
        public static void Align(PoseSkeleton s, string bone, string child, Vector3 dir)
        {
            var b = s.IndexOf(bone);
            var c = s.IndexOf(child);
            if (b < 0 || c < 0) return;
            var cur = s.Nodes[c].WorldPosition - s.Nodes[b].WorldPosition;
            if (cur.sqrMagnitude < 1e-10f) return;
            s.RotateWorld(b, Quaternion.FromToRotation(cur, dir));
        }

        private static void PalmDown(PoseSkeleton s, string side, Vector3 dir)
        {
            var hand = s.IndexOf(side + "Hand");
            if (hand < 0 || !s.Has(side + "Finger1") || !s.Has(side + "Finger4")) return;
            var basis = HandBasis.Compute(s.Nodes[hand].WorldPosition, s.WorldPosition(side + "Finger1"), s.WorldPosition(side + "Finger2"),
                                          s.WorldPosition(side + "Finger4"),
                                          s.Has(side + "Finger0") ? s.WorldPosition(side + "Finger0") : (Vector3?)null, Vector3.down);
            var palm = Vector3.ProjectOnPlane(basis.Palm, dir);
            var target = Vector3.ProjectOnPlane(Vector3.down, dir);
            if (palm.sqrMagnitude < 1e-8f || target.sqrMagnitude < 1e-8f) return;
            s.RotateWorld(hand, Quaternion.FromToRotation(palm, target));
        }
    }
}
