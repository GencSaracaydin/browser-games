using LongShadow.Core;

namespace LongShadow.Player.Footsteps
{
    /// <summary>
    /// Footstep loudness and AI noise radii (GDD §14.2: sprint footsteps 12 m, walk 4 m, crouch 1.5 m), and the default
    /// surface of an acoustic zone when the ground has no tag.
    /// </summary>
    public static class FootstepMath
    {
        /// <summary>Sprint footsteps play 5 % higher (WP06_SFX footstep notes; IAudioService.PlayPitched).</summary>
        public const float SprintPitch = 1.05f;
        /// <summary>GDD §14.2: sprint footsteps heard at 12 m.</summary>
        public const float SprintNoiseRadius = 12f;
        /// <summary>GDD §14.2: walking footsteps heard at 4 m.</summary>
        public const float WalkNoiseRadius = 4f;
        /// <summary>GDD §14.2: crouched footsteps heard at 1.5 m.</summary>
        public const float CrouchNoiseRadius = 1.5f;

        /// <summary>AI hearing radius of one footstep for a stance.</summary>
        public static float NoiseRadius(Stance stance)
        {
            switch (stance)
            {
                case Stance.Sprint:
                case Stance.Slide:
                    return SprintNoiseRadius;
                case Stance.Crouch:
                    return CrouchNoiseRadius;
                default:
                    return WalkNoiseRadius;
            }
        }

        /// <summary>Footstep volume 0..1 for a stance (ADS walking is quieter).</summary>
        public static float Volume(Stance stance, float adsBlend)
        {
            switch (stance)
            {
                case Stance.Sprint:
                    return 1f;
                case Stance.Crouch:
                    return 0.3f;
                default:
                    return 0.65f - 0.15f * (adsBlend < 0f ? 0f : adsBlend > 1f ? 1f : adsBlend);
            }
        }

        /// <summary>Surface used when the ground has no SurfaceTag/physic material, from the acoustic zone.</summary>
        public static SurfaceType ZoneDefaultSurface(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Street:
                case ZoneType.Square: return SurfaceType.Stone;
                case ZoneType.Interior: return SurfaceType.Tile;
                case ZoneType.Basement: return SurfaceType.Concrete;
                case ZoneType.Roof: return SurfaceType.Plaster;
                case ZoneType.Cabin: return SurfaceType.Metal;
                default: return SurfaceType.Sand;
            }
        }
    }
}
