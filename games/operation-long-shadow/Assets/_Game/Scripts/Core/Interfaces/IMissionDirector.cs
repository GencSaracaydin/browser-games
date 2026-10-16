namespace LongShadow.Core
{
    /// <summary>Beat state machine, flags and stats (WP07 MissionDirector).</summary>
    public interface IMissionDirector
    {
        /// <summary>Current beat (0 = intro, 1..11, 12 = outro/debrief).</summary>
        int Beat { get; }
        /// <summary>True between StartMission and the debrief.</summary>
        bool IsRunning { get; }
        /// <summary>Starts from the intro.</summary>
        void StartMission();
        /// <summary>Restarts the beat script at a checkpoint's entry point.</summary>
        void ResumeFrom(string cpId);
        /// <summary>Back to the click gate (pause menu "QUIT TO SESSION", debrief "END SESSION").</summary>
        void QuitToSession();
        /// <summary>Raises a mission event (also <see cref="GameEvents.MissionEvent"/>).</summary>
        void Raise(string eventId);
        /// <summary>Reads a mission flag.</summary>
        bool Flag(string id);
        /// <summary>Sets a mission flag (saved).</summary>
        void SetFlag(string id, bool value);
        /// <summary>Debrief statistics.</summary>
        MissionStats Stats { get; }
        /// <summary>Story clock in minutes after midnight (from <see cref="ILightStage.StoryClockMinutes"/>).</summary>
        float StoryClockMinutes { get; }
    }
}
