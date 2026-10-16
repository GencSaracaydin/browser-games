namespace LongShadow.Core
{
    /// <summary>
    /// Physics layers (TECH_PLAN §3.9) as set by ProjectSetup, and the query masks. Use these constants, never
    /// LayerMask.NameToLayer in gameplay code.
    /// </summary>
    public static class LayerIds
    {
        public const int Default = 0;
        public const int Player = 6;
        public const int Viewmodel = 7;
        public const int Hitbox = 8;
        public const int SquadHitbox = 9;
        public const int Character = 10;
        public const int Ragdoll = 11;
        public const int Projectile = 12;
        public const int Debris = 13;
        public const int Interactable = 14;
        public const int Trigger = 15;
        public const int SmokeLOS = 16;
        public const int FarProxy = 17;
        public const int Booth = 18;
        public const int PlayerClip = 19;
        public const int Vehicle = 20;

        /// <summary>Layer names by index (ProjectSetup writes these).</summary>
        public static readonly string[] Names =
        {
            "Default", "TransparentFX", "Ignore Raycast", "", "Water", "UI", "Player", "Viewmodel", "Hitbox", "SquadHitbox",
            "Character", "Ragdoll", "Projectile", "Debris", "Interactable", "Trigger", "SmokeLOS", "FarProxy", "Booth",
            "PlayerClip", "Vehicle",
        };

        /// <summary>Bit for a layer.</summary>
        public static int Bit(int layer) => 1 << layer;

        /// <summary>Player hitscan: Default | Hitbox | Vehicle | Debris.</summary>
        public const int PlayerShot = (1 << Default) | (1 << Hitbox) | (1 << Vehicle) | (1 << Debris);
        /// <summary>Enemy shot placement: Default | SquadHitbox | Vehicle | Debris (hits on the player are rolled, not raycast).</summary>
        public const int EnemyShot = (1 << Default) | (1 << SquadHitbox) | (1 << Vehicle) | (1 << Debris);
        /// <summary>AI line of sight: Default | Vehicle | SmokeLOS.</summary>
        public const int AISight = (1 << Default) | (1 << Vehicle) | (1 << SmokeLOS);
        /// <summary>Interaction raycasts: Interactable | Default.</summary>
        public const int Interact = (1 << Interactable) | (1 << Default);
        /// <summary>Ground probes: Default | Vehicle.</summary>
        public const int Ground = (1 << Default) | (1 << Vehicle);
        /// <summary>Explosion cover raycast (same blockers as sight minus smoke).</summary>
        public const int ExplosionCover = (1 << Default) | (1 << Vehicle);
    }
}
