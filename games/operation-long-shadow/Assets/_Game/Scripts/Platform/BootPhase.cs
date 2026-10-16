namespace LongShadow.Platform
{
    /// <summary>Stages of the boot flow (TECH_PLAN §5.2).</summary>
    public enum BootPhase
    {
        /// <summary>S_Boot is starting; services register.</summary>
        Starting,
        /// <summary>Addressables is initialising (catalog download).</summary>
        Addressables,
        /// <summary>Loading S_Persistent (systems, player, HUD).</summary>
        Persistent,
        /// <summary>Loading the sections needed before the click (S0_Booth, D20).</summary>
        BootSections,
        /// <summary>The click gate accepts the click; S9_TownFar and S1_Approach load behind it.</summary>
        GateReady,
        /// <summary>The mission is running.</summary>
        Running,
        /// <summary>Boot failed (S_Persistent could not load); the gate shows the error.</summary>
        Failed,
    }
}
