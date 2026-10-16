using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level
{
    /// <summary>
    /// Root of the generated content of a section scene (TECH_PLAN §7.15): names the section, records the build stamp and
    /// generator version, and lets tools find the generated hierarchy. The section builders own everything under it and
    /// rebuild it from code; anything else in the scene (WP09 cameras, WP10 Look's bake rig) is left alone.
    /// </summary>
    public sealed class SectionRoot : MonoBehaviour
    {
        /// <summary>Name of the generated root object in every section scene.</summary>
        public const string GeneratedRootName = "LEVEL_Generated";

        [SerializeField] private SectionId _section;
        [SerializeField, Tooltip("Generator version (bumped when the builders change their output).")]
        private int _generatorVersion;
        [SerializeField, Tooltip("UTC time of the last build (informational).")]
        private string _builtAt;
        [SerializeField, TextArea(2, 8)] private string _buildReport;

        /// <summary>Section.</summary>
        public SectionId Section => _section;
        /// <summary>Generator version.</summary>
        public int GeneratorVersion => _generatorVersion;
        /// <summary>Build stamp.</summary>
        public string BuiltAt => _builtAt;
        /// <summary>Short build report (counts, warnings).</summary>
        public string BuildReport => _buildReport;

        /// <summary>Stamps the root (editor builders).</summary>
        public void Stamp(SectionId section, int version, string builtAt, string report)
        {
            _section = section;
            _generatorVersion = version;
            _builtAt = builtAt;
            _buildReport = report;
        }
    }
}
