namespace LongShadow.Core
{
    /// <summary>
    /// Scene names, asset paths and Addressables keys of the streaming sections (GDD §6.7, TECH_PLAN §5.1). The
    /// Addressables address of a section scene is its scene name ("S2_Market"); WP01's group rules and WP10's scene
    /// builders both use these helpers.
    /// </summary>
    public static class Sections
    {
        /// <summary>The only Build Settings scene (TECH_PLAN D1).</summary>
        public const string BootScene = "S_Boot";
        /// <summary>Addressable scene loaded at the click gate and never unloaded.</summary>
        public const string PersistentScene = "S_Persistent";
        /// <summary>Folder of S_Boot and S_Persistent.</summary>
        public const string ScenesFolder = "Assets/_Game/Scenes";
        /// <summary>Folder of the section scenes.</summary>
        public const string SectionsFolder = "Assets/_Game/Scenes/Sections";
        /// <summary>Folder of dev/test scenes (never shipped).</summary>
        public const string DevFolder = "Assets/_Game/Scenes/Dev";

        private static readonly string[] s_names =
        {
            "S0_Booth", "S1_Approach", "S2_Market", "S3_Square", "S4_Roofs", "S5_StationExt", "S6_StationInt", "S7_Pitch",
            "S8_Heli", "S9_TownFar",
        };

        /// <summary>Number of sections.</summary>
        public const int Count = 10;

        /// <summary>Scene name of a section ("S2_Market").</summary>
        public static string SceneName(SectionId id)
        {
            var i = (int)id;
            return i >= 0 && i < s_names.Length ? s_names[i] : null;
        }

        /// <summary>Project path of a section scene.</summary>
        public static string ScenePath(SectionId id) => SectionsFolder + "/" + SceneName(id) + ".unity";

        /// <summary>Addressables key of a section scene (= its scene name).</summary>
        public static string AddressKey(SectionId id) => SceneName(id);

        /// <summary>Parses "S2_Market", "S2" or "Market" (case-insensitive) into a section id.</summary>
        public static bool TryParse(string text, out SectionId id)
        {
            id = SectionId.Booth;
            if (string.IsNullOrEmpty(text)) return false;
            for (var i = 0; i < s_names.Length; i++)
            {
                var n = s_names[i];
                var shortName = n.Substring(0, n.IndexOf('_'));
                var longName = n.Substring(n.IndexOf('_') + 1);
                if (string.Equals(text, n, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, shortName, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, longName, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, ((SectionId)i).ToString(), System.StringComparison.OrdinalIgnoreCase))
                {
                    id = (SectionId)i;
                    return true;
                }
            }
            return false;
        }
    }
}
