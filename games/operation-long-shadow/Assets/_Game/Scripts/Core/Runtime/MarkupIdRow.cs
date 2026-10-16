using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>One row of <c>Assets/_Game/Data/markup_ids.csv</c> (TECH_PLAN Appendix A).</summary>
    public struct MarkupIdRow
    {
        /// <summary>Marker id ("B3.NodeSet.M2").</summary>
        public string Id;
        /// <summary>Appendix A kind code: M, T, N, C, R, CP, P, I, G, D, K, COVER, SPLINE, POLY.</summary>
        public string Kind;
        /// <summary>Core component to place (SceneMarker, TriggerVolume, SquadNodeSet, ...).</summary>
        public string Component;
        /// <summary>Beat label ("B3", "Global", "Booth", "Heli", "OOB", "Gates").</summary>
        public string Beat;
        /// <summary>True when <see cref="Position"/> is known.</summary>
        public bool HasPosition;
        /// <summary>Centre/anchor in world metres (booth and heli ids: local frame).</summary>
        public Vector3 Position;
        /// <summary>Shape text: "box sx sy sz", "sphere r", "segment" or empty.</summary>
        public string Extents;
        /// <summary>True when <see cref="Heading"/> is known.</summary>
        public bool HasHeading;
        /// <summary>Compass heading in degrees.</summary>
        public float Heading;
        /// <summary>Extra points (node-set members, route/spline knots), in order.</summary>
        public List<Vector3> Points;
        /// <summary>Labels of <see cref="Points"/> (HOL/SPA/TIN for node sets), same length or empty.</summary>
        public List<string> PointLabels;
        /// <summary>Free text from the appendix.</summary>
        public string Notes;

        /// <summary>Parsed box size from <see cref="Extents"/> ("box sx sy sz"), if any.</summary>
        public bool TryGetBox(out Vector3 size)
        {
            size = Vector3.zero;
            if (string.IsNullOrEmpty(Extents) || !Extents.StartsWith("box ")) return false;
            var p = Extents.Substring(4).Split(' ');
            if (p.Length != 3) return false;
            return MarkupIdTable.TryFloat(p[0], out size.x) & MarkupIdTable.TryFloat(p[1], out size.y) & MarkupIdTable.TryFloat(p[2], out size.z);
        }

        /// <summary>Parsed sphere radius from <see cref="Extents"/> ("sphere r"), if any.</summary>
        public bool TryGetRadius(out float radius)
        {
            radius = 0f;
            if (string.IsNullOrEmpty(Extents) || !Extents.StartsWith("sphere ")) return false;
            return MarkupIdTable.TryFloat(Extents.Substring(7), out radius);
        }
    }
}
