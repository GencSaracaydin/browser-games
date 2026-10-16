namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Sample math of the gapless two-source loops (TECH_PLAN §3.12 Web rules; WP06_SFX import notes: clips are laid out
    /// [lead-in][loop body][tail-out], play the body back to back with <c>PlayScheduled</c>/<c>SetScheduledEndTime</c>
    /// and never loop the whole clip, because AAC adds priming silence at the clip start). Pure (unit-tested).
    /// </summary>
    public static class LoopSchedule
    {
        /// <summary>
        /// Converts a loop region in seconds to samples for a clip of <paramref name="lengthSamples"/> at
        /// <paramref name="frequency"/> Hz. Returns false (whole clip loops) when the region is missing or invalid.
        /// </summary>
        public static bool ToSamples(float startSeconds, float endSeconds, int frequency, int lengthSamples, out int start, out int end)
        {
            start = 0;
            end = lengthSamples;
            if (frequency <= 0 || lengthSamples <= 0 || endSeconds <= 0f || endSeconds <= startSeconds) return false;
            start = (int)System.Math.Round(startSeconds * frequency);
            end = (int)System.Math.Round(endSeconds * frequency);
            if (start < 0) start = 0;
            if (end > lengthSamples) end = lengthSamples;
            if (end - start < frequency / 20) // < 50 ms of body: not a usable loop region
            {
                start = 0;
                end = lengthSamples;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Where a loop that should resume at <paramref name="position"/> starts: positions inside the body stay, a
        /// position in the tail-out (or past the end) wraps back into the body, negative positions start at 0.
        /// </summary>
        public static int ClampStart(int position, int loopStart, int loopEnd)
        {
            if (position < 0) return 0;
            if (position < loopEnd) return position;
            var body = loopEnd - loopStart;
            if (body <= 0) return loopStart;
            return loopStart + (position - loopStart) % body;
        }

        /// <summary>Seconds from <paramref name="startSample"/> to the end of the body (first segment).</summary>
        public static double FirstSegmentSeconds(int startSample, int loopStart, int loopEnd, int frequency)
        {
            var s = ClampStart(startSample, loopStart, loopEnd);
            return frequency <= 0 ? 0d : (loopEnd - s) / (double)frequency;
        }

        /// <summary>Seconds of one loop body.</summary>
        public static double BodySeconds(int loopStart, int loopEnd, int frequency) =>
            frequency <= 0 ? 0d : (loopEnd - loopStart) / (double)frequency;

        /// <summary>
        /// Clip position (samples) reached <paramref name="elapsedSeconds"/> after starting at <paramref name="startSample"/>,
        /// wrapping inside the body.
        /// </summary>
        public static int PositionAfter(int startSample, double elapsedSeconds, int loopStart, int loopEnd, int frequency)
        {
            var s = ClampStart(startSample, loopStart, loopEnd);
            var p = s + (long)System.Math.Round(elapsedSeconds * frequency);
            if (p < loopEnd) return (int)p;
            var body = loopEnd - loopStart;
            if (body <= 0) return loopStart;
            return loopStart + (int)((p - loopStart) % body);
        }
    }
}
