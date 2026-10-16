using LongShadow.UI.Common;
using UnityEngine;

namespace LongShadow.UI.Screens
{
    /// <summary>
    /// Timeline of the mission title card (GDD §8.3), as a pure function of time so it can be tested and scrubbed:
    /// bars slide in (0.5 s), then each of the four lines prints character by character as five-digit number groups,
    /// is "corrected" left to right into the real text in 0.4 s, and line 3 reads 18:40 for 6 frames (0.1 s at the 60 fps
    /// reference, at least 2 rendered frames: the view enforces that) before settling to 17:58. The card holds, then the
    /// bars retract. Timings scale down if the card is given less than 8 s.
    /// </summary>
    public sealed class TitleCardScript
    {
        /// <summary>The four lines (GDD §8.3).</summary>
        public static readonly string[] Lines =
        {
            "OPERATION LONG SHADOW",
            "ZAROUD — REPUBLIC OF SARDAHN",
            "21 OCTOBER 1984 — 17:58 LOCAL",
            "SFC NATHAN CALDER — \"SUNDIAL-3\"",
        };

        /// <summary>Line that shows the wrong time first.</summary>
        public const int FlashLine = 2;
        /// <summary>The wrong time shown for 6 frames.</summary>
        public const string FlashTime = "18:40";
        /// <summary>The real time.</summary>
        public const string RealTime = "17:58";
        /// <summary>6 frames at the 60 fps reference (GDD §8.4 convention).</summary>
        public const float FlashSeconds = 0.1f;
        /// <summary>Reference length (GDD §8.3).</summary>
        public const float ReferenceSeconds = 8f;
        /// <summary>Bars slide time (GDD §8.3: 0.5 s).</summary>
        public const float BarsSeconds = 0.5f;
        /// <summary>Correction time per line (GDD §8.3: 0.4 s).</summary>
        public const float CorrectSeconds = 0.4f;
        /// <summary>Print speed of the number groups (characters per second).</summary>
        public const float PrintCps = 30f;
        private const float LineGap = 0.15f;

        private readonly float[] _start = new float[4];
        private readonly float[] _printEnd = new float[4];
        private readonly float[] _correctEnd = new float[4];
        private readonly uint[] _seeds = new uint[4];
        private readonly int _flashIndex;

        /// <summary>Total length in seconds.</summary>
        public float Duration { get; }

        /// <summary>When the bars start to retract.</summary>
        public float BarsOutAt { get; }

        /// <summary>Time scale applied to printing (1 at 8 s).</summary>
        public float Scale { get; }

        /// <summary>Builds the timeline for a card of <paramref name="seconds"/> (default 8).</summary>
        public TitleCardScript(float seconds = ReferenceSeconds)
        {
            Duration = seconds > 0f ? seconds : ReferenceSeconds;
            var natural = BarsSeconds * 2f + 0.8f; // bars in/out and a minimum hold
            for (var i = 0; i < Lines.Length; i++) natural += Lines[i].Length / PrintCps + CorrectSeconds + LineGap;
            Scale = Mathf.Min(1f, Duration / natural);
            var t = BarsSeconds;
            for (var i = 0; i < Lines.Length; i++)
            {
                _start[i] = t;
                _printEnd[i] = t + Lines[i].Length / PrintCps * Scale;
                _correctEnd[i] = _printEnd[i] + CorrectSeconds * Scale;
                t = _correctEnd[i] + LineGap * Scale;
                if (i == FlashLine) t += FlashSeconds;
                _seeds[i] = NumberGroups.Seed(Lines[i]);
            }
            BarsOutAt = Duration - BarsSeconds;
            _flashIndex = Lines[FlashLine].IndexOf(RealTime, System.StringComparison.Ordinal);
        }

        /// <summary>Start of a line's printing.</summary>
        public float LineStart(int line) => _start[line];
        /// <summary>End of a line's number-group printing.</summary>
        public float PrintEnd(int line) => _printEnd[line];
        /// <summary>End of a line's correction.</summary>
        public float CorrectEnd(int line) => _correctEnd[line];
        /// <summary>Start of the 18:40 flash (line 3).</summary>
        public float FlashStart => _correctEnd[FlashLine];

        /// <summary>
        /// Writes line <paramref name="line"/> as it reads at time <paramref name="t"/> into <paramref name="buffer"/>
        /// (cleared first) and returns how many characters are visible. <paramref name="showFlash"/> forces the 18:40
        /// variant (the view holds it for at least two rendered frames).
        /// </summary>
        public int Evaluate(int line, float t, TextBuffer buffer, bool showFlash = false)
        {
            buffer.Clear();
            var target = Lines[line];
            var n = target.Length;
            if (t < _start[line]) return 0;

            var seed = _seeds[line];
            var visible = t >= _printEnd[line] ? n : Mathf.Clamp(Mathf.FloorToInt((t - _start[line]) * PrintCps / Scale), 0, n);
            var corrected = t >= _correctEnd[line] ? n :
                t <= _printEnd[line] ? 0 : Mathf.Clamp(Mathf.FloorToInt((t - _printEnd[line]) / (_correctEnd[line] - _printEnd[line]) * n), 0, n);
            // Line 3 corrects into the wrong time (18:40), holds it 6 frames, then settles to 17:58.
            var flash = line == FlashLine && _flashIndex >= 0 && (showFlash || t < FlashStart + FlashSeconds);

            for (var i = 0; i < n; i++)
            {
                // Digits are a fixed function of the line and position, so they do not flicker between frames.
                var digit = NumberGroups.Digit(ref seed);
                char c;
                if (i < corrected) c = target[i];
                else c = NumberGroups.IsGroupGap(i) ? ' ' : digit;
                if (flash && i < corrected && i >= _flashIndex && i < _flashIndex + FlashTime.Length) c = FlashTime[i - _flashIndex];
                buffer.Append(c);
            }
            return visible;
        }

        /// <summary>True while line 3 should read 18:40 (before the frame guard).</summary>
        public bool IsFlashing(float t) => t >= FlashStart && t < FlashStart + FlashSeconds;
    }
}
