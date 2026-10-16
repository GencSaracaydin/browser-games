using LongShadow.Core;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Five-digit cipher groups ("74410 00640 …"), the visual signature of the click gate (GDD §8.1), the title card
    /// (§8.3) and the debrief. Deterministic xorshift32 so a seed always prints the same groups (tests, replays).
    /// Allocation-free: everything writes into caller-owned char buffers.
    /// </summary>
    public static class NumberGroups
    {
        /// <summary>Digits per group.</summary>
        public const int GroupSize = 5;

        /// <summary>Next xorshift32 value; the state must be non-zero (a zero state is replaced by a constant).</summary>
        public static uint Next(ref uint state)
        {
            if (state == 0u) state = 0x9E3779B9u;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>A random decimal digit character.</summary>
        public static char Digit(ref uint state) => (char)('0' + (int)(Next(ref state) % 10u));

        /// <summary>Seed derived from a text (FNV-1a), never zero.</summary>
        public static uint Seed(string text) => MathX.Fnv1a32(text) | 1u;

        /// <summary>
        /// Writes <paramref name="groups"/> groups of <see cref="GroupSize"/> digits separated by single spaces at
        /// <paramref name="index"/>; returns the index after the last character written. The buffer must be large enough
        /// (groups × 6 − 1 characters).
        /// </summary>
        public static int Write(char[] buffer, int index, int groups, ref uint state)
        {
            for (var g = 0; g < groups; g++)
            {
                if (g > 0) buffer[index++] = ' ';
                for (var d = 0; d < GroupSize; d++) buffer[index++] = Digit(ref state);
            }
            return index;
        }

        /// <summary>
        /// Fills <paramref name="length"/> characters with the number-group pattern ("ddddd ddddd d…": a space after every
        /// fifth digit), the "uncorrected" form of a title-card line of the same length (GDD §8.3).
        /// </summary>
        public static void WritePattern(char[] buffer, int length, ref uint state)
        {
            for (var i = 0; i < length; i++)
                buffer[i] = IsGroupGap(i) ? ' ' : Digit(ref state);
        }

        /// <summary>True when position <paramref name="i"/> of a group pattern is the space between two groups.</summary>
        public static bool IsGroupGap(int i) => i % (GroupSize + 1) == GroupSize;
    }
}
