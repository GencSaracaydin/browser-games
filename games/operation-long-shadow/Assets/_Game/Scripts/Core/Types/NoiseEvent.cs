using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>A sound AI can hear (GDD §14.2). Radius is scaled by <see cref="ILightStage.HearingScale"/> on the AI side.</summary>
    public struct NoiseEvent
    {
        /// <summary>Source position.</summary>
        public Vector3 Position;
        /// <summary>Audible radius in metres (sprint 12, walk 4, crouch 1.5, gunshot per weapon, grenade 80).</summary>
        public float Radius;
        /// <summary>Team that made the noise.</summary>
        public Team Source;
        /// <summary>Kind of noise.</summary>
        public NoiseKind Kind;

        /// <summary>Creates a noise event.</summary>
        public NoiseEvent(Vector3 position, float radius, Team source, NoiseKind kind)
        {
            Position = position;
            Radius = radius;
            Source = source;
            Kind = kind;
        }
    }
}
