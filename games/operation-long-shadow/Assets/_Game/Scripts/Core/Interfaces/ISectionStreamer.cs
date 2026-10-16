using System.Collections;

namespace LongShadow.Core
{
    /// <summary>Additive section scene streaming (WP01 SectionStreamer, TECH_PLAN §5.1). Raises SectionLoaded/Unloaded.</summary>
    public interface ISectionStreamer
    {
        /// <summary>Requests a load (true) or unload (false). Requests are serialised.</summary>
        void Request(SectionId section, bool load);
        /// <summary>True when the section is loaded and its probes tetrahedralised.</summary>
        bool IsLoaded(SectionId section);
        /// <summary>Progress 0..1 of the current request queue.</summary>
        float Progress { get; }
        /// <summary>Coroutine helper: yields until loaded (requests the load if needed).</summary>
        IEnumerator WaitLoaded(SectionId section);
        /// <summary>True while any load/unload is in flight.</summary>
        bool IsBusy { get; }
    }
}
