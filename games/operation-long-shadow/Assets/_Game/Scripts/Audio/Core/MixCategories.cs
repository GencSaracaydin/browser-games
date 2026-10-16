using LongShadow.Core;

namespace LongShadow.Audio.Core
{
    /// <summary>String ↔ <see cref="MixCategory"/> mapping and per-category rules.</summary>
    public static class MixCategories
    {
        /// <summary>Number of categories.</summary>
        public const int Count = 7;

        /// <summary>Name used for the internal tinnitus category.</summary>
        public const string TinnitusName = "Tinnitus";

        /// <summary>
        /// Category of a name from <see cref="AudioCategories"/> (case-sensitive, as the constants). Unknown names and
        /// null map to <see cref="MixCategory.Effects"/>. "Master" is not a voice category: it maps to Effects here and
        /// is handled by <see cref="AudioMix.SetCategoryGain"/> as the global gain.
        /// </summary>
        public static MixCategory Parse(string name)
        {
            switch (name)
            {
                case AudioCategories.Music: return MixCategory.Music;
                case AudioCategories.Voice: return MixCategory.Voice;
                case AudioCategories.UI: return MixCategory.UI;
                case AudioCategories.Insert: return MixCategory.Insert;
                case AudioCategories.Ambience: return MixCategory.Ambience;
                case TinnitusName: return MixCategory.Tinnitus;
                default: return MixCategory.Effects;
            }
        }

        /// <summary>The public name of a category.</summary>
        public static string Name(MixCategory c)
        {
            switch (c)
            {
                case MixCategory.Music: return AudioCategories.Music;
                case MixCategory.Voice: return AudioCategories.Voice;
                case MixCategory.UI: return AudioCategories.UI;
                case MixCategory.Insert: return AudioCategories.Insert;
                case MixCategory.Ambience: return AudioCategories.Ambience;
                case MixCategory.Tinnitus: return TinnitusName;
                default: return AudioCategories.Effects;
            }
        }

        /// <summary>
        /// UI and Insert sources keep playing while <c>AudioListener.pause</c> is set (pause menu, glitch inserts,
        /// TECH_PLAN §5.4, GDD §5.5).
        /// </summary>
        public static bool IgnoresListenerPause(MixCategory c) => c == MixCategory.UI || c == MixCategory.Insert;

        /// <summary>
        /// Categories whose pitch follows slow motion (CINEMATIC_DIRECTION §1 "speed ramps with an audio pitch dip");
        /// music, voice and UI never bend.
        /// </summary>
        public static bool FollowsSlowMotion(MixCategory c) => c == MixCategory.Effects || c == MixCategory.Ambience;

        /// <summary>Unity AudioSource.priority (0 = most important) so VO and music are never virtualised first.</summary>
        public static int UnityPriority(MixCategory c, int cuePriority)
        {
            switch (c)
            {
                case MixCategory.Voice: return 8;
                case MixCategory.Music: return 16;
                case MixCategory.Insert: return 4;
                case MixCategory.UI: return 24;
                case MixCategory.Ambience: return 48;
                case MixCategory.Tinnitus: return 20;
                default:
                    // Effects: importance 0..100 -> Unity 200..64.
                    var p = cuePriority < 0 ? 0 : cuePriority > 100 ? 100 : cuePriority;
                    return 200 - (p * 136) / 100;
            }
        }
    }
}
