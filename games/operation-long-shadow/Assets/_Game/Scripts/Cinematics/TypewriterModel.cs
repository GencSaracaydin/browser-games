using System.Text;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Text reveal of the teletype/typewriter titles (GDD §8.2 shot 1, §8.3 title card, §8.4 "SESSION 12 — RESUMING").
    /// Lines type one after another at <see cref="CharsPerSecond"/>. With <see cref="NumberGroups"/> each line first prints
    /// as five-digit number groups of the same length that are then "corrected" into the real text left to right over
    /// <see cref="CorrectSeconds"/> (0.4 s per line, GDD §8.3). An optional flicker shows a substitute string inside one
    /// line for a short window (line 3 reads 18:40 for 6 frames before settling to 17:58). Allocation-free after
    /// construction: <see cref="Build"/> writes into a caller's StringBuilder.
    /// </summary>
    public sealed class TypewriterModel
    {
        private readonly string[] _lines;
        private readonly string[] _numbers;
        private readonly float[] _starts;
        private readonly float _total;

        /// <summary>Characters per second.</summary>
        public float CharsPerSecond { get; }
        /// <summary>Print number groups first, then correct them (title card).</summary>
        public bool NumberGroups { get; }
        /// <summary>Correction time per line in seconds.</summary>
        public float CorrectSeconds { get; }
        /// <summary>Pause between lines in seconds.</summary>
        public float LineGap { get; }

        /// <summary>Line index of the flicker (-1 = none).</summary>
        public int FlickerLine { get; private set; } = -1;
        private string _flickerFind;
        private string _flickerShow;
        private float _flickerDelay;
        private float _flickerSeconds;

        /// <summary>Creates a model for <paramref name="text"/> ('\n' separates lines).</summary>
        public TypewriterModel(string text, float charsPerSecond, bool numberGroups, float correctSeconds = 0.4f,
                               float lineGap = 0.12f, int seed = 1840)
        {
            _lines = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            CharsPerSecond = charsPerSecond <= 0f ? 30f : charsPerSecond;
            NumberGroups = numberGroups;
            CorrectSeconds = numberGroups ? correctSeconds : 0f;
            LineGap = lineGap;
            _numbers = new string[_lines.Length];
            _starts = new float[_lines.Length];
            var t = 0f;
            var rng = (uint)(seed == 0 ? 1 : seed);
            for (var i = 0; i < _lines.Length; i++)
            {
                _starts[i] = t;
                _numbers[i] = NumberGroups ? NumberLine(_lines[i].Length, ref rng) : _lines[i];
                t += _lines[i].Length / CharsPerSecond + CorrectSeconds + LineGap;
            }
            _total = t;
        }

        /// <summary>Total reveal time in seconds.</summary>
        public float Duration => _total;

        /// <summary>Number of lines.</summary>
        public int LineCount => _lines.Length;

        /// <summary>Start time of a line.</summary>
        public float LineStart(int line) => line >= 0 && line < _starts.Length ? _starts[line] : 0f;

        /// <summary>The number-group version of a line (same length as the line).</summary>
        public string NumbersOf(int line) => line >= 0 && line < _numbers.Length ? _numbers[line] : string.Empty;

        /// <summary>
        /// Shows <paramref name="show"/> in place of <paramref name="find"/> on a line for <paramref name="seconds"/>,
        /// starting <paramref name="delayAfterCorrection"/> seconds after that line's correction ends.
        /// </summary>
        public void SetFlicker(int line, string find, string show, float delayAfterCorrection, float seconds)
        {
            if (line < 0 || line >= _lines.Length || string.IsNullOrEmpty(find) || show == null || find.Length != show.Length) return;
            if (_lines[line].IndexOf(find, System.StringComparison.Ordinal) < 0) return;
            FlickerLine = line;
            _flickerFind = find;
            _flickerShow = show;
            _flickerDelay = delayAfterCorrection;
            _flickerSeconds = seconds;
        }

        /// <summary>Number of characters typed so far (all lines, ignoring line breaks).</summary>
        public int TypedCount(float t)
        {
            var n = 0;
            for (var i = 0; i < _lines.Length; i++)
            {
                var local = t - _starts[i];
                if (local <= 0f) break;
                n += System.Math.Min(_lines[i].Length, (int)(local * CharsPerSecond));
            }
            return n;
        }

        /// <summary>Writes the visible text at time <paramref name="t"/> into <paramref name="sb"/> (cleared first).</summary>
        public void Build(StringBuilder sb, float t)
        {
            sb.Length = 0;
            for (var i = 0; i < _lines.Length; i++)
            {
                var local = t - _starts[i];
                if (local < 0f) break;
                if (i > 0) sb.Append('\n');
                var line = _lines[i];
                var len = line.Length;
                var typeDur = len / CharsPerSecond;
                if (local < typeDur)
                {
                    var k = (int)(local * CharsPerSecond);
                    sb.Append(_numbers[i], 0, System.Math.Min(k, len));
                    continue;
                }
                var c = CorrectSeconds > 0f ? (local - typeDur) / CorrectSeconds : 1f;
                if (c < 1f)
                {
                    var real = (int)(c * len);
                    sb.Append(line, 0, real);
                    sb.Append(_numbers[i], real, len - real);
                    continue;
                }
                if (i == FlickerLine)
                {
                    var since = local - typeDur - CorrectSeconds - _flickerDelay;
                    if (since >= 0f && since < _flickerSeconds)
                    {
                        var at = line.IndexOf(_flickerFind, System.StringComparison.Ordinal);
                        sb.Append(line, 0, at);
                        sb.Append(_flickerShow);
                        sb.Append(line, at + _flickerFind.Length, len - at - _flickerFind.Length);
                        continue;
                    }
                }
                sb.Append(line);
            }
        }

        /// <summary>Five-digit groups separated by spaces, exactly <paramref name="length"/> characters.</summary>
        private static string NumberLine(int length, ref uint rng)
        {
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                if (i % 6 == 5)
                {
                    chars[i] = ' ';
                    continue;
                }
                rng ^= rng << 13;
                rng ^= rng >> 17;
                rng ^= rng << 5;
                chars[i] = (char)('0' + (int)(rng % 10u));
            }
            return new string(chars);
        }
    }
}
