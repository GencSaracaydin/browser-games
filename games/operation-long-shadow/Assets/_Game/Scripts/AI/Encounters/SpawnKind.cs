namespace LongShadow.AI.Encounters
{
    /// <summary>How an encounter enemy enters the world (GDD §7 conventions).</summary>
    public enum SpawnKind
    {
        /// <summary>Exists when the encounter activates, at its table position (or its Appendix A marker).</summary>
        Placed,
        /// <summary>Spawns at a named <c>SpawnCloset</c> out of the player's sight and ≥ 15 m away.</summary>
        Closet,
        /// <summary>Placed on a perch node and never roams (gunners, rooftop shooters, the marksman).</summary>
        Perch,
        /// <summary>Rides a World <c>ISplineVehicle</c> and dismounts at the tailgate (or stays on the bed).</summary>
        Vehicle,
    }
}
