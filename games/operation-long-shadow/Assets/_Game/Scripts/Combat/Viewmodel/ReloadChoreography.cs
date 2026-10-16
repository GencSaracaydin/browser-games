using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// Procedural reload motion from keyed phases (GDD §13.1 "Reload animation: procedural from keyed transforms (tilt,
    /// magazine out, magazine in, charge), with left-hand IK following the separately generated magazine"). Pure function of
    /// the weapon kind, the <see cref="ReloadSpec"/> keys and the elapsed time, so EditMode tests can check every phase.
    /// <list type="bullet">
    /// <item>0 → TiltEnd: the weapon cants (top to the right), noses up and drops a little; the left hand leaves the
    /// handguard for the magazine.</item>
    /// <item>MagOut: tactical, the hand pulls the old magazine down out of frame; empty, the magazine drops free (the
    /// controller hides the attached one and spawns a physical magazine) and the hand goes to the pouch.</item>
    /// <item>MagGrab → MagIn: a fresh magazine comes up from below, lines up under the well and seats with a slap.</item>
    /// <item>ChargeStart → ChargeEnd (empty only): the hand moves to the charging handle (rifles), the bolt (RPK) or the
    /// slide (pistol), pulls it back and lets it fly.</item>
    /// <item>→ Duration: the hand returns to the handguard and the weapon rolls back.</item>
    /// </list>
    /// </summary>
    public static class ReloadChoreography
    {
        /// <summary>Per-kind amplitudes.</summary>
        public struct Style
        {
            /// <summary>Weapon offset at full tilt.</summary>
            public Vector3 TiltOffset;
            /// <summary>Weapon rotation at full tilt.</summary>
            public Vector3 TiltEuler;
            /// <summary>Distance the magazine travels down out of frame.</summary>
            public float MagDrop;
            /// <summary>Magazine rock while it leaves/enters (degrees about X).</summary>
            public float MagRock;
            /// <summary>Upward jolt of the slap (metres).</summary>
            public float SlapLift;
            /// <summary>Rotation jolt of the charge pull (degrees).</summary>
            public Vector3 ChargeEuler;
        }

        /// <summary>Default style of a weapon kind.</summary>
        public static Style StyleFor(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Pistol:
                    return new Style
                    {
                        TiltOffset = new Vector3(-0.025f, 0.01f, -0.03f), TiltEuler = new Vector3(-14f, 10f, -20f),
                        MagDrop = 0.2f, MagRock = 4f, SlapLift = 0.008f, ChargeEuler = new Vector3(-4f, -6f, -4f),
                    };
                case WeaponKind.Lmg:
                    return new Style
                    {
                        TiltOffset = new Vector3(-0.045f, -0.035f, 0.035f), TiltEuler = new Vector3(-5f, 14f, -40f),
                        MagDrop = 0.34f, MagRock = 10f, SlapLift = 0.007f, ChargeEuler = new Vector3(-3f, 4f, -7f),
                    };
                case WeaponKind.MountedMg:
                case WeaponKind.Launcher:
                case WeaponKind.HeavyMg:
                    return new Style { MagDrop = 0.3f };
                default:
                    return new Style
                    {
                        TiltOffset = new Vector3(-0.03f, -0.028f, 0.02f), TiltEuler = new Vector3(-7f, 9f, -30f),
                        MagDrop = 0.3f, MagRock = 16f, SlapLift = 0.009f, ChargeEuler = new Vector3(-4f, 3f, -6f),
                    };
            }
        }

        /// <summary>The pose at <paramref name="t"/> seconds into a reload described by <paramref name="spec"/>.</summary>
        public static ReloadPose Evaluate(WeaponKind kind, ReloadSpec spec, float t, bool empty) =>
            Evaluate(StyleFor(kind), spec, t, empty);

        /// <summary>The pose at <paramref name="t"/> seconds with an explicit style.</summary>
        public static ReloadPose Evaluate(in Style st, ReloadSpec spec, float t, bool empty)
        {
            var p = ReloadPose.Rest;
            if (spec == null || spec.Duration <= 0f || t < 0f || t >= spec.Duration) return p;
            p.Active = true;
            var charge = empty && spec.HasCharge;
            var afterSeat = charge ? spec.ChargeStart : spec.Duration;
            var settleStart = charge ? spec.ChargeEnd : spec.MagIn + 0.15f * (spec.Duration - spec.MagIn);

            // Tilt in, hold, tilt out.
            var tilt = CombatUtil.Smooth01(t / Mathf.Max(0.01f, spec.TiltEnd)) * (1f - CombatUtil.Smooth01(CombatUtil.Phase(t, settleStart, spec.Duration)));
            p.WeaponOffset = st.TiltOffset * tilt;
            p.WeaponEuler = st.TiltEuler * tilt;

            // Left hand to the magazine and back.
            var reach = CombatUtil.Smooth01(CombatUtil.Phase(t, spec.TiltEnd * 0.45f, spec.MagOut));
            var release = CombatUtil.Smooth01(CombatUtil.Phase(t, spec.MagIn, spec.MagIn + 0.35f * (afterSeat - spec.MagIn)));
            p.LeftOnMag = reach * (1f - release);

            // Magazine path.
            var rock = st.MagRock;
            if (t < spec.MagOut)
            {
                // Thumb on the release, a small push in before it comes free.
                var unseat = CombatUtil.Phase(t, spec.MagOut - 0.08f, spec.MagOut);
                p.MagOffset = new Vector3(0f, 0.004f * Mathf.Sin(unseat * Mathf.PI), 0f);
            }
            else if (t < spec.MagGrab)
            {
                var k = CombatUtil.Phase(t, spec.MagOut, spec.MagGrab);
                if (empty)
                {
                    // The old magazine fell free at MagOut; the hand goes to the pouch (its target follows a hidden mag).
                    p.MagVisible = false;
                    var down = CombatUtil.Smooth01(k);
                    p.MagOffset = new Vector3(0f, -st.MagDrop * down, -0.04f * down);
                    p.MagEuler = new Vector3(-rock * down, 0f, 0f);
                }
                else
                {
                    var down = k * k; // accelerates out of the well
                    p.MagOffset = new Vector3(0f, -st.MagDrop * down, -0.03f * down);
                    p.MagEuler = new Vector3(-rock * Mathf.Min(1f, k * 2f), 0f, 4f * k);
                    p.MagVisible = down < 0.98f;
                }
            }
            else if (t < spec.MagIn)
            {
                // A fresh magazine rises from below, lines up under the well (90 %), then seats.
                var k = CombatUtil.Phase(t, spec.MagGrab, spec.MagIn);
                const float lineUp = 0.72f;
                Vector3 offset;
                float rot;
                if (k < lineUp)
                {
                    var u = CombatUtil.Smooth01(k / lineUp);
                    offset = Vector3.Lerp(new Vector3(0f, -st.MagDrop, -0.05f), new Vector3(0f, -0.032f, -0.006f), u);
                    rot = Mathf.Lerp(-rock, -rock * 0.25f, u);
                }
                else
                {
                    var u = (k - lineUp) / (1f - lineUp);
                    u = u * u;
                    offset = Vector3.Lerp(new Vector3(0f, -0.032f, -0.006f), Vector3.zero, u);
                    rot = Mathf.Lerp(-rock * 0.25f, 0f, u);
                }
                p.MagOffset = offset;
                p.MagEuler = new Vector3(rot, 0f, 0f);
                p.MagVisible = offset.y > -st.MagDrop * 0.97f;
            }

            // Slap: a short upward jolt as the magazine seats.
            var slap = Bump(t, spec.MagIn, 0.14f);
            p.WeaponOffset += new Vector3(0f, st.SlapLift * slap, 0.003f * slap);
            p.WeaponEuler += new Vector3(-2.5f * slap, 0f, 1.5f * slap);

            // Charge (empty reloads).
            if (charge)
            {
                var len = spec.ChargeEnd - spec.ChargeStart;
                var toHandle = CombatUtil.Smooth01(CombatUtil.Phase(t, spec.ChargeStart - 0.1f * len, spec.ChargeStart + 0.35f * len));
                var away = CombatUtil.Smooth01(CombatUtil.Phase(t, spec.ChargeEnd, spec.ChargeEnd + 0.5f * (spec.Duration - spec.ChargeEnd)));
                p.LeftOnCharge = toHandle * (1f - away);
                var pull = CombatUtil.Smooth01(CombatUtil.Phase(t, spec.ChargeStart + 0.35f * len, spec.ChargeStart + 0.8f * len));
                var letGo = CombatUtil.Phase(t, spec.ChargeEnd - 0.06f * len, spec.ChargeEnd);
                p.ChargeTravel = pull * (1f - letGo);
                p.WeaponEuler += st.ChargeEuler * p.ChargeTravel;
                var home = Bump(t, spec.ChargeEnd, 0.12f);
                p.WeaponOffset += new Vector3(0f, 0.004f * home, 0.008f * home);
                p.WeaponEuler += new Vector3(-1.8f * home, 0f, 0f);
            }
            return p;
        }

        /// <summary>A 0 → 1 → 0 jolt starting at <paramref name="at"/> lasting <paramref name="length"/> seconds.</summary>
        public static float Bump(float t, float at, float length)
        {
            if (t < at || t > at + length || length <= 0f) return 0f;
            var k = (t - at) / length;
            return Mathf.Sin(k * Mathf.PI) * (1f - k);
        }
    }
}
