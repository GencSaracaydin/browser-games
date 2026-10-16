namespace LongShadow.World.Level.Data
{
    /// <summary>Per-member facings (compass degrees) of a squad node set and whether the squad crouches there.</summary>
    public sealed class NodeSetPose
    {
        /// <summary>Hollis facing.</summary>
        public float Hollis;
        /// <summary>Sparrow facing.</summary>
        public float Sparrow;
        /// <summary>Tinker facing.</summary>
        public float Tinker;
        /// <summary>Crouched (cover sets).</summary>
        public bool Crouch;
    }
}
