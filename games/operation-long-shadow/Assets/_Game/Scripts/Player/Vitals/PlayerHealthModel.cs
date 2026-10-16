using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.Vitals
{
    /// <summary>
    /// Player health rules (GDD §16, TECH_PLAN §7.3 PlayerHealth), pure and clock-driven for tests:
    /// 100 HP; regeneration starts <see cref="DifficultyProfile.RegenDelay"/> s after the last damage at
    /// <see cref="DifficultyProfile.RegenRate"/> HP/s; "last stand" immunity for <see cref="DifficultyProfile.LastStandImmunity"/> s
    /// when a hit drops the player below 20 HP while alive, once per regen cycle (re-armed at full health); Beat 11
    /// damage modifiers (×0.25 and an HP floor of 1, so the player cannot die); god mode. The hit that crosses 20 HP is
    /// applied in full (that is what makes GDD §16's "hits to down" 10 / 5 / 4 / 3 come out exactly).
    /// </summary>
    public sealed class PlayerHealthModel
    {
        private DifficultyProfile _profile;

        /// <summary>Creates a model at full health.</summary>
        public PlayerHealthModel(DifficultyProfile profile)
        {
            _profile = profile;
            DamageScale = 1f;
            ResetFull();
        }

        /// <summary>Difficulty in effect (GDD §16).</summary>
        public DifficultyProfile Profile
        {
            get => _profile;
            set
            {
                var wasFull = Current >= Max - 1e-4f;
                _profile = value;
                if (wasFull || Current > Max) Current = Max;
            }
        }

        /// <summary>Maximum HP (GDD §16: 100 on every level).</summary>
        public float Max => _profile.PlayerHp > 0f ? _profile.PlayerHp : 100f;
        /// <summary>Current HP.</summary>
        public float Current { get; private set; }
        /// <summary>False once dead (until <see cref="ResetFull"/>).</summary>
        public bool IsAlive { get; private set; }
        /// <summary>Seconds since the last damage (including hits absorbed by immunity).</summary>
        public float TimeSinceDamage { get; private set; }
        /// <summary>Seconds of last-stand immunity left.</summary>
        public float ImmunityRemaining { get; private set; }
        /// <summary>True once last stand fired in this regen cycle.</summary>
        public bool LastStandUsed { get; private set; }
        /// <summary>Damage scale (Beat 11: 0.25).</summary>
        public float DamageScale { get; set; }
        /// <summary>HP floor (Beat 11: 1); 0 = none.</summary>
        public float FloorHp { get; set; }
        /// <summary>No damage at all (dev console).</summary>
        public bool GodMode { get; set; }
        /// <summary>Current / Max.</summary>
        public float Normalized => Max > 0f ? Current / Max : 0f;

        /// <summary>
        /// Applies damage that is already difficulty-scaled (<see cref="PlayerDamageMath.ScaleIncoming"/>). Returns the HP
        /// actually removed (0 while dead, in god mode or during last-stand immunity).
        /// </summary>
        public float ApplyDamage(float amount)
        {
            if (!IsAlive || GodMode || amount <= 0f) return 0f;
            TimeSinceDamage = 0f;
            if (ImmunityRemaining > 0f) return 0f;

            var before = Current;
            var after = before - amount * Mathf.Max(0f, DamageScale);
            if (FloorHp > 0f && after < FloorHp) after = Mathf.Min(before, FloorHp);

            var threshold = _profile.LastStandThresholdHp > 0f ? _profile.LastStandThresholdHp : 20f;
            if (!LastStandUsed && _profile.LastStandImmunity > 0f && before >= threshold && after < threshold && after > 0f)
            {
                ImmunityRemaining = _profile.LastStandImmunity;
                LastStandUsed = true;
            }

            Current = Mathf.Max(0f, after);
            if (Current <= 0f) IsAlive = false;
            return before - Current;
        }

        /// <summary>Advances the clock: immunity runs out, regeneration after the delay, last stand re-arms at full HP.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            TimeSinceDamage += dt;
            if (ImmunityRemaining > 0f) ImmunityRemaining = Mathf.Max(0f, ImmunityRemaining - dt);
            if (!IsAlive) return;
            if (Current < Max && TimeSinceDamage >= _profile.RegenDelay)
            {
                // Only the part of dt that lies after the delay regenerates (exact at any frame rate).
                var regenTime = Mathf.Min(dt, TimeSinceDamage - _profile.RegenDelay);
                Current = Mathf.Min(Max, Current + _profile.RegenRate * regenTime);
            }
            if (Current >= Max - 1e-4f) LastStandUsed = false;
        }

        /// <summary>Full health, alive, no immunity (checkpoint reload, GDD §17).</summary>
        public void ResetFull()
        {
            Current = Max;
            IsAlive = true;
            TimeSinceDamage = 1000f;
            ImmunityRemaining = 0f;
            LastStandUsed = false;
        }

        /// <summary>Sets HP directly (dev console, save restore); alive if &gt; 0.</summary>
        public void SetCurrent(float hp)
        {
            Current = Mathf.Clamp(hp, 0f, Max);
            IsAlive = Current > 0f;
        }
    }
}
