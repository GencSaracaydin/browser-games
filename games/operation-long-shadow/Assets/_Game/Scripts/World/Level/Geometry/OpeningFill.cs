namespace LongShadow.World.Level.Geometry
{
    /// <summary>What the kit puts into a wall opening.</summary>
    public enum OpeningFill
    {
        /// <summary>Nothing (passages, arcade arches, breach holes).</summary>
        None = 0,
        /// <summary>Casement window with timber shutters (closed, ajar or open by seed).</summary>
        ShutteredWindow = 1,
        /// <summary>Window with an iron grille (ground floors).</summary>
        GrilledWindow = 2,
        /// <summary>Small high window or vent (back facades).</summary>
        Vent = 3,
        /// <summary>Planked timber door (closed).</summary>
        Door = 4,
        /// <summary>Steel roller shutter (shops, shut for the curfew, GDD §2.2).</summary>
        RollerShutter = 5,
        /// <summary>Glazed balcony door with shutters.</summary>
        BalconyDoor = 6,
        /// <summary>Steel-framed industrial window (Relay Hall, gatehouse).</summary>
        SteelWindow = 7,
        /// <summary>Double steel door (Relay Hall).</summary>
        SteelDoor = 8,
        /// <summary>Glazed shopfront with a timber frame.</summary>
        Shopfront = 9,
    }
}
