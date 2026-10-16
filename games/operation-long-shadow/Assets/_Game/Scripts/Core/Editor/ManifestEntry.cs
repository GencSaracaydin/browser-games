using System;

namespace LongShadow.Core.Editor
{
    /// <summary>One entry of <c>Docs/ASSET_MANIFEST.json</c> (fields used by builders; see TECH_PLAN §8 for the schema).</summary>
    [Serializable]
    public class ManifestEntry
    {
        /// <summary>Manifest id ("WPN_COMMANDO").</summary>
        public string id;
        /// <summary>weapon | prop | character | texture | hdri | sfx | vo | music | image | ui | decal.</summary>
        public string category;
        /// <summary>fal, polyhaven, ambientcg, procedural, local, github, google_fonts.</summary>
        public string source;
        /// <summary>Target folder (ends with '/') or file; VO paths contain "{CHAIN}".</summary>
        public string target_path;
        /// <summary>Variants / processing chains.</summary>
        public string[] variants;
        /// <summary>Budget text.</summary>
        public string budget;
        /// <summary>P0, P1, P2.</summary>
        public string priority;
        /// <summary>Pipeline notes.</summary>
        public string notes;

        /// <summary>True if <see cref="target_path"/> names a folder.</summary>
        public bool IsFolder => !string.IsNullOrEmpty(target_path) && target_path.EndsWith("/");
    }
}
