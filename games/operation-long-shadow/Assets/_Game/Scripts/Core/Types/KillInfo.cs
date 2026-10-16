namespace LongShadow.Core
{
    /// <summary>Raised through <see cref="GameEvents.ActorKilled"/> when a <see cref="IDamageable"/> dies.</summary>
    public struct KillInfo
    {
        /// <summary>Actor id of the victim (encounter enemy id such as "E3-02", squad id, or "PLAYER").</summary>
        public string VictimId;
        /// <summary>Team of the victim.</summary>
        public Team VictimTeam;
        /// <summary>The killing blow.</summary>
        public DamageInfo Damage;
        /// <summary>True if the killing blow hit the head.</summary>
        public bool Headshot;
        /// <summary>True if the player dealt the killing blow (stats, hit markers, kill share).</summary>
        public bool ByPlayer;
    }
}
