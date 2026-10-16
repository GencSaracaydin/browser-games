namespace LongShadow.Combat.Weapons
{
    /// <summary>A weapon leaving the player's hands (pickup swap): its id and the ammunition it carries.</summary>
    public struct DroppedWeapon
    {
        /// <summary>Weapon id, or null when nothing was dropped.</summary>
        public string Id;
        /// <summary>Rounds in its magazine.</summary>
        public int Magazine;
        /// <summary>Rounds in its reserve.</summary>
        public int Reserve;

        /// <summary>True when a weapon was dropped.</summary>
        public bool IsValid => !string.IsNullOrEmpty(Id);
    }
}
