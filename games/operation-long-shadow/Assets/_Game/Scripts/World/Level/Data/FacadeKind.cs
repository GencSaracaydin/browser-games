namespace LongShadow.World.Level.Data
{
    /// <summary>What a facade of a generated building shows (TECH_PLAN §7.15 BuildingGenerator facade programme).</summary>
    public enum FacadeKind
    {
        /// <summary>Party wall shared with a neighbour: only the part above the neighbour's height is built.</summary>
        Party = 0,
        /// <summary>Blank wall with the odd vent.</summary>
        Blank = 1,
        /// <summary>House front: door and grilled windows below, shuttered windows above.</summary>
        Residential = 2,
        /// <summary>Shops: roller shutters and signboards below, windows above.</summary>
        Shops = 3,
        /// <summary>Ground-floor arcade of round arches (market, square) with shopfronts behind, windows above.</summary>
        Arcade = 4,
        /// <summary>Back of a house: a service door, small windows, pipes.</summary>
        Service = 5,
    }
}
