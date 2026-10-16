namespace LongShadow.Core
{
    /// <summary>VO queue priority (TECH_PLAN §7.10): Scripted &gt; Objective &gt; Callout &gt; Bark.</summary>
    public enum VOPriority
    {
        Bark = 0,
        Callout = 1,
        Objective = 2,
        Scripted = 3,
    }
}
