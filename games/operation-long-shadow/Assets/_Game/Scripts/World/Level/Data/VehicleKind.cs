namespace LongShadow.World.Level.Data
{
    /// <summary>What rides a <see cref="VehicleRouteDef"/> (selects the model, seats and motion style).</summary>
    public enum VehicleKind
    {
        /// <summary>Legion cargo truck (VEH_CARGO_TRUCK): ground vehicle, yaw only.</summary>
        CargoTruck = 0,
        /// <summary>Magpie (VEH_HELICOPTER): flies, banks into turns, flares on landing.</summary>
        Helicopter = 1,
        /// <summary>A walking route with no vehicle (squad insertion path).</summary>
        PathOnly = 2,
    }
}
