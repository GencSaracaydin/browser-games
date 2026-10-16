namespace LongShadow.Core
{
    /// <summary>Locomotion state of the player or a character body.</summary>
    public enum Stance
    {
        Stand = 0,
        Crouch = 1,
        Sprint = 2,
        Slide = 3,
        Airborne = 4,
        Mantle = 5,
        Ladder = 6,
        /// <summary>Seated on a mount (door gun, vehicle seat).</summary>
        Mounted = 7,
    }
}
