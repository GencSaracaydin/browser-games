using LongShadow.AI.Common;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// Burst cadence of one AI weapon (TECH_PLAN §7.6 AIShooter, GDD §14.1 "Fire behaviour"): bursts of
    /// <see cref="Archetype.BurstMin"/>-<see cref="Archetype.BurstMax"/> rounds at the archetype RPM, separated by
    /// pauses of <see cref="Archetype.PauseMin"/>-<see cref="Archetype.PauseMax"/> seconds; a short reaction delay
    /// before the first round of an engagement; magazine bookkeeping. While suppressed by squad fire the rate halves
    /// (GDD §14.3: interval and pause ×2). Pure C#; the brain asks <see cref="Tick"/> every frame and fires when it
    /// returns true.
    /// </summary>
    public sealed class EnemyShooter
    {
        /// <summary>Reaction delay range before the first round after (re)acquiring a target (s).</summary>
        public const float ReactionMin = 0.2f;
        /// <summary>Upper end of the reaction delay.</summary>
        public const float ReactionMax = 0.45f;
        /// <summary>Rate factor while suppressed (GDD §14.3 "fire rate ×0.5").</summary>
        public const float SuppressedRateFactor = 0.5f;

        private readonly Archetype _archetype;
        private int _burstLeft;
        private float _nextShot;
        private float _nextBurst;
        private bool _engaged;
        private int _magazine;

        /// <summary>Creates a shooter for an archetype.</summary>
        public EnemyShooter(Archetype archetype)
        {
            _archetype = archetype;
            _magazine = archetype.Magazine;
        }

        /// <summary>Rounds left in the magazine (int.MaxValue for belt-fed / no-reload weapons).</summary>
        public int Magazine => _archetype.Magazine > 0 ? _magazine : int.MaxValue;

        /// <summary>True when the magazine is empty and the weapon needs a reload.</summary>
        public bool NeedsReload => _archetype.Magazine > 0 && _magazine <= 0;

        /// <summary>True while a burst is in progress.</summary>
        public bool InBurst => _burstLeft > 0;

        /// <summary>Rounds fired since the last <see cref="Reset"/> (diagnostics, tests).</summary>
        public int RoundsFired { get; private set; }

        /// <summary>Forgets the engagement (target lost, stopped firing): the next round waits for a reaction delay.</summary>
        public void Disengage()
        {
            _engaged = false;
            _burstLeft = 0;
        }

        /// <summary>Full reset (spawn).</summary>
        public void Reset()
        {
            Disengage();
            _nextShot = 0f;
            _nextBurst = 0f;
            _magazine = _archetype.Magazine;
            RoundsFired = 0;
        }

        /// <summary>Refills the magazine (reload finished).</summary>
        public void Refill() => _magazine = _archetype.Magazine;

        /// <summary>
        /// Advances the cadence. Returns true when a round should be fired now. <paramref name="canFire"/> false (no line
        /// of sight, no token, moving) interrupts the burst and disengages.
        /// </summary>
        public bool Tick(float now, bool canFire, bool suppressed, ref AIRandom rng)
        {
            if (!canFire)
            {
                if (_engaged) Disengage();
                return false;
            }
            if (NeedsReload) return false;
            var rate = suppressed ? SuppressedRateFactor : 1f;
            if (!_engaged)
            {
                _engaged = true;
                var reaction = now + rng.Range(ReactionMin, ReactionMax);
                if (_nextBurst < reaction) _nextBurst = reaction;
            }
            if (_burstLeft <= 0)
            {
                if (now < _nextBurst) return false;
                _burstLeft = rng.RangeInclusive(_archetype.BurstMin, _archetype.BurstMax);
                _nextShot = now;
            }
            if (now < _nextShot) return false;
            _burstLeft--;
            RoundsFired++;
            if (_archetype.Magazine > 0) _magazine--;
            _nextShot = now + _archetype.ShotInterval / rate;
            if (_burstLeft <= 0 || NeedsReload)
            {
                _burstLeft = 0;
                _nextBurst = now + rng.Range(_archetype.PauseMin, _archetype.PauseMax) / rate;
            }
            return true;
        }
    }
}
