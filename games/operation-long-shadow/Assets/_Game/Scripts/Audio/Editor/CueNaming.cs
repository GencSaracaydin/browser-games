using System;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// File-name convention of the non-VO audio (TECH_PLAN §4.2: <c>SFX_&lt;Group&gt;_&lt;Name&gt;_&lt;nn&gt;</c>,
    /// <c>MUS_&lt;Id&gt;_&lt;Layer&gt;</c>, <c>AMB_&lt;Name&gt;</c>, <c>STG_&lt;Name&gt;</c>; WP06_SFX deliveries): a trailing
    /// two-digit <c>_nn</c> is a variant, a <c>_FAR</c> before it (or a <c>Far</c> folder) marks the baked distance
    /// variant, everything else is the cue id. Sliced takes keep their slice in the id
    /// (<c>SFX_WPN_AK_RELOAD_MAGOUT_01</c> → <c>SFX_WPN_AK_RELOAD_MAGOUT</c>). VO files (<c>Audio/VO</c>) and
    /// envelopes are not cues. Pure (unit-tested).
    /// </summary>
    public static class CueNaming
    {
        /// <summary>Folder all audio lives under.</summary>
        public const string AudioRoot = "Assets/_Game/Audio/";

        /// <summary>Parses a project path; false for files that are not cue clips.</summary>
        public static bool TryParse(string assetPath, out CueFile file)
        {
            file = default;
            if (string.IsNullOrEmpty(assetPath)) return false;
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(AudioRoot, StringComparison.Ordinal)) return false;
            var rel = p.Substring(AudioRoot.Length);
            var slash = rel.LastIndexOf('/');
            if (slash <= 0) return false;
            var folder = rel.Substring(0, slash);
            var root = folder.Split('/')[0];
            if (root != "SFX" && root != "Music" && root != "Ambience") return false;
            var name = rel.Substring(slash + 1);
            var dot = name.LastIndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            if (name.Length < 3) return false;

            var variant = 0;
            if (name.Length > 3 && name[name.Length - 3] == '_' && char.IsDigit(name[name.Length - 2]) && char.IsDigit(name[name.Length - 1]))
            {
                variant = (name[name.Length - 2] - '0') * 10 + (name[name.Length - 1] - '0');
                name = name.Substring(0, name.Length - 3);
            }
            var far = folder.EndsWith("/Far", StringComparison.Ordinal);
            if (name.EndsWith("_FAR", StringComparison.Ordinal))
            {
                far = true;
                name = name.Substring(0, name.Length - 4);
            }
            if (name.Length == 0 || name.IndexOf('_') < 0) return false;
            var group = far && folder.EndsWith("/Far", StringComparison.Ordinal) ? folder.Substring(0, folder.Length - 4) : folder;
            file = new CueFile { Path = p, CueId = name, Far = far, Variant = variant, Group = group, Root = root };
            return true;
        }

        /// <summary>The SFX group of a group path ("SFX/Weapons" → "Weapons"), or "" for Music/Ambience.</summary>
        public static string SfxGroup(string group)
        {
            if (string.IsNullOrEmpty(group) || !group.StartsWith("SFX/", StringComparison.Ordinal)) return "";
            var rest = group.Substring(4);
            var s = rest.IndexOf('/');
            return s < 0 ? rest : rest.Substring(0, s);
        }
    }
}
