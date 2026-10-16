using System;

namespace LongShadow.Audio.Music
{
    /// <summary>
    /// The musical grid of a playing cue (bar lines from WP06_SFX <c>LOOP_POINTS.json</c>: loops are cut on bar lines, so
    /// the grid is anchored at the loop start and stays aligned through every wrap). Times are DSP seconds. Pure
    /// (unit-tested).
    /// </summary>
    public readonly struct MusicClock
    {
        /// <summary>DSP time at which clip time 0 would have played (start DSP − start offset).</summary>
        public readonly double AnchorDsp;
        /// <summary>Loop start in clip seconds.</summary>
        public readonly double LoopStart;
        /// <summary>Loop end in clip seconds (≤ start = no loop).</summary>
        public readonly double LoopEnd;
        /// <summary>Seconds per bar (0 = no grid).</summary>
        public readonly double Bar;

        /// <summary>Creates a clock.</summary>
        public MusicClock(double anchorDsp, double loopStart, double loopEnd, double bar)
        {
            AnchorDsp = anchorDsp;
            LoopStart = Math.Max(0d, loopStart);
            LoopEnd = loopEnd;
            Bar = Math.Max(0d, bar);
        }

        /// <summary>True when the cue has a bar grid.</summary>
        public bool HasGrid => Bar > 0.05d;

        /// <summary>Loop length in seconds (0 = none).</summary>
        public double LoopLength => LoopEnd > LoopStart ? LoopEnd - LoopStart : 0d;

        /// <summary>Clip time playing at <paramref name="dsp"/> (wrapped into the loop once past its end).</summary>
        public double ClipTimeAt(double dsp)
        {
            var u = dsp - AnchorDsp;
            var len = LoopLength;
            if (len <= 0d || u < LoopEnd) return u;
            return LoopStart + (u - LoopStart) % len;
        }

        /// <summary>
        /// DSP time of the next grid line of size <paramref name="unit"/> (a bar, or a beat = bar / 4) at least
        /// <paramref name="minLead"/> seconds after <paramref name="dsp"/>. Without a grid returns dsp + minLead.
        /// </summary>
        public double NextBoundary(double dsp, double unit, double minLead)
        {
            if (unit <= 0.01d) return dsp + minLead;
            var u = dsp - AnchorDsp; // unwrapped music time
            var k = Math.Ceiling((u - LoopStart) / unit - 1e-9);
            var t = AnchorDsp + LoopStart + k * unit;
            while (t - dsp < minLead) t += unit;
            return t;
        }

        /// <summary>Next bar line.</summary>
        public double NextBar(double dsp, double minLead) => HasGrid ? NextBoundary(dsp, Bar, minLead) : dsp + minLead;

        /// <summary>Next beat (4/4 assumed: bar / 4).</summary>
        public double NextBeat(double dsp, double minLead) => HasGrid ? NextBoundary(dsp, Bar / 4d, minLead) : dsp + minLead;
    }
}
