namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Mix categories of the AudioService (TECH_PLAN D9). The string names of <see cref="LongShadow.Core.AudioCategories"/>
    /// map onto these with <see cref="MixCategories.Parse"/>; <see cref="Tinnitus"/> is internal (the whine itself must
    /// not be ducked by the tinnitus muffle).
    /// </summary>
    public enum MixCategory
    {
        Music = 0,
        Effects = 1,
        Voice = 2,
        UI = 3,
        Insert = 4,
        Ambience = 5,
        Tinnitus = 6,
    }
}
