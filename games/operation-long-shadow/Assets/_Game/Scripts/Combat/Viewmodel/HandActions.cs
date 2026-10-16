using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using UnityEngine;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// Procedural left-hand actions (Black Ops style: the left hand throws the frag and strikes with the knife while the
    /// right hand keeps the gun, which dips out of the way). Pure functions of the phase and time so they are testable;
    /// positions are in view-model space (camera at the origin looking +Z, x right).
    /// </summary>
    public static class HandActions
    {
        private static readonly Vector3 ChestHold = new Vector3(-0.11f, -0.13f, 0.26f);
        private static readonly Vector3 ChestEuler = new Vector3(-20f, 30f, 10f);
        private static readonly Vector3 CookHold = new Vector3(-0.17f, -0.035f, 0.11f);
        private static readonly Vector3 CookEuler = new Vector3(-40f, 20f, 10f);
        private static readonly Vector3 ThrowEnd = new Vector3(-0.05f, 0.05f, 0.56f);
        private static readonly Vector3 ThrowEuler = new Vector3(10f, 5f, 0f);
        private static readonly Vector3 RestHand = new Vector3(-0.08f, -0.17f, 0.3f);
        private static readonly Vector3 GunDipOffset = new Vector3(0.035f, -0.075f, -0.035f);
        private static readonly Vector3 GunDipEuler = new Vector3(14f, 9f, 18f);

        private static readonly Vector3 KnifeWindup = new Vector3(-0.16f, -0.06f, 0.16f);
        private static readonly Vector3 KnifeWindupEuler = new Vector3(0f, 40f, 70f);
        private static readonly Vector3 KnifeStrike = new Vector3(0.01f, -0.03f, 0.6f);
        private static readonly Vector3 KnifeStrikeEuler = new Vector3(0f, -10f, 80f);
        private static readonly Vector3 KnifeGunOffset = new Vector3(0.085f, -0.1f, -0.05f);
        private static readonly Vector3 KnifeGunEuler = new Vector3(14f, 22f, 28f);

        /// <summary>Grenade pose for a phase, <paramref name="phaseTime"/> seconds into it.</summary>
        public static ActionPose Grenade(GrenadePhase phase, float phaseTime, GrenadeSpec spec, float time)
        {
            var p = default(ActionPose);
            if (phase == GrenadePhase.None || spec == null) return p;
            p.Active = true;
            switch (phase)
            {
                case GrenadePhase.PinPull:
                {
                    var k = CombatUtil.Smooth01(phaseTime / Mathf.Max(0.01f, spec.PinPullTime));
                    p.HandWeight = CombatUtil.Smooth01(phaseTime / Mathf.Max(0.01f, spec.PinPullTime * 0.45f));
                    p.HandPosition = Vector3.Lerp(ChestHold + new Vector3(0.02f, -0.06f, -0.04f), ChestHold, k);
                    p.HandEuler = ChestEuler;
                    p.ItemVisible = phaseTime > spec.PinPullTime * 0.2f;
                    p.WeaponOffset = GunDipOffset * k;
                    p.WeaponEuler = GunDipEuler * k;
                    break;
                }
                case GrenadePhase.Cooking:
                {
                    var k = CombatUtil.Smooth01(phaseTime / 0.18f);
                    var tremble = new Vector3(Mathf.Sin(time * 23f), Mathf.Sin(time * 17f + 1f), 0f) * 0.0012f;
                    p.HandWeight = 1f;
                    p.HandPosition = Vector3.Lerp(ChestHold, CookHold, k) + tremble;
                    p.HandEuler = Vector3.Lerp(ChestEuler, CookEuler, k);
                    p.ItemVisible = true;
                    p.WeaponOffset = GunDipOffset * (1f + 0.25f * k);
                    p.WeaponEuler = GunDipEuler * (1f + 0.2f * k);
                    break;
                }
                case GrenadePhase.Release:
                {
                    var k = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, spec.ReleaseTime));
                    var e = 1f - (1f - k) * (1f - k); // ease out: the arm whips forward
                    var arc = Mathf.Sin(k * Mathf.PI) * 0.05f;
                    p.HandWeight = 1f;
                    p.HandPosition = Vector3.Lerp(CookHold, ThrowEnd, e) + new Vector3(0f, arc, 0f);
                    p.HandEuler = Vector3.Lerp(CookEuler, ThrowEuler, e);
                    p.ItemVisible = k < 0.98f;
                    p.WeaponOffset = GunDipOffset * 1.25f;
                    p.WeaponEuler = GunDipEuler * 1.2f;
                    break;
                }
                case GrenadePhase.Recover:
                {
                    var k = CombatUtil.Smooth01(phaseTime / Mathf.Max(0.01f, spec.RecoverTime));
                    p.HandWeight = 1f - k;
                    p.HandPosition = Vector3.Lerp(ThrowEnd, RestHand, k);
                    p.HandEuler = Vector3.Lerp(ThrowEuler, ChestEuler, k);
                    p.ItemVisible = false;
                    p.WeaponOffset = GunDipOffset * 1.25f * (1f - k);
                    p.WeaponEuler = GunDipEuler * 1.2f * (1f - k);
                    break;
                }
            }
            return p;
        }

        /// <summary>Knife pose <paramref name="t"/> seconds into the 0.6 s action (GDD §13.2); <paramref name="lunge"/> stretches the stab.</summary>
        public static ActionPose Knife(float t, MeleeSpec spec, bool lunge)
        {
            var p = default(ActionPose);
            if (spec == null || t < 0f || t >= spec.Duration) return p;
            p.Active = true;
            var d = spec.Duration;
            var windupEnd = spec.HitTime * 0.5f;
            var strikeEnd = spec.HitTime + 0.05f;
            var backEnd = d * 0.7f;
            var strike = KnifeStrike + (lunge ? new Vector3(0f, 0f, 0.08f) : Vector3.zero);
            if (t < windupEnd)
            {
                var k = CombatUtil.Smooth01(t / windupEnd);
                p.HandWeight = k;
                p.HandPosition = Vector3.Lerp(RestHand, KnifeWindup, k);
                p.HandEuler = KnifeWindupEuler;
            }
            else if (t < strikeEnd)
            {
                var k = CombatUtil.Phase(t, windupEnd, strikeEnd);
                var e = 1f - (1f - k) * (1f - k) * (1f - k);
                p.HandWeight = 1f;
                p.HandPosition = Vector3.Lerp(KnifeWindup, strike, e);
                p.HandEuler = Vector3.Lerp(KnifeWindupEuler, KnifeStrikeEuler, e);
            }
            else if (t < backEnd)
            {
                var k = CombatUtil.Smooth01(CombatUtil.Phase(t, strikeEnd, backEnd));
                p.HandWeight = 1f;
                p.HandPosition = Vector3.Lerp(strike, KnifeWindup + new Vector3(0.04f, -0.05f, 0.1f), k);
                p.HandEuler = Vector3.Lerp(KnifeStrikeEuler, KnifeWindupEuler, k);
            }
            else
            {
                var k = CombatUtil.Smooth01(CombatUtil.Phase(t, backEnd, d));
                p.HandWeight = 1f - k;
                p.HandPosition = Vector3.Lerp(KnifeWindup + new Vector3(0.04f, -0.05f, 0.1f), RestHand, k);
                p.HandEuler = KnifeWindupEuler;
            }
            p.ItemVisible = t > 0.03f && t < d * 0.78f;
            var gun = CombatUtil.Smooth01(t / Mathf.Max(0.01f, windupEnd)) * (1f - CombatUtil.Smooth01(CombatUtil.Phase(t, backEnd, d)));
            p.WeaponOffset = KnifeGunOffset * gun;
            p.WeaponEuler = KnifeGunEuler * gun;
            return p;
        }
    }
}
