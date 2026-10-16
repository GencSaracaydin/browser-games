namespace LongShadow.Core
{
    /// <summary>Body zone hit by a shot; selects the damage multiplier (GDD §13.1).</summary>
    public enum HitZone
    {
        /// <summary>Chest, belly, pelvis (multiplier 1, armour may apply).</summary>
        Torso = 0,
        /// <summary>Head and neck.</summary>
        Head = 1,
        /// <summary>Arms and legs.</summary>
        Limb = 2,
    }
}
