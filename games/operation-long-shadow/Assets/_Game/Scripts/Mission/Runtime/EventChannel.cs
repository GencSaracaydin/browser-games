namespace LongShadow.Mission.Runtime
{
    /// <summary>Kinds of events recorded by <see cref="MissionEventLog"/>.</summary>
    public enum EventChannel
    {
        /// <summary>TriggerEntered (player) by trigger id.</summary>
        TriggerEnter = 0,
        /// <summary>TriggerExited (player) by trigger id.</summary>
        TriggerExit = 1,
        /// <summary>GameEvents.MissionEvent by id.</summary>
        Mission = 2,
        /// <summary>GameEvents.Interacted by InteractionPoint id.</summary>
        Interacted = 3,
        /// <summary>GameEvents.DestructibleDestroyed by id.</summary>
        Destroyed = 4,
        /// <summary>GameEvents.VOLineStarted by line id.</summary>
        LineStarted = 5,
        /// <summary>GameEvents.VOLineFinished by line id.</summary>
        LineFinished = 6,
        /// <summary>GameEvents.ActorKilled by victim id.</summary>
        Killed = 7,
        /// <summary>GameEvents.WaveSpawned by "encounter/wave".</summary>
        WaveSpawned = 8,
        /// <summary>GameEvents.EncounterCleared by encounter id.</summary>
        EncounterCleared = 9,
        /// <summary>GameEvents.CinematicEnded by id.</summary>
        CinematicEnded = 10,
        /// <summary>GameEvents.InsertEnded by id.</summary>
        InsertEnded = 11,
        /// <summary>GameEvents.IntelCollected by intel id.</summary>
        Intel = 12,
        /// <summary>GameEvents.ObjectiveCompleted by objective id.</summary>
        Objective = 13,
        /// <summary>GameEvents.SectionLoaded by section name.</summary>
        SectionLoaded = 14,
        /// <summary>Lines requested by the mission (line id).</summary>
        LineRequested = 15,
    }
}
