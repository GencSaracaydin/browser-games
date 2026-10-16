namespace LongShadow.Cinematics.Playables
{
    /// <summary>One-shot actions of <see cref="CinematicEventClip"/> (the signal track of TECH_PLAN §7.12).</summary>
    public enum CinematicEventKind
    {
        /// <summary>Raise a mission event (Id), e.g. "Intro.ControlToPlayer".</summary>
        MissionEvent,
        /// <summary>Hold the timeline until a <see cref="HoldCondition"/> holds (Value = condition index).</summary>
        Hold,
        /// <summary>Booth back to its resting state.</summary>
        BoothResting,
        /// <summary>Booth to "Session 11" (Flag true) or "Session 12" (false).</summary>
        BoothSession11,
        /// <summary>Play a glitch insert (Id, optional Line).</summary>
        PlayInsert,
        /// <summary>Raise InsertStarted (Flag true) or InsertEnded (false) for Id (timeline-driven inserts such as I3).</summary>
        InsertBus,
        /// <summary>Spawn the insertion cast and helicopter (Flag true) or remove them (false).</summary>
        InsertionCast,
        /// <summary>WP08 title card for Value seconds.</summary>
        HudTitleCard,
        /// <summary>IScreenFX.Flash(Value EV, Value2 seconds).</summary>
        Flash,
        /// <summary>Fade to black over Value seconds (Flag: to white).</summary>
        FadeOut,
        /// <summary>Fade in from black/white over Value seconds.</summary>
        FadeIn,
        /// <summary>MID-B projection rig: set up (Flag true) or tear down (false).</summary>
        Projection,
        /// <summary>MID-B projection content (Id: "Leader", "Film", "Off").</summary>
        ProjectionContent,
        /// <summary>IAudioService.Tinnitus(Value seconds).</summary>
        Tinnitus,
        /// <summary>Squad look target (Id binding, Value weight; empty Id clears).</summary>
        SquadLook,
        /// <summary>Holster (Flag true) or draw the player's weapon.</summary>
        Holster,
        /// <summary>Set the player's look angles (Value pitch, Value2 compass yaw): hand-off headings, also on skip.</summary>
        PlayerLook,
        /// <summary>
        /// LightStage: Flag true = take the override and jump to s = Value (the insertion plays at s 0, sun 8° at 258°);
        /// Flag false = release the override.
        /// </summary>
        LightStage,
    }
}
