using System.Collections.Generic;

namespace LongShadow.Core
{
    /// <summary>Objectives, HUD markers and intel counts (WP07 ObjectiveService, GDD §7.12). Raises ObjectivesChanged/MarkersChanged.</summary>
    public interface IObjectiveService
    {
        /// <summary>Adds or replaces an objective.</summary>
        void Set(ObjectiveDef objective);
        /// <summary>Completes an objective (raises ObjectiveCompleted).</summary>
        void Complete(string id);
        /// <summary>True once completed.</summary>
        bool IsComplete(string id);
        /// <summary>Adds or replaces a marker.</summary>
        void AddMarker(MarkerDef marker);
        /// <summary>Removes a marker.</summary>
        void RemoveMarker(string id);
        /// <summary>Removes every objective and marker (checkpoint restore rebuilds them).</summary>
        void Clear();
        /// <summary>Active (not completed) objectives, in display order.</summary>
        IReadOnlyList<ObjectiveDef> Active { get; }
        /// <summary>Current markers.</summary>
        IReadOnlyList<MarkerDef> Markers { get; }
        /// <summary>Main intel collected (of 3).</summary>
        int IntelCount { get; }
        /// <summary>Optional intel collected (of 2).</summary>
        int OptionalCount { get; }
    }
}
