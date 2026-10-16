using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>A bullet passed close to the player (within 3 m, GDD §14.3): crack/whizz audio and suppression FX.</summary>
    public struct NearMissEvent
    {
        /// <summary>Closest point of the bullet path to the player's head.</summary>
        public Vector3 Point;
        /// <summary>Normalised bullet direction.</summary>
        public Vector3 Direction;
        /// <summary>Closest distance to the player's head in metres.</summary>
        public float Distance;
        /// <summary>Team of the shooter.</summary>
        public Team ShooterTeam;
        /// <summary>Weapon id (selects the crack sound), may be null.</summary>
        public string WeaponId;
    }
}
