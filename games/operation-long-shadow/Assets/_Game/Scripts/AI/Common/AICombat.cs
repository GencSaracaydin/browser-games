using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// The AI's only door into combat resolution: shots, grenades, rockets, explosions, smoke and pickups go through
    /// <see cref="ICombatService"/> (WP03). Without a combat service (dev arena before WP03 lands, tests) shots fall back to
    /// a minimal resolver (hit-chance roll, damage, ShotFired/Noise/TracerPath events, a debug line) so the game stays
    /// playable; everything else is skipped with one log line. Statics reset on SubsystemRegistration (R6).
    /// </summary>
    public static class AICombat
    {
        /// <summary>Noise radius of a fallback AI shot (GDD §14.2 unsuppressed shot).</summary>
        public const float FallbackNoiseRadius = 60f;
        /// <summary>Noise radius of squad gunfire (GDD §14.2).</summary>
        public const float SquadNoiseRadius = 40f;

        private static ServiceRef<ICombatService> s_combat;
        private static int s_fallbackRounds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_combat = default;
            s_fallbackRounds = 0;
        }

        /// <summary>The combat service, or null.</summary>
        public static ICombatService Service => s_combat.Value;

        /// <summary>Fires one AI round (GDD §14.3 resolution happens in WP03 CombatService).</summary>
        public static void Fire(in AIShotRequest r)
        {
            var c = s_combat.Value;
            if (c != null)
            {
                c.FireAI(r);
                return;
            }
            FallbackFire(r);
        }

        /// <summary>Throws a frag (M67, 4 s fuse) from a hand position to a point. False when nothing was thrown.</summary>
        public static bool ThrowGrenade(Vector3 from, Vector3 to, Team team)
        {
            var c = s_combat.Value;
            if (c == null)
            {
                LSLog.WarnOnce("ai-nocombat-grenade", LogCategory.AI, "No ICombatService: AI grenades are skipped until WP03 is in the scene.");
                return false;
            }
            c.ThrowGrenadeAI(from, to, team);
            return true;
        }

        /// <summary>Fires an RPG rocket. Without a combat service the impact callback runs at once at the target.</summary>
        public static void FireRocket(Vector3 from, Vector3 to, Team team, Action<Vector3> onImpact)
        {
            var c = s_combat.Value;
            if (c == null)
            {
                LSLog.WarnOnce("ai-nocombat-rocket", LogCategory.AI, "No ICombatService: AI rockets resolve instantly without effects.");
                GameEvents.RaiseShotFired(new ShotEvent { Origin = from, Direction = (to - from).normalized, Team = team, WeaponId = WeaponIds.RPG7, NoiseRadius = 80f });
                onImpact?.Invoke(to);
                return;
            }
            c.FireRocket(from, to, team, onImpact);
        }

        /// <summary>An explosion (fuel tank fireball). Skipped without a combat service.</summary>
        public static void Explode(Vector3 position, float damage, float lethalRadius, float maxRadius, GameObject instigator, Team team)
        {
            var c = s_combat.Value;
            if (c == null)
            {
                LSLog.WarnOnce("ai-nocombat-explode", LogCategory.AI, "No ICombatService: AI explosions are skipped.");
                GameEvents.RaiseExplosion(position, maxRadius, team);
                return;
            }
            c.Explode(position, damage, lethalRadius, maxRadius, instigator, team);
        }

        /// <summary>A smoke screen (B3 white smoke 20 s, B10 green smoke). Skipped without a combat service.</summary>
        public static void SpawnSmoke(Vector3 position, Color color, float seconds)
        {
            var c = s_combat.Value;
            if (c == null)
            {
                LSLog.WarnOnce("ai-nocombat-smoke", LogCategory.AI, "No ICombatService: smoke is skipped.");
                return;
            }
            c.SpawnSmoke(position, color, seconds);
        }

        /// <summary>A dropped weapon pickup (dead Legion riflemen drop AKs, GDD §13.1). Skipped without a combat service.</summary>
        public static void SpawnPickup(string weaponId, Vector3 position)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            s_combat.Value?.SpawnPickup(weaponId, position);
        }

        private static void FallbackFire(in AIShotRequest r)
        {
            LSLog.WarnOnce("ai-nocombat-fire", LogCategory.AI, "No ICombatService: AI shots use the WP04 fallback resolver (no effects).");
            var target = r.Target;
            Vector3 aim;
            if (target != null && target.AimPoint != null) aim = target.AimPoint.position;
            else if (r.Shooter != null && r.Shooter.Root != null) aim = r.Muzzle + r.Shooter.Root.forward * 50f;
            else return;
            var dir = aim - r.Muzzle;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            var hit = !r.SuppressOnly && target != null && target.IsAlive && UnityEngine.Random.value < Mathf.Clamp01(r.HitChance);
            var shooterGo = r.Shooter != null && r.Shooter.Root != null ? r.Shooter.Root.gameObject : null;
            if (hit)
            {
                target.ApplyDamage(new DamageInfo(r.Damage, DamageType.Bullet, HitZone.Torso, aim, dir, shooterGo, r.Team, r.WeaponId));
            }
            else if (r.SuppressOnly && target is ISuppressible s)
            {
                s.Suppress(2f, r.Team);
            }
            var end = hit ? aim : aim + UnityEngine.Random.insideUnitSphere * 1.5f;
            s_fallbackRounds++;
            var every = r.TracerEvery > 0 ? r.TracerEvery : 5;
            if (s_fallbackRounds % every == 0) GameEvents.RaiseTracerPath(r.Muzzle, end, r.Team);
            var noise = r.Team == Team.Squad ? SquadNoiseRadius : FallbackNoiseRadius;
            GameEvents.RaiseShotFired(new ShotEvent { Origin = r.Muzzle, Direction = dir, Team = r.Team, WeaponId = r.WeaponId, NoiseRadius = noise, Hit = hit, IsPlayer = false });
            GameEvents.RaiseNoise(new NoiseEvent(r.Muzzle, noise, r.Team, NoiseKind.Gunshot));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.DrawLine(r.Muzzle, end, hit ? Color.red : Color.yellow, 0.1f);
#endif
        }
    }
}
