using LongShadow.Core;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// How one authored sequence plays (TECH_PLAN §7.12): its timeline, skip rule, what it locks and hides, the camera it
    /// starts on, how the screen looks when it ends, the sequence it chains into and the mission event raised when the
    /// whole chain is over. Rows live in <see cref="CinematicSequences"/>.
    /// </summary>
    public sealed class CinematicSequenceDef
    {
        /// <summary>Sequence id (<see cref="SequenceIds"/>).</summary>
        public string Id { get; }
        /// <summary>LIB_Cinematics id of the timeline (<see cref="CinematicAssetIds"/>).</summary>
        public string TimelineId { get; }
        /// <summary>Nominal length in seconds (shot tables; GDD §8).</summary>
        public float Seconds { get; }
        /// <summary>Skip rule.</summary>
        public SkipRule Skip { get; }
        /// <summary>Input channels locked for the whole sequence (Look is handled per shot by LookControl clips).</summary>
        public InputLockFlags BaseLock { get; }
        /// <summary>True when the player's own look stays free (title card on the player camera).</summary>
        public bool PlayerLookFree { get; }
        /// <summary>Hide the HUD while playing.</summary>
        public bool HideHud { get; }
        /// <summary>Holster the player's weapon while playing.</summary>
        public bool Holster { get; }
        /// <summary>Camera at the start (clips switch it per shot).</summary>
        public CameraMode StartCamera { get; }
        /// <summary>Screen state at the end (also after a skip).</summary>
        public EndScreen End { get; }
        /// <summary>Restore the player's pitch/yaw recorded at the start (MID-B).</summary>
        public bool RestorePlayerLook { get; }
        /// <summary>Sequence played right after this one (the intro chains the title card); null for none.</summary>
        public string Next { get; }
        /// <summary>Mission event raised when the whole chain ends ("Intro.ControlToPlayer"); null for none.</summary>
        public string EndEvent { get; }

        /// <summary>Creates a row.</summary>
        public CinematicSequenceDef(string id, string timelineId, float seconds, SkipRule skip, InputLockFlags baseLock,
                                    bool playerLookFree, bool hideHud, bool holster, CameraMode startCamera, EndScreen end,
                                    bool restorePlayerLook = false, string next = null, string endEvent = null)
        {
            Id = id;
            TimelineId = timelineId;
            Seconds = seconds;
            Skip = skip;
            BaseLock = baseLock;
            PlayerLookFree = playerLookFree;
            HideHud = hideHud;
            Holster = holster;
            StartCamera = startCamera;
            End = end;
            RestorePlayerLook = restorePlayerLook;
            Next = next;
            EndEvent = endEvent;
        }

        /// <summary>True if the sequence may be skipped given whether it was seen before.</summary>
        public bool CanSkip(bool seenBefore) =>
            Skip == SkipRule.Always || (Skip == SkipRule.AfterFirstView && seenBefore);
    }
}
