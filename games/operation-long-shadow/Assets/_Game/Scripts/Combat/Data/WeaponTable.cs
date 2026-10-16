using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// Every weapon number of GDD §13 as code data (TECH_PLAN D12, §7.5 WeaponTable). GDD values carry a "GDD §" comment;
    /// everything else is marked "tuning" and only shapes feel (randomness ranges, bloom, keyed reload phases inside the
    /// GDD durations, view-model poses). Instances are shared and immutable by convention. Pure data: no mutable statics.
    /// </summary>
    public static class WeaponTable
    {
        /// <summary>Vertical FOV of the default 90° horizontal FOV at 16:9 (GDD §13: "about 58.7° vertical").</summary>
        public const float DefaultVerticalFov = 58.7f;
        /// <summary>Hitscan range of player weapons (TECH_PLAN §7.5: RaycastNonAlloc on PlayerShot, 500 m).</summary>
        public const float HitscanRange = 500f;
        /// <summary>Default smoke lifetime (GDD §7.3: Tinker's white smoke lasts 20 s).</summary>
        public const float SmokeSeconds = 20f;
        /// <summary>Most dropped weapons alive at once (GDD §13.1).</summary>
        public const int MaxDroppedWeapons = 6;
        /// <summary>Hold time of the pickup swap prompt (GDD §13.1: "HOLD [E] Take &lt;weapon&gt; (0.4 s)").</summary>
        public const float PickupHoldSeconds = 0.4f;
        /// <summary>Hold time of the resupply crate prompt (GDD §13.2: 1.0 s).</summary>
        public const float ResupplyHoldSeconds = 1.0f;
        /// <summary>AI miss distance from the player's head, minimum (GDD §14.3: misses land 0.5-3 m from the player).</summary>
        public const float AiMissMin = 0.5f;
        /// <summary>AI miss distance from the player's head, maximum (GDD §14.3).</summary>
        public const float AiMissMax = 3f;
        /// <summary>Near-miss radius around the player's head (GDD §10.2, §14.3: within 3 m crack and whizz).</summary>
        public const float NearMissRadius = 3f;
        /// <summary>Rounds landing within this distance of the player suppress him (GDD §14.3: 2 m, for 1 s).</summary>
        public const float SuppressionRadius = 2f;
        /// <summary>Player suppression duration (GDD §14.3).</summary>
        public const float PlayerSuppressionSeconds = 1f;
        /// <summary>Squad fire suppresses an enemy for this long (GDD §14.3: ×0.5 rate and accuracy for 2 s).</summary>
        public const float SquadSuppressionSeconds = 2f;
        /// <summary>Squad gunfire hearing radius (GDD §14.2).</summary>
        public const float SquadNoiseRadius = 40f;

        /// <summary>COMMANDO carbine (XM177E2 pattern), primary at start (GDD §13.1).</summary>
        public static readonly WeaponSpec Commando = BuildCommando();
        /// <summary>HUSH PUPPY suppressed pistol (Mk 22 pattern), always slot 2 (GDD §13.1).</summary>
        public static readonly WeaponSpec HushPuppy = BuildHushPuppy();
        /// <summary>AK rifle (AKM pattern), pickup (GDD §13.1).</summary>
        public static readonly WeaponSpec AK = BuildAK();
        /// <summary>RPK light machine gun with the 75-round drum, pickup (GDD §13.1).</summary>
        public static readonly WeaponSpec RPK = BuildRPK();
        /// <summary>M60D-pattern door gun, Beat 11 only (GDD §13.3).</summary>
        public static readonly WeaponSpec DoorGun = BuildDoorGun();

        /// <summary>M67-pattern frag (GDD §13.2).</summary>
        public static readonly GrenadeSpec Frag = new GrenadeSpec
        {
            Id = WeaponIds.Frag,
            Fuse = 4.0f,            // GDD §13.2: 4.0 s fuse from the pin pull
            ThrowRange = 18f,       // GDD §13.2: throw range 18 m
            Damage = 220f,          // GDD §13.2: 220 at the centre
            LethalRadius = 4.0f,    // GDD §13.2: lethal within 4.0 m
            MaxRadius = 8.0f,       // GDD §13.2: damage out to 8.0 m, linear falloff
            StartCount = 3,         // GDD §13.2: 3 at start
            MaxCount = 4,           // GDD §13.2: 4 max
            CheckpointMinimum = 2,  // GDD §17: frags topped up to at least 2
            NoiseRadius = 80f,      // GDD §14.2: frag 80 m
            TinnitusRadius = 6f,    // TECH_PLAN §7.5: tinnitus < 6 m
        };

        /// <summary>Knife (GDD §13.2).</summary>
        public static readonly MeleeSpec Knife = new MeleeSpec
        {
            Id = WeaponIds.Knife,
            Damage = 150f,   // GDD §13.2
            Reach = 1.6f,    // GDD §13.2: 1.6 m reach
            Lunge = 1.0f,    // GDD §13.2: with a 1.0 m lunge
            Duration = 0.6f, // GDD §13.2: 0.6 s
        };

        /// <summary>Door-gun mount and heat (GDD §13.3, §7.11).</summary>
        public static readonly DoorGunSpec DoorGunMount = new DoorGunSpec
        {
            HeatPerShot = 1f,          // GDD §13.3: +1 per shot
            OverheatAt = 55f,          // GDD §13.3: overheats at 55 (about 6 s of fire)
            OverheatRecovery = 2.0f,   // GDD §13.3: recovers 2.0 s after an overheat
            IdleRecovery = 1.0f,       // GDD §13.3: or 1.0 s to full when idle
            YawLimit = 70f,            // GDD §13.3: yaw ±70° around the left-door normal
            PitchUp = 15f,             // GDD §13.3: pitch +15°
            PitchDown = 60f,           // GDD §13.3: pitch −60°
            IncomingDamageScale = 0.25f, // GDD §7.11: incoming damage ×0.25
            HealthFloor = 1f,          // GDD §7.11: health floor of 1
        };

        /// <summary>RPG-7 pattern rocket (GDD §14.1).</summary>
        public static readonly RocketSpec Rocket = new RocketSpec
        {
            Id = WeaponIds.RPG7,
            DirectDamage = 90f,  // GDD §14.1: 90 direct
            SplashDamage = 90f,  // GDD §14.1: 4 m splash with falloff (centre value = direct)
            LethalRadius = 0.75f, // tuning
            SplashRadius = 4f,   // GDD §14.1
            Speed = 70f,         // tuning (visual)
            MaxDistance = 400f,  // tuning
            NoiseRadius = 80f,   // GDD §14.2 (explosions)
        };

        /// <summary>AI weapons (GDD §13.4, §14.1). Tracers: CINEMATIC_DIRECTION §4 (every third round for enemies).</summary>
        public static readonly EnemyWeaponSpec[] EnemyWeapons =
        {
            // GDD §14.1 Legion Rifleman: AK, 20 per hit, bursts of 3-6 with 0.6-1.2 s pauses
            new EnemyWeaponSpec { Id = WeaponIds.AK, Kind = WeaponKind.Rifle, DamageToPlayer = 20f, Rpm = 600f, BurstMin = 3, BurstMax = 6, PauseMin = 0.6f, PauseMax = 1.2f, TracerEvery = 3, CasingId = FxIds.Casing762x39 },
            // GDD §14.1 Legion Gunner: RPK, 16 per hit, long bursts of 8-15 (pauses: tuning)
            new EnemyWeaponSpec { Id = WeaponIds.RPK, Kind = WeaponKind.Lmg, DamageToPlayer = 16f, Rpm = 600f, BurstMin = 8, BurstMax = 15, PauseMin = 1.0f, PauseMax = 2.0f, TracerEvery = 3, MuzzleFlashScale = 1.15f, CasingId = FxIds.Casing762x39 },
            // GDD §14.1 Greycoat Advisor: AKMS, 24 per hit, controlled bursts of 2-3 (pauses: tuning)
            new EnemyWeaponSpec { Id = WeaponIds.AKMS, Kind = WeaponKind.Rifle, DamageToPlayer = 24f, Rpm = 600f, BurstMin = 2, BurstMax = 3, PauseMin = 0.5f, PauseMax = 1.0f, TracerEvery = 3, CasingId = FxIds.Casing762x39 },
            // GDD §14.1 Greycoat Marksman: SVD, 70 per hit (Regular), 1 shot per 3.5 s
            new EnemyWeaponSpec { Id = WeaponIds.SVD, Kind = WeaponKind.Marksman, DamageToPlayer = 70f, Rpm = 60f / 3.5f, BurstMin = 1, BurstMax = 1, PauseMin = 3.5f, PauseMax = 3.5f, TracerEvery = 1, MuzzleFlashScale = 1.2f, CasingId = FxIds.Casing762x51 },
            // GDD §14.1 Legion RPG: 90 direct, 8 s reload
            new EnemyWeaponSpec { Id = WeaponIds.RPG7, Kind = WeaponKind.Launcher, DamageToPlayer = 90f, Rpm = 60f / 8f, BurstMin = 1, BurstMax = 1, PauseMin = 8f, PauseMax = 8f, TracerEvery = 0, NoiseRadius = 80f, MuzzleFlashScale = 1.6f },
            // GDD §14.1 Technical Gunner: DShK, 35 per hit, 3-round bursts, 550 RPM (pauses: tuning)
            new EnemyWeaponSpec { Id = WeaponIds.DShK, Kind = WeaponKind.HeavyMg, DamageToPlayer = 35f, Rpm = 550f, BurstMin = 3, BurstMax = 3, PauseMin = 0.4f, PauseMax = 0.8f, TracerEvery = 3, NoiseRadius = 80f, MuzzleFlashScale = 1.7f, CasingId = FxIds.Casing762x51 },
            // Squad (SUNDIAL) carbines: red tracers, 1 in 5 (GDD §13.4), squad gunfire heard at 40 m (GDD §14.2)
            new EnemyWeaponSpec { Id = WeaponIds.Commando, Kind = WeaponKind.Rifle, DamageToPlayer = 0f, Rpm = 750f, BurstMin = 3, BurstMax = 5, PauseMin = 0.5f, PauseMax = 1.0f, TracerEvery = 5, NoiseRadius = SquadNoiseRadius, CasingId = FxIds.Casing556 },
            // Tinker on the right door gun (GDD §7.11): red tracer every 3rd (GDD §13.3)
            new EnemyWeaponSpec { Id = WeaponIds.DoorGun, Kind = WeaponKind.MountedMg, DamageToPlayer = 0f, Rpm = 550f, BurstMin = 6, BurstMax = 12, PauseMin = 0.4f, PauseMax = 0.9f, TracerEvery = 3, MuzzleFlashScale = 1.35f, CasingId = FxIds.Casing762x51 },
        };

        private static readonly WeaponSpec[] s_player = { Commando, HushPuppy, AK, RPK, DoorGun };

        /// <summary>Every weapon the player can hold (including the door gun).</summary>
        public static IReadOnlyList<WeaponSpec> PlayerWeapons => s_player;

        /// <summary>The player weapon with this id, or null.</summary>
        public static WeaponSpec Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (var i = 0; i < s_player.Length; i++)
                if (s_player[i].Id == id) return s_player[i];
            return null;
        }

        /// <summary>The AI weapon with this id, or null.</summary>
        public static EnemyWeaponSpec GetEnemy(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (var i = 0; i < EnemyWeapons.Length; i++)
                if (EnemyWeapons[i].Id == id) return EnemyWeapons[i];
            return null;
        }

        /// <summary>HUD name of any weapon id (falls back to the id).</summary>
        public static string DisplayName(string id)
        {
            var w = Get(id);
            if (w != null) return w.DisplayName;
            if (id == WeaponIds.Frag) return "FRAG";
            if (id == WeaponIds.Knife) return "KNIFE";
            return id ?? string.Empty;
        }

        /// <summary>Art folder / file stem of a weapon id ("HUSHPUPPY" gives "HushPuppy"; manifest convention).</summary>
        public static string ArtName(string id)
        {
            switch (id)
            {
                case WeaponIds.Commando: return "Commando";
                case WeaponIds.HushPuppy: return "HushPuppy";
                case WeaponIds.AK: return "AK";
                case WeaponIds.RPK: return "RPK";
                case WeaponIds.AKMS: return "AKMS";
                case WeaponIds.SVD: return "SVD";
                case WeaponIds.RPG7: return "RPG7";
                case WeaponIds.DShK: return "DShK";
                case WeaponIds.DoorGun: return "M60D";
                case WeaponIds.Frag: return "M67";
                case WeaponIds.Knife: return "Knife";
                case WeaponIds.Smoke: return "SmokeGrenade";
                default: return id;
            }
        }

        // ------------------------------------------------------------------------------------------------------------

        private static WeaponSpec BuildCommando() => new WeaponSpec
        {
            Id = WeaponIds.Commando, DisplayName = "COMMANDO", Kind = WeaponKind.Rifle, Slot = 1, ArtName = "Commando",
            DamageClose = 34f, DamageFar = 26f,        // GDD §13.1: 34 → 26
            FalloffStart = 25f, FalloffEnd = 45f,      // GDD §13.1: 25 → 45 m
            FireMode = FireMode.Auto, Rpm = 750f,      // GDD §13.1: Auto / 750
            MagazineSize = 30, MaxReserve = 150,       // GDD §13.1: 30 / 150
            PickupReserve = 60,                        // tuning
            TacticalReload = new ReloadSpec(2.1f, 0.28f, 0.42f, 0.86f, 1.36f),                 // GDD §13.1: 2.1 s (keys: tuning)
            EmptyReload = new ReloadSpec(2.7f, 0.26f, 0.38f, 0.82f, 1.30f, 1.62f, 2.02f),      // GDD §13.1: 2.7 s (keys: tuning)
            AdsTime = 0.22f, AdsFovVertical = 47f,     // GDD §13.1: 0.22 s, 47° (1.25×)
            Spread = new SpreadSpec { Hip = 3.0f, Ads = 0.15f },  // GDD §13.1: 3.0 / 0.15
            Recoil = new RecoilSpec
            {
                FirstShotPitch = 0.8f,                 // GDD §13.1: first shot 0.8°
                Pitch = 0.55f, SustainPitch = 0.55f,   // GDD §13.1: 0.55° vertical
                YawBias = 0.12f, YawRandom = 0.18f,    // GDD §13.1: drifting right +0.12° (±0.18 random)
                PitchRandom = 0.05f,                   // tuning
                // GDD §13.1: the first 8 shots are fixed: up, then right, then hooking slightly left at shots 6-8
                Pattern = new[]
                {
                    new Vector2(0.80f, 0.00f), new Vector2(0.55f, 0.02f), new Vector2(0.55f, 0.06f), new Vector2(0.55f, 0.16f),
                    new Vector2(0.55f, 0.20f), new Vector2(0.55f, -0.06f), new Vector2(0.55f, -0.10f), new Vector2(0.55f, -0.08f),
                },
                RecoveryDelay = 0.08f, RecoveryRate = 8f, // GDD §13.1: 8°/s after 0.08 s
                CameraShare = 0.3f, ModelKickBack = 0.022f, // GDD §13.1: camera 30 %, model 2.2 cm
                ModelKickPitch = 1.5f, ModelKickRoll = 1.0f,
            },
            MoveMultiplier = 1.00f,                    // GDD §13.1
            SprintOutTime = 0.20f,                     // GDD §12
            RaiseTime = 0.45f, LowerTime = 0.30f,      // tuning
            NoiseRadius = 60f,                         // GDD §13.1: 60 m
            TracerEvery = 5,                           // GDD §13.4: rifles 1 in 5
            BodyHitsToKillClose = 3, BodyHitsToKillFar = 4, // GDD §13.1
            Viewmodel = new ViewmodelSpec
            {
                HipPosition = new Vector3(0.092f, -0.128f, 0.115f), HipEuler = new Vector3(0f, -2.5f, 0f),
                EyeRelief = 0.075f, AdsViewmodelFov = 44f,
                SprintPosition = new Vector3(-0.02f, -0.035f, -0.03f), SprintEuler = new Vector3(14f, -34f, 20f),
                CasingId = FxIds.Casing556, Heft = 0.3f,
                Sockets = new WeaponSockets(new Vector3(0f, 0.058f, 0.545f), new Vector3(0f, 0.128f, 0.345f), new Vector3(0f, 0.128f, 0.03f),
                    Vector3.zero, new Vector3(0f, 0.035f, 0.26f), new Vector3(0.018f, 0.075f, 0.075f), new Vector3(0f, 0.03f, 0.085f), -0.245f),
            },
        };

        private static WeaponSpec BuildHushPuppy() => new WeaponSpec
        {
            Id = WeaponIds.HushPuppy, DisplayName = "HUSH PUPPY", Kind = WeaponKind.Pistol, Slot = 2, ArtName = "HushPuppy",
            DamageClose = 40f, DamageFar = 25f,        // GDD §13.1: 40 → 25
            FalloffStart = 12f, FalloffEnd = 30f,      // GDD §13.1: 12 → 30 m
            HeadMultiplierOverride = 3.0f,             // GDD §13.1: head ×3.0
            FireMode = FireMode.Semi, Rpm = 300f,      // GDD §13.1: semi, capped at 300
            MagazineSize = 8, MaxReserve = 48,         // GDD §13.1: 8 / 48
            PickupReserve = 16,                        // tuning (never dropped by enemies)
            TacticalReload = new ReloadSpec(1.6f, 0.18f, 0.26f, 0.58f, 0.98f),                 // GDD §13.1: 1.6 s
            EmptyReload = new ReloadSpec(2.0f, 0.17f, 0.24f, 0.55f, 0.95f, 1.22f, 1.42f),      // GDD §13.1: 2.0 s
            AdsTime = 0.15f, AdsFovVertical = 52f,     // GDD §13.1: 0.15 s, 52° (1.13×)
            Spread = new SpreadSpec { Hip = 2.0f, Ads = 0.25f, MoveAdd = 0.8f, BloomPerShot = 0.35f, BloomMax = 1.2f, BloomDecay = 4f }, // GDD §13.1: 2.0 / 0.25
            Recoil = new RecoilSpec
            {
                FirstShotPitch = 1.4f, Pitch = 1.4f, SustainPitch = 1.4f, // GDD §13.1: 1.4° vertical
                YawRandom = 0.3f,                      // GDD §13.1: ±0.3
                PitchRandom = 0.1f,                    // tuning
                RecoveryDelay = 0.05f, RecoveryRate = 12f, // GDD §13.1: 12°/s ("snaps back")
                CameraShare = 0.3f, ModelKickBack = 0.03f,
                ModelKickPitch = 4.5f, ModelKickRoll = 1.5f, BurstResetTime = 0.15f,
            },
            MoveMultiplier = 1.05f,                    // GDD §13.1
            SprintOutTime = 0.20f,                     // GDD §12
            RaiseTime = 0.30f, LowerTime = 0.22f,      // tuning
            NoiseRadius = 6f, NoiseKind = NoiseKind.SuppressedShot, // GDD §13.1: 6 m
            TracerEvery = 0,
            BodyHitsToKillClose = 3, BodyHitsToKillFar = 4, // GDD §13.1 (1 headshot close)
            Viewmodel = new ViewmodelSpec
            {
                HipPosition = new Vector3(0.085f, -0.118f, 0.27f), HipEuler = new Vector3(0f, -4f, 0f),
                EyeRelief = 0.36f, AdsViewmodelFov = 48f,
                SprintPosition = new Vector3(0f, -0.02f, -0.09f), SprintEuler = new Vector3(-38f, -12f, 14f),
                LoweredPosition = new Vector3(0.03f, -0.25f, -0.05f), LoweredEuler = new Vector3(45f, -15f, -10f),
                MuzzleFlashScale = 0f, CasingId = FxIds.Casing9mm, SwayScale = 1.3f, BobScale = 1.1f, Heft = 0.1f,
                Sockets = new WeaponSockets(new Vector3(0f, 0.085f, 0.275f), new Vector3(0f, 0.118f, 0.115f), new Vector3(0f, 0.118f, -0.015f),
                    Vector3.zero, new Vector3(-0.028f, -0.025f, 0.005f), new Vector3(0.012f, 0.098f, 0.04f), new Vector3(0f, -0.088f, -0.018f), -0.055f),
            },
        };

        private static WeaponSpec BuildAK() => new WeaponSpec
        {
            Id = WeaponIds.AK, DisplayName = "AK", Kind = WeaponKind.Rifle, Slot = 1, ArtName = "AK",
            DamageClose = 42f, DamageFar = 33f,        // GDD §13.1: 42 → 33
            FalloffStart = 22f, FalloffEnd = 45f,      // GDD §13.1: 22 → 45 m
            FireMode = FireMode.Auto, Rpm = 600f,      // GDD §13.1: Auto / 600
            MagazineSize = 30, MaxReserve = 120,       // GDD §13.1: 30 / 120
            PickupReserve = 60,                        // tuning
            TacticalReload = new ReloadSpec(2.4f, 0.30f, 0.50f, 0.98f, 1.56f),                 // GDD §13.1: 2.4 s
            EmptyReload = new ReloadSpec(3.0f, 0.28f, 0.46f, 0.94f, 1.52f, 1.92f, 2.40f),      // GDD §13.1: 3.0 s
            AdsTime = 0.26f, AdsFovVertical = 46f,     // GDD §13.1: 0.26 s, 46° (1.28×)
            Spread = new SpreadSpec { Hip = 3.6f, Ads = 0.30f },  // GDD §13.1: 3.6 / 0.30
            Recoil = new RecoilSpec
            {
                FirstShotPitch = 1.1f,                 // GDD §13.1: first shot 1.1°
                Pitch = 0.85f, SustainPitch = 0.85f,   // GDD §13.1: 0.85° vertical
                YawRandom = 0.35f,                     // GDD §13.1: ±0.35 horizontal (wild)
                PitchRandom = 0.08f,                   // tuning
                // GDD §13.1: the first 6 are fixed: up, left, right, left
                Pattern = new[]
                {
                    new Vector2(1.10f, 0.00f), new Vector2(0.85f, -0.05f), new Vector2(0.85f, -0.32f),
                    new Vector2(0.85f, 0.38f), new Vector2(0.85f, -0.30f), new Vector2(0.85f, -0.12f),
                },
                RecoveryDelay = 0.08f, RecoveryRate = 6f, // GDD §13.1: 6°/s
                CameraShare = 0.3f, ModelKickBack = 0.022f,
                ModelKickPitch = 1.9f, ModelKickRoll = 1.4f,
            },
            MoveMultiplier = 0.97f,                    // GDD §13.1
            SprintOutTime = 0.20f,                     // GDD §12
            RaiseTime = 0.50f, LowerTime = 0.32f,      // tuning
            NoiseRadius = 60f,                         // GDD §13.1
            TracerEvery = 5,                           // GDD §13.4
            BodyHitsToKillClose = 3, BodyHitsToKillFar = 4, // GDD §13.1
            Viewmodel = new ViewmodelSpec
            {
                HipPosition = new Vector3(0.095f, -0.13f, 0.11f), HipEuler = new Vector3(0f, -2.5f, 0f),
                EyeRelief = 0.19f, AdsViewmodelFov = 44f,
                SprintPosition = new Vector3(-0.02f, -0.04f, -0.03f), SprintEuler = new Vector3(15f, -33f, 20f),
                CasingId = FxIds.Casing762x39, Heft = 0.4f,
                Sockets = new WeaponSockets(new Vector3(0f, 0.0629f, 0.6116f), new Vector3(0f, 0.1019f, 0.5684f), new Vector3(0f, 0.1094f, 0.2005f),
                    Vector3.zero, new Vector3(0f, 0.0544f, 0.316f), new Vector3(0.014f, 0.0714f, 0.1291f), new Vector3(0f, 0.034f, 0.1436f), -0.268f),
            },
        };

        private static WeaponSpec BuildRPK() => new WeaponSpec
        {
            Id = WeaponIds.RPK, DisplayName = "RPK", Kind = WeaponKind.Lmg, Slot = 1, ArtName = "RPK",
            DamageClose = 42f, DamageFar = 35f,        // GDD §13.1: 42 → 35
            FalloffStart = 25f, FalloffEnd = 50f,      // GDD §13.1: 25 → 50 m
            FireMode = FireMode.Auto, Rpm = 600f,      // GDD §13.1: Auto / 600
            MagazineSize = 75, MaxReserve = 150,       // GDD §13.1: 75 / 150
            PickupReserve = 75,                        // tuning
            TacticalReload = new ReloadSpec(4.2f, 0.45f, 0.90f, 1.85f, 2.85f),                 // GDD §13.1: 4.2 s
            EmptyReload = new ReloadSpec(5.0f, 0.42f, 0.85f, 1.80f, 2.80f, 3.55f, 4.15f),      // GDD §13.1: 5.0 s
            AdsTime = 0.38f, AdsFovVertical = 46f,     // GDD §13.1: 0.38 s, 46° (1.28×)
            Spread = new SpreadSpec { Hip = 4.5f, Ads = 0.35f, BloomPerShot = 0.12f },  // GDD §13.1: 4.5 / 0.35
            Recoil = new RecoilSpec
            {
                FirstShotPitch = 0.9f,
                Pitch = 0.6f,                          // GDD §13.1: 0.6° vertical
                SustainPitch = 0.45f,                  // GDD §13.1: settles to 0.45° after 8 shots
                YawRandom = 0.25f,                     // GDD §13.1: ±0.25
                PitchRandom = 0.05f,                   // tuning
                // GDD §13.1: the first 8 are fixed: steep climb, then flattening
                Pattern = new[]
                {
                    new Vector2(0.90f, 0.00f), new Vector2(0.82f, 0.04f), new Vector2(0.74f, -0.05f), new Vector2(0.66f, 0.07f),
                    new Vector2(0.58f, -0.04f), new Vector2(0.52f, 0.05f), new Vector2(0.48f, -0.03f), new Vector2(0.45f, 0.02f),
                },
                RecoveryDelay = 0.08f, RecoveryRate = 7f, // GDD §13.1: 7°/s
                CameraShare = 0.3f, ModelKickBack = 0.02f,
                ModelKickPitch = 1.2f, ModelKickRoll = 0.9f,
            },
            MoveMultiplier = 0.88f,                    // GDD §13.1
            SprintOutTime = 0.32f,                     // GDD §12: 0.32 s with the RPK
            RaiseTime = 0.65f, LowerTime = 0.42f,      // tuning
            NoiseRadius = 60f,                         // GDD §13.1
            TracerEvery = 3,                           // GDD §13.4: machine guns 1 in 3
            BodyHitsToKillClose = 3, BodyHitsToKillFar = 3, // GDD §13.1
            Viewmodel = new ViewmodelSpec
            {
                HipPosition = new Vector3(0.10f, -0.142f, 0.10f), HipEuler = new Vector3(0f, -2.5f, 0f),
                EyeRelief = 0.20f, AdsViewmodelFov = 44f,
                SprintPosition = new Vector3(-0.01f, -0.05f, -0.03f), SprintEuler = new Vector3(16f, -28f, 16f),
                MuzzleFlashScale = 1.15f, CasingId = FxIds.Casing762x39, Heft = 0.75f, SwayScale = 0.85f,
                Sockets = new WeaponSockets(new Vector3(0f, 0.063f, 0.77f), new Vector3(0f, 0.103f, 0.72f), new Vector3(0f, 0.11f, 0.20f),
                    Vector3.zero, new Vector3(0f, 0.055f, 0.33f), new Vector3(0.014f, 0.072f, 0.13f), new Vector3(0f, 0.034f, 0.144f), -0.27f),
            },
        };

        private static WeaponSpec BuildDoorGun() => new WeaponSpec
        {
            Id = WeaponIds.DoorGun, DisplayName = "M60", Kind = WeaponKind.MountedMg, Slot = 0, ArtName = "M60D",
            DamageClose = 60f, DamageFar = 60f,        // GDD §13.3: 60 damage per hit
            FalloffStart = 0f, FalloffEnd = 0f,
            FireMode = FireMode.Auto, Rpm = 550f,      // GDD §13.3: 550 RPM
            InfiniteAmmo = true,                       // GDD §13.3: infinite ammo
            Spread = new SpreadSpec { Hip = 0.6f, Ads = 0.6f, MoveAdd = 0f, AdsMoveAdd = 0f, AirborneMul = 1f, CrouchMul = 1f, BloomPerShot = 0f, BloomMax = 0f, CrosshairMax = 7f }, // GDD §13.3: 0.6°
            Recoil = new RecoilSpec
            {
                FirstShotPitch = 0.22f, Pitch = 0.22f, SustainPitch = 0.22f, // tuning (mounted: light climb)
                YawRandom = 0.15f, PitchRandom = 0.05f,
                RecoveryDelay = 0.06f, RecoveryRate = 10f,
                CameraShare = 0.3f, ModelKickBack = 0.012f, ModelKickPitch = 0.8f, ModelKickRoll = 0.6f,
            },
            MoveMultiplier = 1f, SprintOutTime = 0f, RaiseTime = 0.6f, LowerTime = 0.4f,
            NoiseRadius = 60f,
            TracerEvery = 3,                           // GDD §13.3: red tracer every 3rd round
            Viewmodel = new ViewmodelSpec
            {
                HipPosition = new Vector3(0f, -0.16f, 0.22f), HipEuler = Vector3.zero,
                EyeRelief = 0.3f, AdsViewmodelFov = 55f,
                LoweredPosition = new Vector3(0f, -0.35f, -0.1f), LoweredEuler = new Vector3(30f, 0f, 0f),
                MuzzleFlashScale = 1.35f, CasingId = FxIds.Casing762x51, Heft = 1f, SwayScale = 0.45f, BobScale = 0.2f,
                Sockets = new WeaponSockets(new Vector3(0f, 0.07f, 1.0f), new Vector3(0f, 0.13f, 0.92f), new Vector3(0f, 0.13f, 0.25f),
                    new Vector3(0.06f, 0f, 0f), new Vector3(-0.06f, 0f, 0f), new Vector3(0.04f, 0.07f, 0.30f), new Vector3(-0.05f, 0.05f, 0.32f), -0.1f),
            },
        };
    }
}
