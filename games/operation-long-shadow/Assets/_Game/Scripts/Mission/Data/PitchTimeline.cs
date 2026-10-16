namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Beat 10 hold timeline (GDD §7.10 "Hold timeline"), seconds from the end of D070. The trucks, the NVG flanker and
    /// the RPG gunner's shots are WP04's (EncounterTable E10); the flares, Magpie's approach, the wave-off and the
    /// wheels-down timer are the beat script's.
    /// </summary>
    public static class PitchTimeline
    {
        /// <summary>Truck 1 enters view (D071).</summary>
        public const float Truck1 = 8f;
        /// <summary>FL1 ignites (D072, BK_GRY_09 once).</summary>
        public const float Flare1 = 12f;
        /// <summary>The NVG flanker (WP04).</summary>
        public const float Nvg = 20f;
        /// <summary>Truck 2 with the RPG gunner (D073 when he spawns).</summary>
        public const float Truck2 = 40f;
        /// <summary>FL2 ignites.</summary>
        public const float Flare2 = 42f;
        /// <summary>Magpie's final approach, 15 s left (D074).</summary>
        public const float Approach = 60f;
        /// <summary>Seconds before <see cref="Approach"/> at which the approach spline is started (authored 30 s: 15 s to the (600, 60, 420) knot, 15 s to the pad).</summary>
        public const float ApproachLead = 15f;
        /// <summary>The RPG gunner fires at Magpie if alive (wave-off).</summary>
        public const float RpgAtMagpie = 63f;
        /// <summary>Grace for the AI's B10.RPGAtMagpie event before the script forces the wave-off.</summary>
        public const float RpgEventGrace = 0.5f;
        /// <summary>Wheels down without a wave-off.</summary>
        public const float WheelsDown = 75f;
        /// <summary>Extra seconds a wave-off adds (wheels down at 95).</summary>
        public const float WaveOffExtra = 20f;
        /// <summary>FL3 after a wave-off.</summary>
        public const float Flare3 = 72f;
        /// <summary>Hollis kills the RPG gunner this long after the wave-off if he is still alive (he would telegraph again).</summary>
        public const float WaveOffKillDelay = 8f;
        /// <summary>Magpie's distance at the hold start (ring label).</summary>
        public const float StartDistance = 2000f;
        /// <summary>Magpie's distance after a wave-off; it counts down over the last 20 s.</summary>
        public const float WaveOffDistance = 900f;

        /// <summary>Magpie's distance in metres at hold time <paramref name="t"/> (GDD §7.10 objective 2).</summary>
        public static float MagpieDistance(float t, float waveOffAt)
        {
            if (waveOffAt < 0f)
                return t >= WheelsDown ? 0f : StartDistance * (1f - t / WheelsDown);
            var atWaveOff = StartDistance * (1f - waveOffAt / WheelsDown);
            var down = WheelsDown + WaveOffExtra;
            if (t <= waveOffAt) return StartDistance * (1f - t / WheelsDown);
            if (t <= WheelsDown) return atWaveOff + (WaveOffDistance - atWaveOff) * ((t - waveOffAt) / (WheelsDown - waveOffAt));
            if (t >= down) return 0f;
            return WaveOffDistance * (1f - (t - WheelsDown) / WaveOffExtra);
        }
    }
}
