using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// G9 positional radio (GDD §0.3, §8.5; TECH_PLAN §7.9): a line starts in 3D from a speaker (Tinker's PRC-77, 2 m
    /// away) and jumps onto everyone's headset as 2D radio at a given time (<c>D039</c> at the first "Seven", 1.83 s).
    /// The same clip plays on a 3D and a 2D source in sync; this gives their gains at a playback time. Played through
    /// <c>IVOService.Play</c> with <c>VOOptions { Speaker, ForceDry = true, RadioSwitchTime }</c> (0 uses the line's
    /// default from <c>LIB_Dialogue</c>). Pure (unit-tested).
    /// </summary>
    public static class PositionalRadio
    {
        /// <summary>Seconds of the 3D → 2D crossfade (a short, audible "jump").</summary>
        public const float Crossfade = 0.12f;

        /// <summary>Gains of the 3D and 2D sources at playback time <paramref name="t"/>.</summary>
        public static void Weights(float t, float switchTime, out float positional, out float radio)
        {
            if (switchTime <= 0f)
            {
                positional = 1f;
                radio = 0f;
                return;
            }
            radio = Mathf.Clamp01((t - switchTime) / Crossfade);
            positional = 1f - radio;
        }
    }
}
