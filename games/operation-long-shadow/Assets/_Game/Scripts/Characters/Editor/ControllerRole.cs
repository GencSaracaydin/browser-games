namespace LongShadow.Characters.Editor
{
    /// <summary>Which controller a body gets: AC_Soldier itself or one of its override controllers (TECH_PLAN §7.8).</summary>
    public enum ControllerRole
    {
        /// <summary>AC_Soldier (base clips).</summary>
        Soldier = 0,
        /// <summary>AOC_Legion: irregulars (looser run).</summary>
        Legion = 1,
        /// <summary>AOC_Greycoat: professional advisors.</summary>
        Greycoat = 2,
        /// <summary>AOC_Squad: Hollis, Sparrow, Tinker (and Calder's third-person body in the insertion).</summary>
        Squad = 3,
        /// <summary>AOC_Civilian: unarmed locomotion (interrogator, Harrou, booth Calder).</summary>
        Civilian = 4,
    }
}
