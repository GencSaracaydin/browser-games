namespace LongShadow.AI.Common
{
    /// <summary>
    /// An AI agent updated by <see cref="AIDirector"/> once per frame (enemy brains, squad members). Central ticking keeps
    /// the order deterministic and lets the director measure the AI cost (TECH_PLAN §7.16: AI ≤ 3 ms).
    /// </summary>
    public interface IAITickable
    {
        /// <summary>Per-frame update with scaled time (TECH_PLAN R3).</summary>
        void AITick(float now, float dt);
    }
}
