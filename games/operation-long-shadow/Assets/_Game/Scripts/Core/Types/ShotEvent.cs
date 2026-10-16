using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>A weapon discharge (player or AI). Raised through <see cref="GameEvents.ShotFired"/>.</summary>
    public struct ShotEvent
    {
        /// <summary>Muzzle position.</summary>
        public Vector3 Origin;
        /// <summary>Normalised shot direction.</summary>
        public Vector3 Direction;
        /// <summary>Shooter team.</summary>
        public Team Team;
        /// <summary>WeaponTable id.</summary>
        public string WeaponId;
        /// <summary>Hearing radius in metres for AI perception (GDD §14.2).</summary>
        public float NoiseRadius;
        /// <summary>True when the shot hit a damageable.</summary>
        public bool Hit;
        /// <summary>True when fired by the player.</summary>
        public bool IsPlayer;
    }
}
