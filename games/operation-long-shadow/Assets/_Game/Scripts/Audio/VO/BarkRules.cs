using LongShadow.Audio.Core;

namespace LongShadow.Audio.VO
{
    /// <summary>Enemy bark rules (GDD §11, §14.4). Pure (unit-tested).</summary>
    public static class BarkRules
    {
        /// <summary>
        /// Barks are subtitled (with their DIALOGUE.json translation) only within 25 m and only when both subtitles and
        /// "Translate enemy chatter" are on (GDD §11).
        /// </summary>
        public static bool ShouldSubtitle(float distance, bool subtitlesOn, bool translateEnemyChatter) =>
            subtitlesOn && translateEnemyChatter && distance <= AudioConstants.BarkSubtitleDistance;

        /// <summary>Legion barks switch to the baked FAR chain beyond 30 m.</summary>
        public static bool UseFarChain(float distance) => distance > AudioConstants.BarkFarDistance;

        /// <summary>Bark voice "A" or "B" for a speaker: stable per enemy (one man never changes voice).</summary>
        public static string VoiceOf(int speakerKey) => (speakerKey & 1) == 0 ? "A" : "B";
    }
}
