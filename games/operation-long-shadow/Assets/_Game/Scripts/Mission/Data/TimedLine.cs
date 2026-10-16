namespace LongShadow.Mission.Data
{
    /// <summary>
    /// A line at a fixed time in an authored sequence, or chained after the previous line (<see cref="AfterPrevious"/>):
    /// the fallback schedules used when no <c>ICinematicService</c> is registered (the Timelines are WP09's).
    /// </summary>
    public readonly struct TimedLine
    {
        /// <summary>Line id (DIALOGUE.json).</summary>
        public readonly string LineId;
        /// <summary>Seconds from the sequence start, or the gap after the previous line ends when <see cref="AfterPrevious"/>.</summary>
        public readonly float Time;
        /// <summary>True when the line follows the previous line's end plus <see cref="Time"/>.</summary>
        public readonly bool AfterPrevious;

        /// <summary>Creates an entry.</summary>
        public TimedLine(string lineId, float time, bool afterPrevious = false)
        {
            LineId = lineId;
            Time = time;
            AfterPrevious = afterPrevious;
        }
    }
}
