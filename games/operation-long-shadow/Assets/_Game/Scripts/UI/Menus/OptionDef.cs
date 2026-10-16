using System;
using LongShadow.Core;
using LongShadow.UI.Common;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// One option of GDD §11 bound to a <see cref="GameSettings"/> field: label, page, control kind, range or choices,
    /// and how to read, write and print it. Slider values are in display units (percent for volumes and grain).
    /// </summary>
    public sealed class OptionDef
    {
        /// <summary>Stable id (tests).</summary>
        public readonly string Id;
        /// <summary>Row label.</summary>
        public readonly string Label;
        /// <summary>Page (tab) name.</summary>
        public readonly string Page;
        /// <summary>Control kind.</summary>
        public readonly OptionKind Kind;
        /// <summary>Slider minimum (display units).</summary>
        public readonly float Min;
        /// <summary>Slider maximum (display units).</summary>
        public readonly float Max;
        /// <summary>Slider step (display units).</summary>
        public readonly float Step;
        /// <summary>Stepper choices.</summary>
        public readonly string[] Choices;
        /// <summary>Small print under the menu when the row is selected (e.g. difficulty timing), or null.</summary>
        public readonly string Note;

        private readonly Func<GameSettings, float> _get;
        private readonly Action<GameSettings, float> _set;
        private readonly Action<float, TextBuffer> _format;

        private OptionDef(string id, string label, string page, OptionKind kind, float min, float max, float step, string[] choices,
            Func<GameSettings, float> get, Action<GameSettings, float> set, Action<float, TextBuffer> format, string note)
        {
            Id = id;
            Label = label;
            Page = page;
            Kind = kind;
            Min = min;
            Max = max;
            Step = step;
            Choices = choices;
            _get = get;
            _set = set;
            _format = format;
            Note = note;
        }

        /// <summary>A slider option.</summary>
        public static OptionDef Slider(string id, string label, string page, float min, float max, float step,
            Func<GameSettings, float> get, Action<GameSettings, float> set, Action<float, TextBuffer> format, string note = null) =>
            new OptionDef(id, label, page, OptionKind.Slider, min, max, step, null, get, set, format, note);

        /// <summary>A choice option (the value is the index).</summary>
        public static OptionDef Choice(string id, string label, string page, string[] choices,
            Func<GameSettings, float> get, Action<GameSettings, float> set, string note = null) =>
            new OptionDef(id, label, page, OptionKind.Stepper, 0f, choices.Length - 1, 1f, choices, get, set, null, note);

        /// <summary>Reads the option from settings (display units or choice index).</summary>
        public float Get(GameSettings s) => s != null ? _get(s) : 0f;

        /// <summary>Writes the option into settings (callers sanitise afterwards).</summary>
        public void Set(GameSettings s, float value)
        {
            if (s != null) _set(s, value);
        }

        /// <summary>Prints a slider value.</summary>
        public void Format(float value, TextBuffer b)
        {
            if (_format != null) _format(value, b);
            else b.Append(value, 1);
        }

        /// <summary>The slider formatter (for <see cref="CrtSlider.Configure"/>).</summary>
        public Action<float, TextBuffer> Formatter => _format;
    }
}
