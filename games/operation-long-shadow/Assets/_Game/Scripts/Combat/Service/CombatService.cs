using System;
using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Combat.Interactables;
using LongShadow.Combat.Projectiles;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Service
{
    /// <summary>
    /// <see cref="ICombatService"/> (TECH_PLAN §7.5 CombatService), the combat host in S_Persistent: AI shots (roll the hit
    /// chance, damage or a near miss 0.5-3 m off the player's head, GDD §14.3), AI grenades, RPG rockets (GDD §14.1),
    /// explosions with cover occlusion, smoke screens that block AI sight, weapon pickups and resupply crates. Owns the
    /// pools (grenades 8, rockets 4, smoke 4) and the <see cref="CombatFx"/> hub, and clears every transient on a
    /// checkpoint reload. Registers itself in Awake; systems reach it through <c>ServiceRef&lt;ICombatService&gt;</c>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Weapons - 10)]
    [DisallowMultipleComponent]
    public sealed class CombatService : MonoBehaviour, ICombatService
    {
        /// <summary>Grenade pool size (TECH_PLAN §7.16).</summary>
        public const int GrenadeCount = 8;
        /// <summary>Rocket pool size.</summary>
        public const int RocketCount = 4;
        /// <summary>Smoke cloud pool size.</summary>
        public const int SmokeCount = 4;
        /// <summary>Explosions with a damage radius at least this big use the vehicle fireball effect.</summary>
        public const float FireballRadius = 9f;

        private static CombatService s_current;

        private GrenadePool _grenades;
        private RocketPool _rockets;
        private SmokePool _smoke;
        private CombatFx _fx;
        private ServiceRef<IAudioService> _audio;
        private bool _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_current = null;

        /// <summary>The live service (same object as the registered <see cref="ICombatService"/>), or null.</summary>
        public static CombatService Current => s_current != null ? s_current : null;

        /// <summary>True while any grenade's fuse burns (checkpoint deferral, GDD §17).</summary>
        public bool AnyLiveGrenade => _grenades != null && _grenades.AnyLive;

        private void Awake()
        {
            if (s_current != null && s_current != this)
            {
                LSLog.Warn(LogCategory.Combat, "A second CombatService was created; the first one stays.", this);
                enabled = false;
                return;
            }
            s_current = this;
            _fx = GetComponent<CombatFx>();
            if (_fx == null) _fx = gameObject.AddComponent<CombatFx>();
            var lib = Libraries.Get<CombatFxLibrary>();
            var root = new GameObject("Projectiles").transform;
            root.SetParent(transform, false);
            _fx.EnsureBuilt();
            var mats = _fx.Materials ?? FxMaterialSet.Procedural();
            _grenades = new GrenadePool(root, lib, GrenadeCount);
            _rockets = new RocketPool(root, lib, mats, RocketCount);
            _smoke = new SmokePool(root, lib, mats, SmokeCount);
            GameServices.Register<ICombatService>(this);
            _registered = true;
        }

        private void OnDestroy()
        {
            if (_registered) GameServices.Unregister<ICombatService>(this);
            if (s_current == this) s_current = null;
        }

        private void OnEnable() => GameEvents.CheckpointLoaded += OnCheckpointLoaded;

        private void OnDisable() => GameEvents.CheckpointLoaded -= OnCheckpointLoaded;

        // ---- ICombatService -----------------------------------------------------------------------------------------

        /// <inheritdoc/>
        public void FireAI(in AIShotRequest r) => CombatResolver.FireAI(r);

        /// <inheritdoc/>
        public void ThrowGrenadeAI(Vector3 from, Vector3 to, Team team)
        {
            var g = GrenadeBallistics.Gravity;
            var flat = to - from;
            flat.y = 0f;
            var time = GrenadeBallistics.AiFlightTime(flat.magnitude);
            var v = GrenadeBallistics.SolveArc(from, to, time, g);
            // AI cooks about a second; a little randomness keeps two grenades from popping together.
            var fuse = WeaponTable.Frag.AiFuseAtRelease + UnityEngine.Random.Range(-0.25f, 0.25f);
            ThrowGrenade(from, v, Mathf.Max(time + 0.6f, fuse), team, null);
        }

        /// <summary>Launches a frag (player and AI): position, velocity, fuse left, thrower team and instigator.</summary>
        public void ThrowGrenade(Vector3 position, Vector3 velocity, float fuse, Team team, GameObject instigator)
        {
            if (_grenades == null) return;
            _grenades.Launch(position, velocity, fuse, team, instigator);
        }

        /// <inheritdoc/>
        public void FireRocket(Vector3 from, Vector3 to, Team team, Action<Vector3> onImpact)
        {
            if (_rockets == null) return;
            _rockets.Launch(from, to, team, null, onImpact);
            var dir = to - from;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            _audio.Value?.PlayWeaponShot(WeaponIds.RPG7, from, false);
            if (_fx != null)
            {
                _fx.Muzzle.FlashWorld(from, dir, WeaponTable.GetEnemy(WeaponIds.RPG7)?.MuzzleFlashScale ?? 1.6f);
                // The back-blast: a flash and a dust puff behind the tube.
                _fx.Lights.Flash(from - dir * 0.8f, new Color(1f, 0.7f, 0.4f), 6f, 6f, 0.12f);
                _fx.Impacts.Play(SurfaceType.Sand, from - dir * 1.2f, -dir, null, false, null, _fx.ListenerPosition);
            }
            var noise = WeaponTable.Rocket.NoiseRadius;
            GameEvents.RaiseShotFired(new ShotEvent { Origin = from, Direction = dir, Team = team, WeaponId = WeaponIds.RPG7, NoiseRadius = noise, IsPlayer = false });
            GameEvents.RaiseNoise(new NoiseEvent(from, noise, team, NoiseKind.Gunshot));
        }

        /// <inheritdoc/>
        public void Explode(Vector3 position, float damage, float lethalRadius, float maxRadius, GameObject instigator, Team team)
        {
            var big = maxRadius >= FireballRadius;
            CombatResolver.Explode(position, damage, lethalRadius, maxRadius, instigator, team, null,
                big ? FxIds.ExplosionFireball : FxIds.ExplosionFrag, big ? CombatCues.ExplosionFireball : CombatCues.ExplosionFrag,
                WeaponTable.Frag.NoiseRadius, null); // GDD §14.2: explosions heard at 80 m
        }

        /// <inheritdoc/>
        public void SpawnSmoke(Vector3 position, Color color, float seconds)
        {
            if (_smoke == null) return;
            _smoke.Spawn(position, color, seconds > 0f ? seconds : WeaponTable.SmokeSeconds);
            _audio.Value?.Play(CombatCues.Smoke, position);
        }

        /// <inheritdoc/>
        public void SpawnPickup(string weaponId, Vector3 position) => WeaponPickups.Spawn(weaponId, position);

        /// <inheritdoc/>
        public GameObject SpawnAmmoCrate(Vector3 position, Quaternion rotation) => AmmoCrate.Spawn(position, rotation);

        // ---- housekeeping -------------------------------------------------------------------------------------------

        /// <summary>Removes every live grenade, rocket, smoke cloud, pickup and effect (checkpoint reload).</summary>
        public void ClearTransients()
        {
            _grenades?.Clear();
            _rockets?.Clear();
            _smoke?.Clear();
            WeaponPickups.Clear();
            _fx?.Clear();
        }

        private void OnCheckpointLoaded(string cpId) => ClearTransients();
    }
}
