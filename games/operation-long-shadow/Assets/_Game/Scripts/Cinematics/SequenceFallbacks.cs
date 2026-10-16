using LongShadow.Core;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// What a sequence still does when its timeline has not been built yet (degraded mode, logged once): the booth
    /// camera if loaded and the sequence's lines in order, so the story and the mission flow keep working.
    /// </summary>
    public static class SequenceFallbacks
    {
        private static readonly string[] s_intro = { "D001", "D002", "D003", "D004", "D005", "D006", "D200", "D201", "D202", "D203", "D204", "D205", "D206", "D207" };
        private static readonly string[] s_midB = { "D054", "D055" };
        private static readonly string[] s_climax = { "D081", "D082" };
        private static readonly string[] s_outro = { "D083", "D084", "D085", "D086", "D087", "D088", "D089", "D090", "D091", "D092" };
        private static readonly string[] s_none = new string[0];

        /// <summary>Lines of a sequence in story order.</summary>
        public static string[] Lines(string sequenceId)
        {
            switch (sequenceId)
            {
                case SequenceIds.Intro: return s_intro;
                case SequenceIds.MidB: return s_midB;
                case SequenceIds.Climax: return s_climax;
                case SequenceIds.Outro: return s_outro;
                default: return s_none;
            }
        }
    }
}
