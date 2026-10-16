using UnityEngine;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Fallback geometry of a TriggerVolume from TECH_PLAN Appendix A, used by the MissionDirector only while the scene has
    /// no TriggerVolume with that id (greybox gaps, dev scenes), so a beat never soft-locks on missing markup. The
    /// EditMode tests compare every shape with <c>markup_ids.csv</c>.
    /// </summary>
    public readonly struct TriggerShape
    {
        /// <summary>Shape kind.</summary>
        public readonly TriggerShapeKind Kind;
        /// <summary>Box centre, sphere centre, or capsule midpoint.</summary>
        public readonly Vector3 Center;
        /// <summary>Full box size (boxes only).</summary>
        public readonly Vector3 Size;
        /// <summary>Sphere / capsule radius.</summary>
        public readonly float Radius;
        /// <summary>Capsule segment start.</summary>
        public readonly Vector3 A;
        /// <summary>Capsule segment end.</summary>
        public readonly Vector3 B;

        private TriggerShape(TriggerShapeKind kind, Vector3 center, Vector3 size, float radius, Vector3 a, Vector3 b)
        {
            Kind = kind;
            Center = center;
            Size = size;
            Radius = radius;
            A = a;
            B = b;
        }

        /// <summary>A box from min/max world coordinates.</summary>
        public static TriggerShape Box(float x0, float x1, float y0, float y1, float z0, float z1) =>
            new TriggerShape(TriggerShapeKind.Box, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)), 0f, Vector3.zero, Vector3.zero);

        /// <summary>A sphere.</summary>
        public static TriggerShape Sphere(float x, float y, float z, float r) =>
            new TriggerShape(TriggerShapeKind.Sphere, new Vector3(x, y, z), Vector3.zero, r, Vector3.zero, Vector3.zero);

        /// <summary>A capsule (swept box) along a segment.</summary>
        public static TriggerShape Capsule(Vector3 a, Vector3 b, float r) =>
            new TriggerShape(TriggerShapeKind.Capsule, (a + b) * 0.5f, Vector3.zero, r, a, b);

        /// <summary>True if the point is inside.</summary>
        public bool Contains(Vector3 p)
        {
            switch (Kind)
            {
                case TriggerShapeKind.Box:
                    var d = p - Center;
                    return Mathf.Abs(d.x) <= Size.x * 0.5f && Mathf.Abs(d.y) <= Size.y * 0.5f && Mathf.Abs(d.z) <= Size.z * 0.5f;
                case TriggerShapeKind.Sphere:
                    return (p - Center).sqrMagnitude <= Radius * Radius;
                default:
                    var ab = B - A;
                    var len2 = ab.sqrMagnitude;
                    var t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - A, ab) / len2) : 0f;
                    var closest = A + ab * t;
                    return (p - closest).sqrMagnitude <= Radius * Radius;
            }
        }

        /// <summary>A point inside the shape (the autopilot and the smoke test teleport there).</summary>
        public Vector3 Inside => Center;
    }
}
