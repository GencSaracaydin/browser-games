using System.Collections.Generic;

namespace LongShadow.World.Level.Editor.Markup
{
    /// <summary>Result of a <see cref="MarkupValidator"/> run.</summary>
    public sealed class MarkupReport
    {
        /// <summary>Errors.</summary>
        public readonly List<string> Errors = new List<string>();
        /// <summary>Warnings.</summary>
        public readonly List<string> Warnings = new List<string>();
        /// <summary>Ids checked.</summary>
        public int Checked;

        /// <summary>One-line summary.</summary>
        public string Summary => Checked + " ids, " + Errors.Count + " errors, " + Warnings.Count + " warnings";
    }
}
