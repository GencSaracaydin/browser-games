using LongShadow.Audio.Core;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Per-beat decoding windows (TECH_PLAN §7.10, §3.12): VO clips import with Preload Audio Data off;
    /// <c>PrepareWindow(from, to)</c> loads the lines of beats [from, to] plus the always-needed sets (callouts, barks,
    /// death inserts, the OOB warning) and unloads lines of beats that have passed (beat &lt; from). Future beats are left
    /// alone. Pure decision (unit-tested).
    /// </summary>
    public static class BeatWindow
    {
        /// <summary>What to do with a line's audio data.</summary>
        public enum Action
        {
            /// <summary>Leave as is.</summary>
            Keep,
            /// <summary>Call <c>LoadAudioData</c>.</summary>
            Load,
            /// <summary>Call <c>UnloadAudioData</c> (unless the line is playing).</summary>
            Unload,
        }

        /// <summary>Decision for a line of <paramref name="lineBeat"/> when the window is [from, to].</summary>
        public static Action Decide(int lineBeat, int from, int to)
        {
            if (to < from)
            {
                var t = to;
                to = from;
                from = t;
            }
            if (lineBeat == AudioConstants.AlwaysBeat) return Action.Load;
            if (lineBeat >= from && lineBeat <= to) return Action.Load;
            if (lineBeat < from) return Action.Unload;
            return Action.Keep;
        }
    }
}
