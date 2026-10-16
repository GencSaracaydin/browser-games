using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Objectives
{
    /// <summary>
    /// Beat 1 binocular glassing (TECH_PLAN §7.11 GlassingTracker, GDD §7.1): a POI ticks when the binocular view keeps it
    /// within its radius (4°) of the view centre for its dwell time (1.0 s). Two sources, first one wins: WP02's
    /// Binoculars raise <c>Glassed.&lt;id&gt;</c> mission events; this tracker also measures it itself from
    /// <see cref="IPlayerRig.IsGlassing"/> and <see cref="IPlayerRig.ViewRay"/> against the <see cref="GlassingPOI"/>
    /// markers (Appendix A fallback positions while the far section is missing).
    /// </summary>
    public sealed class GlassingTracker
    {
        /// <summary>Default angular radius (GDD §7.1).</summary>
        public const float RadiusDegrees = 4f;
        /// <summary>Default dwell (GDD §7.1).</summary>
        public const float DwellSeconds = 1f;

        private readonly string[] _ids;
        private readonly float[] _dwell;
        private readonly HashSet<string> _glassed = new HashSet<string>(StringComparer.Ordinal);
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<IMarkerRegistry> _markers;

        /// <summary>Creates a tracker for the POI ids (Beat 1: B1.POI1..3).</summary>
        public GlassingTracker(params string[] poiIds)
        {
            _ids = poiIds ?? Array.Empty<string>();
            _dwell = new float[_ids.Length];
        }

        /// <summary>Raised once per POI when it is glassed.</summary>
        public event Action<string> Glassed;

        /// <summary>POIs glassed so far.</summary>
        public int Count => _glassed.Count;
        /// <summary>Number of POIs.</summary>
        public int Total => _ids.Length;
        /// <summary>True once the POI is glassed.</summary>
        public bool IsGlassed(string id) => id != null && _glassed.Contains(id);

        /// <summary>Forgets every POI (checkpoint reload).</summary>
        public void Reset()
        {
            _glassed.Clear();
            Array.Clear(_dwell, 0, _dwell.Length);
        }

        /// <summary>Marks a POI glassed (silent ticks after the 40 s timeout, or the WP02 event).</summary>
        public bool Mark(string id)
        {
            if (Array.IndexOf(_ids, id) < 0 || !_glassed.Add(id)) return false;
            Glassed?.Invoke(id);
            return true;
        }

        /// <summary>Updates the dwell timers; <paramref name="log"/> supplies the WP02 Glassed.* events.</summary>
        public void Tick(float deltaTime, MissionEventLog log)
        {
            for (var i = 0; i < _ids.Length; i++)
            {
                var id = _ids[i];
                if (_glassed.Contains(id)) continue;
                if (log != null && log.Happened(EventChannel.Mission, MissionEvents.Glassed(id)))
                {
                    Mark(id);
                    continue;
                }
                var rig = _rig.Value;
                if (rig == null || !rig.IsGlassing || !TryPoi(id, out var pos, out var radius, out var dwell))
                {
                    _dwell[i] = 0f;
                    continue;
                }
                var ray = rig.ViewRay;
                var inView = Vector3.Angle(ray.direction, pos - ray.origin) <= radius;
                _dwell[i] = inView ? _dwell[i] + deltaTime : 0f;
                if (_dwell[i] >= dwell) Mark(id);
            }
        }

        private bool TryPoi(string id, out Vector3 position, out float radius, out float dwell)
        {
            radius = RadiusDegrees;
            dwell = DwellSeconds;
            var reg = _markers.Value;
            if (reg != null && reg.TryGet(id, out GlassingPOI poi) && poi != null)
            {
                position = poi.Position;
                radius = poi.RadiusDegrees;
                dwell = poi.DwellSeconds;
                return true;
            }
            return MissionMarkers.TryGetFallback(id, out position);
        }
    }
}
