using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>
    /// Formats objective lines exactly as GDD §7.12/§11: <c>OBJ 4/11 — 47219 — NEUTRALIZE THE TECHNICAL</c> (beat
    /// counter, five-digit flavour group = FNV-1a 32 of the id mod 100000, zero-padded, then the text). The counter and
    /// group are dimmed so the order reads first. Writes into a <see cref="TextBuffer"/> (no allocation).
    /// </summary>
    public static class ObjectiveFormatter
    {
        /// <summary>Separator between the parts (GDD: an em dash with spaces).</summary>
        public const string Separator = " — ";

        /// <summary>Writes the plain line (no rich text), clearing the buffer.</summary>
        public static void WritePlain(TextBuffer b, in ObjectiveDef o)
        {
            b.Clear();
            WritePrefix(b, o);
            b.Append(o.Text);
        }

        /// <summary>Writes the HUD line with a dimmed prefix, clearing the buffer.</summary>
        public static void Write(TextBuffer b, in ObjectiveDef o, Color prefixColor, Color textColor, bool struck = false)
        {
            b.Clear();
            b.OpenColor(prefixColor);
            WritePrefix(b, o);
            b.CloseColor();
            b.OpenColor(textColor);
            if (struck) b.Append("<s>");
            b.Append(string.IsNullOrEmpty(o.Text) ? "—" : o.Text);
            if (struck) b.Append("</s>");
            b.CloseColor();
        }

        /// <summary>Writes "OBJ i/n — ggggg — ".</summary>
        public static void WritePrefix(TextBuffer b, in ObjectiveDef o)
        {
            if (o.Optional) b.Append("OPT");
            else b.Append("OBJ ").Append(o.Index).Append('/').Append(o.Total);
            b.Append(Separator);
            WriteGroup(b, o);
            b.Append(Separator);
        }

        /// <summary>Writes the five-digit flavour group (the objective's own, or the one derived from its id).</summary>
        public static void WriteGroup(TextBuffer b, in ObjectiveDef o)
        {
            if (!string.IsNullOrEmpty(o.FlavourGroup)) b.Append(o.FlavourGroup);
            else b.AppendPadded(MathX.FlavourGroup(o.Id), 5);
        }

        /// <summary>Writes the intel line of the Tab list: "INTEL 1/3 · OPTIONAL 0/2" (GDD §11).</summary>
        public static void WriteIntel(TextBuffer b, int intel, int optional)
        {
            b.Clear();
            b.Append("INTEL ").Append(intel).Append("/3").Append("  ·  ").Append("OPTIONAL ").Append(optional).Append("/2");
        }
    }
}
