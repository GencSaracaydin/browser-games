using System.Collections.Generic;
using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Service
{
    /// <summary>
    /// Combat rules shared by <see cref="CombatService"/>, grenades, rockets, destructibles and player weapons: explosions
    /// with linear falloff and a cover raycast to head and chest (GDD §13.2, TECH_PLAN §7.4), AI shot resolution (GDD §14.3:
    /// roll the hit chance; squad hits are VFX only; misses at the player land 0.5-3 m off his head and crack past him), and
    /// the collider → damageable lookup. Allocation-free after warm-up; buffers are static.
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>Layers whose colliders can belong to a damageable caught in a blast.</summary>
        public const int ExplosionQueryMask = (1 << LayerIds.Hitbox) | (1 << LayerIds.SquadHitbox) | (1 << LayerIds.Character) |
                                              (1 << LayerIds.Player) | (1 << LayerIds.Vehicle) | (1 << LayerIds.Debris);
        /// <summary>Layers pushed by blasts (ragdolls, debris, loose projectiles).</summary>
        public const int PushMask = (1 << LayerIds.Ragdoll) | (1 << LayerIds.Debris) | (1 << LayerIds.Projectile);
        /// <summary>Height above the aim point (chest) used as the head for cover tests.</summary>
        public const float HeadAboveChest = 0.38f;
        /// <summary>Range of an AI shot's ray when it misses.</summary>
        public const float AiShotRange = 300f;

        private static readonly Collider[] s_overlap = new Collider[96];
        private static readonly RaycastHit[] s_hits = new RaycastHit[1];
        private static readonly List<IDamageable> s_damaged = new List<IDamageable>(24);
        private static readonly List<Rigidbody> s_pushed = new List<Rigidbody>(32);
        private static readonly List<IDestructible> s_destructibles = new List<IDestructible>(32);
        private static readonly Dictionary<ITargetable, int> s_tracerCount = new Dictionary<ITargetable, int>(32);
        private static int s_anonTracer;
        private static ServiceRef<IAudioService> s_audio;
        private static ServiceRef<IPlayerRig> s_rig;
        private static ServiceRef<ITargetRegistry> s_targets;
        private static ServiceRef<IPlayerHealth> s_playerHealth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_tracerCount.Clear();
            s_anonTracer = 0;
            s_damaged.Clear();
            s_pushed.Clear();
            s_destructibles.Clear();
            s_audio = default;
            s_rig = default;
            s_targets = default;
            s_playerHealth = default;
        }

        /// <summary>The damageable a collider belongs to (hitbox owner, else the nearest parent damageable), or null.</summary>
        public static IDamageable DamageableOf(Collider c)
        {
            if (c == null) return null;
            if (c.TryGetComponent<IHitbox>(out var hb)) return hb.Owner;
            return c.GetComponentInParent<IDamageable>();
        }

        /// <summary>The player as a damageable (health service, else the registry's player), or null.</summary>
        public static IDamageable PlayerDamageable
        {
            get
            {
                var h = s_playerHealth.Value;
                if (h != null) return h;
                return s_targets.Value?.Player;
            }
        }

        /// <summary>True when <paramref name="d"/> is the player.</summary>
        public static bool IsPlayer(IDamageable d)
        {
            if (d == null) return false;
            if (d is IPlayerHealth) return true;
            var p = s_targets.Value?.Player;
            return p != null && ReferenceEquals(p, d);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Explosions
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Explosion: VFX, light, scorch, sound, damage with linear falloff from <paramref name="lethalRadius"/> to
        /// <paramref name="maxRadius"/> to every damageable whose head or chest is not covered (raycast on
        /// <see cref="LayerIds.ExplosionCover"/>), destructibles, ragdoll/debris impulse, camera shake, tinnitus, AI noise and
        /// <see cref="GameEvents.Explosion"/>. <paramref name="exclude"/> (a rocket's direct-hit victim) takes no splash.
        /// </summary>
        public static void Explode(Vector3 position, float damage, float lethalRadius, float maxRadius, GameObject instigator, Team team,
                                   string weaponId, string fxId, string cueId, float noiseRadius, IDamageable exclude)
        {
            var fx = CombatFx.Instance;
            var groundNormal = Vector3.up;
            Collider groundCollider = null;
            if (Physics.Raycast(position + Vector3.up * 0.3f, Vector3.down, out var gh, 2.5f, LayerIds.Ground, QueryTriggerInteraction.Ignore))
            {
                groundNormal = gh.normal;
                groundCollider = gh.collider;
            }
            if (fx != null)
            {
                fx.Explosions.Play(fxId, position, groundNormal);
                fx.Lights.Flash(position + Vector3.up * 0.6f, new Color(1f, 0.62f, 0.32f), fxId == FxIds.ExplosionFireball ? 18f : 12f,
                    Mathf.Max(10f, maxRadius * 2.5f), fxId == FxIds.ExplosionFireball ? 0.5f : 0.2f);
                if (groundCollider != null) fx.Impacts.Scorch(gh.point, groundNormal, Mathf.Clamp(maxRadius * 0.45f, 1.6f, 4f), groundCollider);
            }
            var audio = s_audio.Value;
            if (audio != null && !string.IsNullOrEmpty(cueId)) audio.Play(cueId, position);

            var byPlayer = team == Team.Player;
            var anyHit = false;
            var anyKill = false;

            // Damageables in range (deduplicated by owner).
            s_damaged.Clear();
            var n = Physics.OverlapSphereNonAlloc(position, maxRadius, s_overlap, ExplosionQueryMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
            {
                var d = DamageableOf(s_overlap[i]);
                if (d == null || ReferenceEquals(d, exclude) || !d.IsAlive || s_damaged.Contains(d)) continue;
                s_damaged.Add(d);
            }
            var player = PlayerDamageable;
            if (player != null && player.IsAlive && !ReferenceEquals(player, exclude) && !s_damaged.Contains(player)) s_damaged.Add(player);

            for (var i = 0; i < s_damaged.Count; i++)
            {
                var d = s_damaged[i];
                var chest = d.AimPoint != null ? d.AimPoint.position : (d is Component c ? c.transform.position + Vector3.up * 1.3f : position);
                var dist = Vector3.Distance(position, chest);
                if (dist > maxRadius) continue;
                if (IsCovered(position, chest)) continue;
                var amount = ExplosionMath.DamageAt(dist, damage, lethalRadius, maxRadius);
                if (amount <= 0f) continue;
                var dir = chest - position;
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
                var wasAlive = d.IsAlive;
                d.ApplyDamage(new DamageInfo(amount, DamageType.Explosion, HitZone.Torso, chest, dir, instigator, team, weaponId));
                if (byPlayer && !IsPlayer(d) && Teams.IsEnemySide(d.Team))
                {
                    anyHit = true;
                    if (wasAlive && !d.IsAlive) anyKill = true;
                    (d as ITargetable)?.NotifyEngagedByPlayer();
                }
            }
            s_damaged.Clear();

            // Destructibles (racks, floods, trucks) by registry: cheap and independent of their layers.
            Destructibles.GetAll(s_destructibles);
            for (var i = 0; i < s_destructibles.Count; i++)
            {
                if (!(s_destructibles[i] is IDamageable dd) || ReferenceEquals(dd, exclude) || !dd.IsAlive) continue;
                var target = dd.AimPoint != null ? dd.AimPoint.position : s_destructibles[i].Root.position;
                var dist = Vector3.Distance(position, target);
                if (dist > maxRadius) continue;
                var amount = ExplosionMath.DamageAt(dist, damage, lethalRadius, maxRadius);
                if (amount <= 0f) continue;
                var dir = (target - position).sqrMagnitude > 1e-6f ? (target - position).normalized : Vector3.up;
                dd.ApplyDamage(new DamageInfo(amount, DamageType.Explosion, HitZone.Torso, target, dir, instigator, team, weaponId));
            }
            s_destructibles.Clear();

            // Physics push.
            s_pushed.Clear();
            n = Physics.OverlapSphereNonAlloc(position, maxRadius * 1.25f, s_overlap, PushMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
            {
                var rb = s_overlap[i].attachedRigidbody;
                if (rb == null || rb.isKinematic || s_pushed.Contains(rb)) continue;
                s_pushed.Add(rb);
                var impulse = ExplosionMath.Impulse(Vector3.Distance(position, rb.worldCenterOfMass), maxRadius) * Mathf.Clamp(rb.mass, 0.2f, 12f);
                if (impulse > 0f) rb.AddExplosionForce(impulse, position, maxRadius * 1.25f, 0.5f, ForceMode.Impulse);
            }
            s_pushed.Clear();

            // The player's senses.
            var rig = s_rig.Value;
            if (rig != null && rig.Eye != null)
            {
                var pd = Vector3.Distance(rig.Eye.position, position);
                var trauma = ExplosionMath.Trauma(pd, maxRadius);
                if (trauma > 0.01f) rig.Shake(trauma);
                var tinnitus = ExplosionMath.TinnitusSeconds(pd, WeaponTable.Frag.TinnitusRadius);
                if (tinnitus > 0f && audio != null && !IsCovered(position, rig.Eye.position)) audio.Tinnitus(tinnitus);
            }

            if (anyHit) GameEvents.RaiseHitConfirmed(anyKill, false);
            GameEvents.RaiseExplosion(position, maxRadius, team);
            if (noiseRadius > 0f) GameEvents.RaiseNoise(new NoiseEvent(position, noiseRadius, team, NoiseKind.Explosion));
        }

        /// <summary>True when both the chest and the head above it are blocked from the blast centre.</summary>
        public static bool IsCovered(Vector3 center, Vector3 chest)
        {
            var head = chest + Vector3.up * HeadAboveChest;
            return Blocked(center, chest) && Blocked(center, head);
        }

        private static bool Blocked(Vector3 from, Vector3 to)
        {
            var d = to - from;
            var len = d.magnitude;
            if (len < 0.05f) return false;
            // Stop 0.15 m short so the target's own collider (or the wall it leans on) does not count.
            return Physics.RaycastNonAlloc(from, d / len, s_hits, Mathf.Max(0f, len - 0.15f), LayerIds.ExplosionCover, QueryTriggerInteraction.Ignore) > 0;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // AI shots
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Resolves one AI shot (GDD §14.3): sound, muzzle flash, ShotFired/Noise, hit-chance roll, damage (squad: VFX only /
        /// SuppressOnly → suppression), misses near the player 0.5-3 m off his head with crack/whizz and NearMiss, rounds
        /// within 2 m suppress him, every N-th round draws a tracer.
        /// </summary>
        public static void FireAI(in AIShotRequest r)
        {
            var fx = CombatFx.Instance;
            var audio = s_audio.Value;
            var spec = WeaponTable.GetEnemy(r.WeaponId);
            var shooterGo = r.Shooter != null && r.Shooter.Root != null ? r.Shooter.Root.gameObject : null;
            var target = r.Target;
            Vector3 aim;
            if (target != null && target.AimPoint != null) aim = target.AimPoint.position;
            else if (r.Shooter != null && r.Shooter.Root != null) aim = r.Muzzle + r.Shooter.Root.forward * 50f;
            else return;
            var dir = aim - r.Muzzle;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;

            // Signature.
            audio?.PlayWeaponShot(r.WeaponId, r.Muzzle, false);
            if (fx != null)
            {
                var flash = spec?.MuzzleFlashScale ?? 1f;
                fx.Muzzle.FlashWorld(r.Muzzle, dir, flash);
                if (fx.WithinLightRange(r.Muzzle)) fx.Lights.Flash(r.Muzzle + dir * 0.1f, new Color(1f, 0.78f, 0.5f), 3.5f * flash, 4f, 0.05f);
            }
            var noise = r.Team == Team.Squad ? WeaponTable.SquadNoiseRadius : spec?.NoiseRadius ?? 60f;

            var isPlayerTarget = IsPlayer(target);
            var hit = !r.SuppressOnly && target != null && target.IsAlive && Random.value < Mathf.Clamp01(r.HitChance);
            Vector3 end;
            if (hit)
            {
                end = aim + Random.insideUnitSphere * 0.12f;
                var info = new DamageInfo(r.Damage, DamageType.Bullet, HitZone.Torso, end, dir, shooterGo, r.Team, r.WeaponId);
                target.ApplyDamage(info);
                if (!isPlayerTarget && fx != null) fx.Impacts.Play(SurfaceType.Flesh, end, -dir, null, false, audio, fx.ListenerPosition, 0.8f);
            }
            else
            {
                Vector3 missPoint;
                var rig = s_rig.Value;
                if (isPlayerTarget && rig != null && rig.Eye != null)
                    missPoint = NearMissMath.MissPoint(rig.Eye.position, dir, WeaponTable.AiMissMin, WeaponTable.AiMissMax, Random.value, Random.value);
                else
                    missPoint = NearMissMath.MissPoint(aim, dir, r.SuppressOnly ? 0.3f : 0.4f, r.SuppressOnly ? 1.2f : 2f, Random.value, Random.value);
                var mdir = missPoint - r.Muzzle;
                mdir = mdir.sqrMagnitude > 1e-6f ? mdir.normalized : dir;
                end = r.Muzzle + mdir * AiShotRange;
                Collider hitCol = null;
                if (Physics.RaycastNonAlloc(r.Muzzle + mdir * 0.3f, mdir, s_hits, AiShotRange, LayerIds.EnemyShot, QueryTriggerInteraction.Ignore) > 0)
                {
                    var h = s_hits[0];
                    end = h.point;
                    hitCol = h.collider;
                    if (fx != null) fx.Impacts.Play(SurfaceTag.Resolve(h.collider), h.point, h.normal, h.collider, true, audio, fx.ListenerPosition, 0.7f);
                }
                if (isPlayerTarget && rig != null && rig.Eye != null)
                {
                    var head = rig.Eye.position;
                    if (NearMissMath.IsNearMiss(r.Muzzle, end, head, WeaponTable.NearMissRadius, out var closest, out var dist))
                    {
                        GameEvents.RaiseNearMiss(new NearMissEvent { Point = closest, Direction = mdir, Distance = dist, ShooterTeam = r.Team, WeaponId = r.WeaponId });
                        if (audio != null)
                        {
                            audio.Play(AudioCues.BulletCrack, closest, Mathf.Lerp(1f, 0.55f, dist / WeaponTable.NearMissRadius));
                            if (dist < 1.8f) audio.Play(AudioCues.BulletWhiz, closest, 0.8f);
                        }
                    }
                    var feet = rig.Root != null ? rig.Root.position : head;
                    var landedNear = hitCol != null && (Vector3.Distance(end, feet) <= WeaponTable.SuppressionRadius || Vector3.Distance(end, head) <= WeaponTable.SuppressionRadius);
                    if (landedNear || (NearMissMath.ClosestApproach(r.Muzzle, end, head, out _, out _) <= WeaponTable.SuppressionRadius))
                        (target as ISuppressible ?? rig as ISuppressible)?.Suppress(WeaponTable.PlayerSuppressionSeconds, r.Team);
                }
                else if (r.SuppressOnly && target is ISuppressible s)
                {
                    s.Suppress(WeaponTable.SquadSuppressionSeconds, r.Team); // GDD §14.3: squad fire suppresses an enemy for 2 s
                }
            }

            // Tracer every N rounds per shooter.
            var every = r.TracerEvery > 0 ? r.TracerEvery : spec?.TracerEvery ?? 0;
            if (every > 0 && NextTracer(r.Shooter, every) && fx != null)
            {
                fx.Tracers.Fire(r.Muzzle, end, r.Team);
                GameEvents.RaiseTracerPath(r.Muzzle, end, r.Team);
            }

            GameEvents.RaiseShotFired(new ShotEvent { Origin = r.Muzzle, Direction = dir, Team = r.Team, WeaponId = r.WeaponId, NoiseRadius = noise, Hit = hit, IsPlayer = false });
            GameEvents.RaiseNoise(new NoiseEvent(r.Muzzle, noise, r.Team, NoiseKind.Gunshot));
        }

        /// <summary>Counts a shooter's rounds; true on every <paramref name="every"/>-th (the first round of a shooter is a tracer).</summary>
        public static bool NextTracer(ITargetable shooter, int every)
        {
            if (every <= 1) return every == 1;
            int count;
            if (shooter == null)
            {
                count = s_anonTracer++;
            }
            else
            {
                if (s_tracerCount.Count > 64) s_tracerCount.Clear();
                s_tracerCount.TryGetValue(shooter, out count);
                s_tracerCount[shooter] = count + 1;
            }
            return count % every == 0;
        }
    }
}
