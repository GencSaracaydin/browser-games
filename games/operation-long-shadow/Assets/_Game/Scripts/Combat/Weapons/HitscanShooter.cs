using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Combat.Service;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// One player bullet (TECH_PLAN §7.5: hitscan <c>RaycastNonAlloc</c> on <see cref="LayerIds.PlayerShot"/>, 500 m): walks
    /// the hits nearest first, damages the first hitbox (falloff × zone multiplier or the weapon's override × armour,
    /// <see cref="DamageResolver"/>) or damageable (destructibles, vehicles), passes through thin cloth and glass at a
    /// damage cost (<see cref="HitscanRules"/>), stops at anything else, and plays the impact, decal, blood splat and
    /// physics push. Allocation-free (static buffers).
    /// </summary>
    public static class HitscanShooter
    {
        /// <summary>Hits examined per bullet.</summary>
        public const int MaxHits = 16;
        /// <summary>Impulse on loose rigidbodies (N·s).</summary>
        public const float PushImpulse = 1.6f;
        /// <summary>Distance behind a body hit searched for a wall to splat.</summary>
        public const float BloodReach = 2.5f;

        private static readonly RaycastHit[] s_hits = new RaycastHit[MaxHits];
        private static readonly RaycastHit[] s_one = new RaycastHit[1];
        private static ServiceRef<IAudioService> s_audio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_audio = default;

        /// <summary>Outcome of one bullet.</summary>
        public struct Result
        {
            /// <summary>Where the bullet stopped (or its range end).</summary>
            public Vector3 End;
            /// <summary>Something solid stopped it.</summary>
            public bool HitSomething;
            /// <summary>A damageable took damage.</summary>
            public bool HitDamageable;
            /// <summary>An enemy-side actor took damage (hit marker).</summary>
            public bool HitEnemy;
            /// <summary>The hit killed it.</summary>
            public bool Kill;
            /// <summary>The hit was a headshot.</summary>
            public bool Headshot;
            /// <summary>Damage dealt.</summary>
            public float Damage;
        }

        /// <summary>Fires one bullet of <paramref name="w"/> from <paramref name="origin"/> along <paramref name="direction"/>.</summary>
        public static Result Fire(WeaponSpec w, Vector3 origin, Vector3 direction, GameObject instigator, Team team, int mask = LayerIds.PlayerShot)
        {
            var result = new Result();
            var dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            var range = w != null && w.Range > 0f ? w.Range : WeaponTable.HitscanRange;
            result.End = origin + dir * range;
            var n = Physics.RaycastNonAlloc(origin, dir, s_hits, range, mask, QueryTriggerInteraction.Ignore);
            HitscanRules.SortByDistance(s_hits, n);
            var fx = CombatFx.Instance;
            var audio = s_audio.Value;
            var listener = fx != null ? fx.ListenerPosition : origin;
            var penetrations = 0;
            var scale = 1f;
            var instigatorRoot = instigator != null ? instigator.transform : null;
            for (var i = 0; i < n; i++)
            {
                var h = s_hits[i];
                var col = h.collider;
                if (col == null) continue;
                if (instigatorRoot != null && col.transform.IsChildOf(instigatorRoot)) continue;

                if (col.TryGetComponent<IHitbox>(out var hb))
                {
                    var owner = hb.Owner;
                    if (owner == null || !owner.IsAlive) continue; // a dying body's leftover hitbox: let the bullet pass
                    var dmg = DamageResolver.Resolve(w, h.distance, hb.Zone, hb.Multiplier, DamageResolver.ArmourOf(owner, hb.Zone), scale);
                    Apply(owner, col, dmg, hb.Zone, h.point, dir, instigator, team, w, ref result);
                    fx?.Impacts.Play(hb.Surface, h.point, h.normal, col, false, audio, listener, 0.9f);
                    if (hb.Surface == SurfaceType.Flesh) Splat(fx, h.point, dir);
                    result.End = h.point;
                    result.HitSomething = true;
                    return result;
                }

                var surface = SurfaceTag.Resolve(col);
                var damageable = CombatResolver.DamageableOf(col);
                if (damageable != null && damageable.IsAlive && !(damageable is IPlayerHealth))
                {
                    var dmg = DamageFalloff.Evaluate(w, h.distance) * scale;
                    Apply(damageable, col, dmg, HitZone.Torso, h.point, dir, instigator, team, w, ref result);
                    fx?.Impacts.Play(surface, h.point, h.normal, col, true, audio, listener);
                    result.End = h.point;
                    result.HitSomething = true;
                    return result;
                }

                fx?.Impacts.Play(surface, h.point, h.normal, col, true, audio, listener);
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(dir * PushImpulse, h.point, ForceMode.Impulse);
                if (HitscanRules.Penetrates(surface) && penetrations < HitscanRules.MaxPenetrations)
                {
                    penetrations++;
                    scale *= HitscanRules.PenetrationDamageScale;
                    continue;
                }
                result.End = h.point;
                result.HitSomething = true;
                return result;
            }
            return result;
        }

        private static void Apply(IDamageable target, Collider col, float amount, HitZone zone, Vector3 point, Vector3 dir, GameObject instigator,
                                  Team team, WeaponSpec w, ref Result result)
        {
            var wasAlive = target.IsAlive;
            target.ApplyDamage(new DamageInfo(amount, DamageType.Bullet, zone, point, dir, instigator, team, w != null ? w.Id : null));
            result.HitDamageable = true;
            result.Damage = amount;
            var enemy = Teams.IsEnemySide(target.Team);
            if (team == Team.Player)
            {
                // The actor may keep ITargetable on another component than its Health (WP04 brains).
                var t = target as ITargetable ?? (col != null ? col.GetComponentInParent<ITargetable>() : null);
                t?.NotifyEngagedByPlayer();
            }
            if (enemy || target is IDestructible)
            {
                result.HitEnemy = true;
                result.Kill = wasAlive && !target.IsAlive && enemy;
                result.Headshot = zone == HitZone.Head && enemy;
            }
        }

        private static void Splat(CombatFx fx, Vector3 point, Vector3 dir)
        {
            if (fx == null) return;
            if (Physics.RaycastNonAlloc(point + dir * 0.25f, dir, s_one, BloodReach, LayerIds.Ground, QueryTriggerInteraction.Ignore) == 0) return;
            var h = s_one[0];
            fx.Impacts.BloodSplat(h.point, h.normal, h.collider);
        }
    }
}
