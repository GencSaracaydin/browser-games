using LongShadow.AI.Common;
using LongShadow.AI.Enemy;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// One squadmate's gunfight (TECH_PLAN §7.7 SquadCombat, GDD §15): engages aware, visible hostiles within 40 m
    /// (squad targets first, then the nearest), fires Commando bursts through <see cref="AICombat"/>, and applies the
    /// <see cref="KillShare"/> rule to every round: lethal rounds roll the GDD §14.3 hit chance, the rest are
    /// <see cref="AIShotRequest.SuppressOnly"/> (they crack past the enemy and suppress him for 2 s). Reloads with a
    /// callout (20 s cooldown, via the service). Plain C#, ticked by <see cref="SquadMember"/>.
    /// </summary>
    public sealed class SquadCombat
    {
        /// <summary>Engagement range (GDD §15).</summary>
        public const float EngageRange = 40f;
        /// <summary>Squad accuracy at 20 m before modifiers (WP04; squad rounds are mostly suppressive anyway).</summary>
        public const float BaseAccuracy = 0.35f;
        /// <summary>Damage per hit (Commando 34 → 26 over 25-45 m, GDD §13.1; mid value).</summary>
        public const float DamagePerHit = 30f;
        /// <summary>Seconds between target re-selections.</summary>
        public const float SelectInterval = 0.3f;

        /// <summary>The squad rifle's cadence: Commando 750 RPM, bursts of 3-5, 30-round magazines (GDD §13.1).</summary>
        public static readonly Archetype Rifle = new Archetype
        {
            Id = ArchetypeId.LegionRifleman, Name = "Squad Commando", Team = Team.Squad, Hp = 100f,
            WeaponId = WeaponIds.Commando, BurstMin = 3, BurstMax = 5, PauseMin = 0.6f, PauseMax = 1.3f, Rpm = 750f,
            Accuracy20 = BaseAccuracy, RangeMin = 5f, RangeMax = EngageRange, TracerEvery = 5, Magazine = 30, ReloadSeconds = 2.1f,
        };

        private readonly SquadMember _member;
        private readonly EnemyShooter _shooter = new EnemyShooter(Rifle);
        private float _nextSelect;
        private ITargetable _target;
        private TargetMemory _memory;
        private bool _reloading;
        private float _reloadUntil;

        /// <summary>Creates the combat logic of a member.</summary>
        public SquadCombat(SquadMember member) => _member = member;

        /// <summary>Current target (null when none in range).</summary>
        public ITargetable Target => _target;
        /// <summary>True while reloading.</summary>
        public bool Reloading => _reloading;
        /// <summary>Rounds fired (tests, diagnostics).</summary>
        public int RoundsFired => _shooter.RoundsFired;

        /// <summary>Forgets the target and refills (checkpoints, teleports).</summary>
        public void Reset()
        {
            _target = null;
            _memory = null;
            _reloading = false;
            _shooter.Reset();
        }

        /// <summary>Per-frame update; <paramref name="allowedToFire"/> is false while running, scripted or yielding.</summary>
        public void Tick(float now, bool allowedToFire, ref AIRandom rng)
        {
            if (now >= _nextSelect)
            {
                _nextSelect = now + SelectInterval;
                Select();
            }
            if (_reloading)
            {
                if (now >= _reloadUntil) FinishReload();
                return;
            }
            var canFire = allowedToFire && _target != null && _memory != null && _memory.Visible && _target.IsAlive;
            if (_shooter.Tick(now, canFire, false, ref rng)) Fire(now);
            if (_shooter.NeedsReload) StartReload(now);
        }

        private void Select()
        {
            _target = null;
            _memory = null;
            var perception = _member.Perception;
            if (perception == null) return;
            var mems = perception.Memories;
            var r2 = EngageRange * EngageRange;
            var bestScore = float.MaxValue;
            for (var i = 0; i < mems.Count; i++)
            {
                var m = mems[i];
                var t = m.Target;
                if (!m.Aware || !m.Visible || t == null || !t.IsAlive || !Teams.AreHostile(Team.Squad, t.Team)) continue;
                var d2 = m.Distance * m.Distance;
                if (d2 > r2) continue;
                var score = d2 - (t.SquadTarget ? 10000f : 0f);
                if (score >= bestScore) continue;
                bestScore = score;
                _target = t;
                _memory = m;
            }
        }

        private void Fire(float now)
        {
            var body = _member.Body;
            var muzzle = body?.Muzzle != null ? body.Muzzle.position : _member.EyePosition;
            var lethal = KillShare.IsLethal(_target, now);                                      // GDD §15 kill share
            var chance = 0f;
            if (lethal)
            {
                var inputs = new HitChanceInputs
                {
                    BaseAccuracy = BaseAccuracy,
                    Distance = Vector3.Distance(muzzle, Perception.AimOf(_target)),
                    RangeMax = EngageRange,
                    TargetMotion = MotionClassifier.Classify(Stance.Stand, _target.Velocity),
                    ApplyMotionFactor = true,
                    TimeOnTarget = _memory?.TimeOnTarget(now) ?? 0f,
                    DifficultyMultiplier = 1f,
                };
                chance = HitChance.Compute(inputs);
            }
            AICombat.Fire(new AIShotRequest
            {
                Shooter = _member,
                Muzzle = muzzle,
                Target = _target,
                HitChance = chance,
                Damage = DamagePerHit,
                WeaponId = Rifle.WeaponId,
                Team = Team.Squad,
                TracerEvery = Rifle.TracerEvery,
                SuppressOnly = !lethal,
            });
        }

        private void StartReload(float now)
        {
            _reloading = true;
            _reloadUntil = now + Rifle.ReloadSeconds + 0.3f;
            _member.Body?.PlayAction(ActionIds.Reload);
            _member.Service?.OnMemberReload(_member, now);
        }

        private void FinishReload()
        {
            _reloading = false;
            _shooter.Refill();
        }
    }
}
