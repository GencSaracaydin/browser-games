using System.Collections.Generic;
using LongShadow.World.Look.Stage;
using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// A sodium street lamp head (GDD §8.5 row 4, §9.5; PROC_LAMP_POSTS): dark until the strike, then
    /// <see cref="SodiumWarmup"/> (sputter, pink-red → orange over 8 s). <see cref="LampRipple"/> schedules the strike by
    /// the lamp's distance from the generator (15 m/s); a lamp that loads after the ripple, or after a checkpoint jump
    /// past s 1.97, snaps on warm; going back below 1.97 (checkpoint) turns it off. 38 lamp heads are emissive; the
    /// budget lets at most 6 near the player light the street for real.
    /// </summary>
    public sealed class SodiumLamp : PracticalLight
    {
        private static readonly List<SodiumLamp> s_all = new List<SodiumLamp>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_all.Clear();

        /// <summary>Every enabled sodium lamp.</summary>
        public static IReadOnlyList<SodiumLamp> All => s_all;

        private float _strikeAt = float.PositiveInfinity;
        private bool _warm;

        /// <summary>True once struck (warming or warm).</summary>
        public bool Struck => _warm || Time.time >= _strikeAt;
        /// <summary>Scheduled strike time (Time.time), +∞ when none.</summary>
        public float StrikeAt => _strikeAt;

        /// <inheritdoc />
        protected override void OnEnable()
        {
            if (!s_all.Contains(this)) s_all.Add(this);
            base.OnEnable();
            // A section that loads while the ripple is still travelling joins it.
            if (!Struck && LampRipple.HasStruck)
            {
                var at = LampRipple.StrikeTimeFor(transform.position);
                if (at > Time.time) ScheduleStrike(at);
                else SnapOn();
            }
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            s_all.Remove(this);
            base.OnDisable();
        }

        /// <summary>Strikes at <paramref name="time"/> (Time.time).</summary>
        public void ScheduleStrike(float time)
        {
            if (_warm) return;
            _strikeAt = Mathf.Min(_strikeAt, time);
        }

        /// <summary>Fully warm now (checkpoint, late load).</summary>
        public void SnapOn()
        {
            _warm = true;
            _strikeAt = Time.time - SodiumWarmup.Duration;
            Snap();
        }

        /// <summary>Dark now (before the strike).</summary>
        public void SnapOff()
        {
            _warm = false;
            _strikeAt = float.PositiveInfinity;
            Snap();
        }

        /// <inheritdoc />
        protected override float TargetLevel()
        {
            if (IsDestroyed) return 0f;
            if (_warm) return 1f;
            var since = Time.time - _strikeAt;
            if (since < 0f) return 0f;
            if (since >= SodiumWarmup.Duration) _warm = true;
            return SodiumWarmup.Level(since, Seed);
        }

        /// <inheritdoc />
        protected override Color ColorFactor() => _warm ? SodiumWarmup.WarmColor : SodiumWarmup.Hue(Time.time - _strikeAt);

        /// <inheritdoc />
        protected override float Flicker(float t) => 1f; // the warm-up carries the sputter

        /// <inheritdoc />
        protected override void EvaluateRule(bool snap)
        {
            var ls = LightStageController.Active;
            var s = ls != null ? ls.S : 0f;
            if (s < LightStageTable.StrikeS - 0.005f)
            {
                if (Struck) SnapOff();
            }
            else if (!Struck && !LampRipple.IsRippling) SnapOn();
            if (snap) Snap();
        }
    }
}
