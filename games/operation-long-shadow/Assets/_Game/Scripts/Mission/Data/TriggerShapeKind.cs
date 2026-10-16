namespace LongShadow.Mission.Data
{
    /// <summary>Shape kind of a <see cref="TriggerShape"/>.</summary>
    public enum TriggerShapeKind
    {
        /// <summary>Axis-aligned box (Appendix A "x a–b, z c–d, y e–f").</summary>
        Box = 0,
        /// <summary>Sphere (Appendix A "centre r n").</summary>
        Sphere = 1,
        /// <summary>A box swept along a segment (Appendix A "4 m-wide box along (a)–(b)"): distance to the segment ≤ radius.</summary>
        Capsule = 2,
    }
}
