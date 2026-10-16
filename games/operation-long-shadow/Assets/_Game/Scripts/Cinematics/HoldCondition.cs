namespace LongShadow.Cinematics
{
    /// <summary>What a timeline hold waits for before playing on (TECH_PLAN §5.2 step 3, D20).</summary>
    public enum HoldCondition
    {
        /// <summary>No wait.</summary>
        None,
        /// <summary>S1_Approach and S9_TownFar loaded (intro shot 8: CRT2 needs the live ridge and the town behind it).</summary>
        IntroSections,
        /// <summary>S0_Booth loaded.</summary>
        Booth,
    }
}
