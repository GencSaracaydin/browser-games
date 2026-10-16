namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// What a <see cref="Wait"/> waits for. Beat scripts use it for diagnostics (BeatJumpWindow, smoke test report) and the
    /// EditMode fake world's autopilot uses it to satisfy the wait the way a player and the AI would.
    /// </summary>
    public enum WaitKind
    {
        /// <summary>A number of seconds (scaled or unscaled).</summary>
        Seconds = 0,
        /// <summary>An arbitrary condition (see the wait's detail).</summary>
        Until = 1,
        /// <summary>The player reached a TriggerVolume (id) during the beat or is inside it.</summary>
        Trigger = 2,
        /// <summary>The player is near a marker (id, radius).</summary>
        PlayerNear = 3,
        /// <summary>A line finished.</summary>
        Line = 4,
        /// <summary>An encounter (id) is cleared, or a wave (id/wave) has at most N alive.</summary>
        Encounter = 5,
        /// <summary>An enemy (id) is dead.</summary>
        Killed = 6,
        /// <summary>An InteractionPoint (id) was used.</summary>
        Interaction = 7,
        /// <summary>A mission event (id) was raised.</summary>
        Event = 8,
        /// <summary>The squad arrived at a node set (id).</summary>
        Squad = 9,
        /// <summary>An intel item (id) was collected.</summary>
        Intel = 10,
        /// <summary>A destructible (id) was destroyed.</summary>
        Destroyed = 11,
        /// <summary>A section (id) is loaded.</summary>
        Section = 12,
        /// <summary>A cinematic or insert finished.</summary>
        Sequence = 13,
        /// <summary>A POI (id) was glassed through the binoculars.</summary>
        Glassed = 14,
        /// <summary>Any of the child waits.</summary>
        Any = 15,
        /// <summary>All of the child waits.</summary>
        All = 16,
        /// <summary>An objective (id) completed.</summary>
        Objective = 17,
    }
}
