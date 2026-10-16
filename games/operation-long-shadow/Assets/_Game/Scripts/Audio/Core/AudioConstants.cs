namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Numbers of the audio design (GDD §10, TECH_PLAN §7.9-7.10, CINEMATIC_DIRECTION §4) in one place, each with its
    /// source. Components expose the tunable ones as serialized fields initialised from here.
    /// </summary>
    public static class AudioConstants
    {
        /// <summary>Pooled one-shot sources (TECH_PLAN §7.9, §7.16).</summary>
        public const int SourcePool = 32;
        /// <summary>Audible SFX voice cap with priority stealing (TECH_PLAN §7.9, GDD §9.8 "audio ≤ 24 voices").</summary>
        public const int AudibleVoices = 24;
        /// <summary>Concurrent looping SFX (each uses two sources for gapless scheduled loops, TECH_PLAN §3.12).</summary>
        public const int LoopSlots = 12;

        /// <summary>Weapon near layer is full below this distance (TECH_PLAN §7.9 "near &lt; 40 m").</summary>
        public const float WeaponNearDistance = 40f;
        /// <summary>Weapon far variant is full beyond this distance (TECH_PLAN §7.9 "far &gt; 120 m").</summary>
        public const float WeaponFarDistance = 120f;
        /// <summary>Tail re-trigger interval during full auto ("every ~4th round", WP06_SFX import notes).</summary>
        public const int TailEveryNthRound = 4;

        /// <summary>Speed of sound at 30 °C desert air, m/s (realism: distant shots are heard after they are seen).</summary>
        public const float SpeedOfSound = 349f;
        /// <summary>Below this distance the propagation delay is not applied (≤ 57 ms is masked by the frame).</summary>
        public const float SpeedOfSoundMinDistance = 20f;
        /// <summary>Longest propagation delay applied (seconds).</summary>
        public const float SpeedOfSoundMaxDelay = 2.5f;

        /// <summary>Dry/radio crossfade: dry only within this distance (GDD §10.1, TECH_PLAN §7.10).</summary>
        public const float VODryMaxDistance = 8f;
        /// <summary>Dry/radio crossfade: radio only beyond this distance (GDD §10.1, TECH_PLAN §7.10).</summary>
        public const float VORadioMinDistance = 15f;
        /// <summary>Enemy barks are subtitled only within this distance (GDD §11).</summary>
        public const float BarkSubtitleDistance = 25f;
        /// <summary>Legion barks switch from the DRY to the baked FAR chain beyond this distance.</summary>
        public const float BarkFarDistance = 30f;

        /// <summary>VO ducks music by this much (TECH_PLAN §7.10).</summary>
        public const float VOMusicDuckDb = -6f;
        /// <summary>VO ducks ambience by this much (TECH_PLAN §7.10).</summary>
        public const float VOAmbienceDuckDb = -4f;
        /// <summary>VO ducks combat effects by this much while in combat (CINEMATIC_DIRECTION §4 "VO ducks combat by −6 dB").</summary>
        public const float VOCombatDuckDb = -6f;
        /// <summary>Combat ducks ambience (CINEMATIC_DIRECTION §4 "combat ducks ambience").</summary>
        public const float CombatAmbienceDuckDb = -6f;

        /// <summary>PA horn rolloff min distance (GDD §7.1).</summary>
        public const float PAHornMinDistance = 25f;
        /// <summary>PA horn rolloff max distance (GDD §7.1).</summary>
        public const float PAHornMaxDistance = 500f;

        /// <summary>Civilian emitters latch when shooting starts within this distance (GDD §0.3 G5, TECH_PLAN §7.9).</summary>
        public const float CivilianLatchDistance = 40f;

        /// <summary>LightStage s at which the cicadas stop and the crickets start (GDD §9.2a, §8.5 shot 3).</summary>
        public const float CricketsStageS = 1.97f;

        /// <summary>Beat index used for the intro and the insertion cold open (VO windows).</summary>
        public const int IntroBeat = 0;
        /// <summary>Beat index used for the outro (VO windows; WP07 passes it to PrepareWindow at the outro).</summary>
        public const int OutroBeat = 12;
        /// <summary>Beat value of lines that are always loaded (callouts, barks, death inserts, the OOB warning).</summary>
        public const int AlwaysBeat = -1;
    }
}
