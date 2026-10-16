using LongShadow.Core;

namespace LongShadow.Mission.Data
{
    /// <summary>A section load requested at a named quiet point (GDD §6.7: "Beat 2, on D019").</summary>
    public readonly struct QuietLoad
    {
        /// <summary>Line id or mission event id at which the load is requested.</summary>
        public readonly string At;
        /// <summary>Section to load.</summary>
        public readonly SectionId Section;

        /// <summary>Creates a quiet-point load.</summary>
        public QuietLoad(string at, SectionId section)
        {
            At = at;
            Section = section;
        }
    }
}
