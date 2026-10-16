using System;
using TMPro;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Allocation-free text builder for HUD strings (TECH_PLAN R2): append numbers, padded numbers, fixed-point floats and
    /// colour tags into a reusable char array, then hand it to TMP with <see cref="ApplyTo"/>
    /// (<c>TMP_Text.SetText(char[], int, int)</c>, which copies into TMP's own buffers without allocating in players).
    /// Views rebuild a buffer only when the value it shows changes.
    /// </summary>
    public sealed class TextBuffer
    {
        private char[] _chars;
        private int _length;

        /// <summary>Creates a buffer with an initial capacity (it grows if needed, which allocates once).</summary>
        public TextBuffer(int capacity = 128)
        {
            _chars = new char[Mathf.Max(8, capacity)];
        }

        /// <summary>Characters written so far.</summary>
        public int Length => _length;

        /// <summary>The backing array (valid up to <see cref="Length"/>).</summary>
        public char[] Chars => _chars;

        /// <summary>Character at an index.</summary>
        public char this[int index] => _chars[index];

        /// <summary>Empties the buffer.</summary>
        public TextBuffer Clear()
        {
            _length = 0;
            return this;
        }

        /// <summary>Cuts the buffer to a length.</summary>
        public void Truncate(int length) => _length = Mathf.Clamp(length, 0, _length);

        /// <summary>Appends one character.</summary>
        public TextBuffer Append(char c)
        {
            Ensure(1);
            _chars[_length++] = c;
            return this;
        }

        /// <summary>Appends a character n times.</summary>
        public TextBuffer Append(char c, int count)
        {
            if (count <= 0) return this;
            Ensure(count);
            for (var i = 0; i < count; i++) _chars[_length++] = c;
            return this;
        }

        /// <summary>Appends a string (null appends nothing).</summary>
        public TextBuffer Append(string s)
        {
            if (string.IsNullOrEmpty(s)) return this;
            Ensure(s.Length);
            s.CopyTo(0, _chars, _length, s.Length);
            _length += s.Length;
            return this;
        }

        /// <summary>Appends part of a string.</summary>
        public TextBuffer Append(string s, int start, int count)
        {
            if (string.IsNullOrEmpty(s) || count <= 0) return this;
            start = Mathf.Clamp(start, 0, s.Length);
            count = Mathf.Min(count, s.Length - start);
            if (count <= 0) return this;
            Ensure(count);
            s.CopyTo(start, _chars, _length, count);
            _length += count;
            return this;
        }

        /// <summary>Appends part of a char array.</summary>
        public TextBuffer Append(char[] src, int start, int count)
        {
            if (src == null || count <= 0) return this;
            Ensure(count);
            Array.Copy(src, start, _chars, _length, count);
            _length += count;
            return this;
        }

        /// <summary>Appends another buffer.</summary>
        public TextBuffer Append(TextBuffer other) => other == null ? this : Append(other._chars, 0, other._length);

        /// <summary>Appends an integer in decimal.</summary>
        public TextBuffer Append(int value) => AppendPadded(value, 0, '0');

        /// <summary>Appends an integer padded on the left to <paramref name="width"/> digits with <paramref name="pad"/>.</summary>
        public TextBuffer AppendPadded(int value, int width, char pad = '0')
        {
            long v = value;
            var negative = v < 0;
            if (negative) v = -v;
            // Count digits.
            var digits = 1;
            for (var t = v; t >= 10; t /= 10) digits++;
            var padCount = Mathf.Max(0, width - digits - (negative ? 1 : 0));
            Ensure(digits + padCount + 1);
            if (negative && pad == '0') _chars[_length++] = '-';
            for (var i = 0; i < padCount; i++) _chars[_length++] = pad;
            if (negative && pad != '0') _chars[_length++] = '-';
            var end = _length + digits;
            for (var i = end - 1; i >= _length; i--)
            {
                _chars[i] = (char)('0' + (int)(v % 10));
                v /= 10;
            }
            _length = end;
            return this;
        }

        /// <summary>Appends a float with a fixed number of decimals (0..4), rounded half away from zero.</summary>
        public TextBuffer Append(float value, int decimals, bool forceSign = false)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return Append('-');
            decimals = Mathf.Clamp(decimals, 0, 4);
            var scale = 1;
            for (var i = 0; i < decimals; i++) scale *= 10;
            var scaled = (long)Math.Round(Math.Abs((double)value) * scale, MidpointRounding.AwayFromZero);
            var negative = value < 0f && scaled != 0;
            if (negative) Append('-');
            else if (forceSign) Append('+');
            var whole = scaled / scale;
            AppendLong(whole);
            if (decimals > 0)
            {
                Append('.');
                AppendPadded((int)(scaled % scale), decimals);
            }
            return this;
        }

        /// <summary>Appends a colour as RRGGBB hex (for <c>&lt;color=#RRGGBB&gt;</c>).</summary>
        public TextBuffer AppendHex(Color color, bool withAlpha = false)
        {
            Color32 c = color;
            AppendHexByte(c.r);
            AppendHexByte(c.g);
            AppendHexByte(c.b);
            if (withAlpha) AppendHexByte(c.a);
            return this;
        }

        /// <summary>Appends one byte as two hex digits.</summary>
        public TextBuffer AppendHexByte(byte b)
        {
            Ensure(2);
            _chars[_length++] = HexDigit(b >> 4);
            _chars[_length++] = HexDigit(b & 0xF);
            return this;
        }

        /// <summary>Appends <c>&lt;color=#RRGGBB&gt;</c>.</summary>
        public TextBuffer OpenColor(Color color) => Append("<color=#").AppendHex(color).Append('>');

        /// <summary>Appends <c>&lt;/color&gt;</c>.</summary>
        public TextBuffer CloseColor() => Append("</color>");

        /// <summary>Appends minutes:seconds ("14:02") for a duration in seconds (hours roll into minutes).</summary>
        public TextBuffer AppendClock(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return AppendPadded(total / 60, 2).Append(':').AppendPadded(total % 60, 2);
        }

        /// <summary>True when the contents equal a string (ordinal).</summary>
        public bool ContentEquals(string s)
        {
            if (s == null) return _length == 0;
            if (s.Length != _length) return false;
            for (var i = 0; i < _length; i++)
                if (_chars[i] != s[i]) return false;
            return true;
        }

        /// <summary>True when the contents equal another buffer.</summary>
        public bool ContentEquals(TextBuffer other)
        {
            if (other == null || other._length != _length) return false;
            for (var i = 0; i < _length; i++)
                if (_chars[i] != other._chars[i]) return false;
            return true;
        }

        /// <summary>Copies this buffer's contents into another (no allocation once the target has grown).</summary>
        public void CopyTo(TextBuffer target)
        {
            if (target == null) return;
            target.Clear().Append(_chars, 0, _length);
        }

        /// <summary>Sends the contents to a TMP text (no allocation in players).</summary>
        public void ApplyTo(TMP_Text text)
        {
            if (text != null) text.SetText(_chars, 0, _length);
        }

        /// <summary>The contents as a new string (allocates: tests, logs and one-off labels only).</summary>
        public override string ToString() => new string(_chars, 0, _length);

        private TextBuffer AppendLong(long v)
        {
            var digits = 1;
            for (var t = v; t >= 10; t /= 10) digits++;
            Ensure(digits);
            var end = _length + digits;
            for (var i = end - 1; i >= _length; i--)
            {
                _chars[i] = (char)('0' + (int)(v % 10));
                v /= 10;
            }
            _length = end;
            return this;
        }

        private void Ensure(int extra)
        {
            var need = _length + extra;
            if (need <= _chars.Length) return;
            var size = _chars.Length * 2;
            while (size < need) size *= 2;
            Array.Resize(ref _chars, size);
        }

        private static char HexDigit(int v) => (char)(v < 10 ? '0' + v : 'A' + v - 10);
    }
}
