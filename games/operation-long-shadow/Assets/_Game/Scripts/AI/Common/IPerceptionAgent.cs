namespace LongShadow.AI.Common
{
    /// <summary>
    /// Something that needs line-of-sight raycasts at its perception rate (enemy brains, squad members). Scheduled
    /// round-robin by <see cref="PerceptionScheduler"/> under the per-frame <see cref="LosBudget"/>.
    /// </summary>
    public interface IPerceptionAgent
    {
        /// <summary>True when the agent's next perception update is due (10 Hz, TECH_PLAN §7.16).</summary>
        bool PerceptionDue(float now);

        /// <summary>
        /// Prepares an update and returns the number of rays it will cast (0 is allowed). Must be cheap and must not
        /// raycast. The scheduler reserves that many rays before calling <see cref="RunPerception"/>.
        /// </summary>
        int PreparePerception(float now);

        /// <summary>Runs the prepared update, casting at most the number of rays returned by <see cref="PreparePerception"/>.</summary>
        void RunPerception(float now);
    }
}
