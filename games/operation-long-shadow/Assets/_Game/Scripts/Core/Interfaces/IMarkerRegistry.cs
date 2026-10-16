using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Lookup of every enabled markup component by id (Core <see cref="MarkerRegistry"/>; TECH_PLAN D13, Appendix A).
    /// Markers register on enable and unregister on disable, so ids of unloaded sections are absent.
    /// </summary>
    public interface IMarkerRegistry
    {
        /// <summary>Transform of a marker, or null (logs once when missing).</summary>
        Transform Get(string id);
        /// <summary>Finds a marker by id.</summary>
        bool TryGet(string id, out SceneMarker marker);
        /// <summary>Finds a marker of a type by id.</summary>
        bool TryGet<T>(string id, out T marker) where T : SceneMarker;
        /// <summary>True if a marker with this id is registered.</summary>
        bool Has(string id);
        /// <summary>Every registered marker of a type (allocates an enumerator; use <see cref="GetAll{T}"/> in hot paths).</summary>
        IEnumerable<T> All<T>() where T : SceneMarker;
        /// <summary>Fills <paramref name="results"/> (cleared first) with every registered marker of a type; returns the count.</summary>
        int GetAll<T>(List<T> results) where T : SceneMarker;
    }
}
