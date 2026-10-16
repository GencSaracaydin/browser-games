using LongShadow.Core;
using LongShadow.Mission.Data;

namespace LongShadow.Mission.Objectives
{
    /// <summary>HUD text of an objective (GDD §7.12): <c>OBJ &lt;beat&gt;/11 — &lt;group&gt; — &lt;TEXT&gt;</c>.</summary>
    public static class ObjectiveFormat
    {
        /// <summary>The five-digit flavour group: FNV-1a 32-bit of the id mod 100000, zero-padded (GDD §7.12).</summary>
        public static string Group(string objectiveId) => MathX.FlavourGroup(objectiveId ?? string.Empty).ToString("D5");

        /// <summary>The full HUD line of an objective.</summary>
        public static string HudLine(in ObjectiveDef def)
        {
            var group = string.IsNullOrEmpty(def.FlavourGroup) ? Group(def.Id) : def.FlavourGroup;
            return "OBJ " + def.Index + "/" + (def.Total > 0 ? def.Total : BeatTable.PlayableBeats) + " — " + group + " — " + def.Text;
        }

        /// <summary>A counter objective text ("OBSERVE ZAROUD 2/3"): replaces a trailing "n/m".</summary>
        public static string WithCounter(string text, int count, int total)
        {
            if (string.IsNullOrEmpty(text)) return count + "/" + total;
            var space = text.LastIndexOf(' ');
            var head = space >= 0 && text.IndexOf('/', space) > space ? text.Substring(0, space) : text;
            return head + " " + count + "/" + total;
        }
    }
}
