namespace LongShadow.Core
{
    /// <summary>Weapon fire mode shown by the HUD ammo panel (GDD §11, §13.1).</summary>
    public enum FireMode
    {
        Semi = 0,
        Auto = 1,
        Burst = 2,
        /// <summary>Bolt/single-shot launchers and grenades.</summary>
        Single = 3,
    }
}
