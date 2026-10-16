using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// The knife (TECH_PLAN §7.5 Knife, GDD §13.2): V, 150 damage, 1.6 m reach plus a 1.0 m lunge, 0.6 s. The lunge
    /// extends the reach to 2.6 m when a living hostile stands inside a 22° cone with line of sight at the press (the player
    /// is not moved: IPlayerRig has no displacement call). Melee damage is flat (no zone multiplier, no armour), so one stab
    /// kills every archetype of GDD §14.1. The hit test is a sphere cast at 0.17 s.
    /// </summary>
    public sealed class Knife
    {
        private static readonly RaycastHit[] s_hits = new RaycastHit[8];
        private static readonly List<ITargetable> s_targets = new List<ITargetable>(16);
        private static readonly RaycastHit[] s_los = new RaycastHit[1];

        private readonly MeleeSpec _spec;
        private bool _hitDone;

        /// <summary>Creates a knife with <paramref name="spec"/> (WeaponTable.Knife).</summary>
        public Knife(MeleeSpec spec) => _spec = spec ?? WeaponTable.Knife;

        /// <summary>True during the 0.6 s action.</summary>
        public bool Active { get; private set; }
        /// <summary>Seconds into the action.</summary>
        public float Time { get; private set; }
        /// <summary>The current strike lunges.</summary>
        public bool Lunge { get; private set; }
        /// <summary>The spec.</summary>
        public MeleeSpec Spec => _spec;

        /// <summary>Outcome of the strike.</summary>
        public struct Strike
        {
            /// <summary>The strike happened this tick.</summary>
            public bool Happened;
            /// <summary>Something was hit.</summary>
            public bool Hit;
            /// <summary>A damageable took the damage.</summary>
            public bool HitActor;
            /// <summary>It died.</summary>
            public bool Kill;
            /// <summary>Hit point.</summary>
            public Vector3 Point;
            /// <summary>Hit normal.</summary>
            public Vector3 Normal;
            /// <summary>Surface hit.</summary>
            public SurfaceType Surface;
            /// <summary>Hit collider.</summary>
            public Collider Collider;
        }

        /// <summary>Starts a strike from <paramref name="eye"/>; <paramref name="registry"/> finds lunge targets (may be null).</summary>
        public void Begin(Ray eye, ITargetRegistry registry)
        {
            Active = true;
            Time = 0f;
            _hitDone = false;
            Lunge = FindLungeTarget(eye, registry);
        }

        /// <summary>Advances; performs the hit test at the spec's hit time.</summary>
        public Strike Tick(float dt, Ray eye, GameObject instigator, Team team)
        {
            var s = default(Strike);
            if (!Active) return s;
            Time += dt;
            if (!_hitDone && Time >= _spec.HitTime)
            {
                _hitDone = true;
                s = Hit(eye, instigator, team);
                s.Happened = true;
            }
            if (Time >= _spec.Duration) Active = false;
            return s;
        }

        /// <summary>Stops the action (checkpoint, mount).</summary>
        public void Cancel()
        {
            Active = false;
            Time = 0f;
        }

        private Strike Hit(Ray eye, GameObject instigator, Team team)
        {
            var s = default(Strike);
            var reach = _spec.Reach + (Lunge ? _spec.Lunge : 0f);
            var n = Physics.SphereCastNonAlloc(eye.origin, _spec.SweepRadius, eye.direction, s_hits, reach, LayerIds.PlayerShot, QueryTriggerInteraction.Ignore);
            var best = -1;
            var root = instigator != null ? instigator.transform : null;
            for (var i = 0; i < n; i++)
            {
                var c = s_hits[i].collider;
                if (c == null || (root != null && c.transform.IsChildOf(root))) continue;
                if (s_hits[i].distance <= 0f && s_hits[i].point == Vector3.zero) continue; // started overlapping
                if (best < 0 || s_hits[i].distance < s_hits[best].distance) best = i;
            }
            if (best < 0) return s;
            var h = s_hits[best];
            s.Hit = true;
            s.Point = h.point;
            s.Normal = h.normal;
            s.Collider = h.collider;
            s.Surface = SurfaceTag.Resolve(h.collider);
            var target = Service.CombatResolver.DamageableOf(h.collider);
            if (target != null && target.IsAlive && !(target is IPlayerHealth))
            {
                var wasAlive = target.IsAlive;
                // GDD §13.2: 150, flat; zone Torso so no multiplier is applied downstream.
                target.ApplyDamage(new DamageInfo(_spec.Damage, DamageType.Melee, HitZone.Torso, h.point, eye.direction, instigator, team, WeaponIds.Knife));
                if (team == Team.Player) (target as ITargetable ?? h.collider.GetComponentInParent<ITargetable>())?.NotifyEngagedByPlayer();
                s.HitActor = true;
                s.Kill = wasAlive && !target.IsAlive;
                if (h.collider.TryGetComponent<IHitbox>(out var hb)) s.Surface = hb.Surface;
            }
            return s;
        }

        private bool FindLungeTarget(Ray eye, ITargetRegistry registry)
        {
            if (registry == null) return false;
            registry.GetHostilesOf(Team.Player, s_targets);
            var maxReach = _spec.Reach + _spec.Lunge;
            var found = false;
            for (var i = 0; i < s_targets.Count && !found; i++)
            {
                var t = s_targets[i];
                if (t == null || !t.IsAlive) continue;
                var aim = t.AimPoint != null ? t.AimPoint.position : t.Root != null ? t.Root.position + Vector3.up * 1.2f : Vector3.zero;
                if (!MathX.InCone(eye.origin, eye.direction, _spec.LungeConeDegrees, maxReach, aim)) continue;
                var d = aim - eye.origin;
                var len = d.magnitude;
                if (len < 0.05f) continue;
                found = Physics.RaycastNonAlloc(eye.origin, d / len, s_los, Mathf.Max(0f, len - 0.35f), LayerIds.ExplosionCover, QueryTriggerInteraction.Ignore) == 0;
            }
            s_targets.Clear();
            return found;
        }
    }
}
