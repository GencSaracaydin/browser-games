using LongShadow.Core;

namespace LongShadow.Audio.Foley
{
    /// <summary>
    /// Footstep surface rules for characters (GDD §10.2 "Footsteps by surface"; WP02 does the player): a tagged or
    /// named surface wins; an untagged one falls back to the acoustic zone's typical floor. Pure (unit-tested).
    /// </summary>
    public static class FootstepSurfaces
    {
        /// <summary>Typical floor of an acoustic zone.</summary>
        public static SurfaceType ZoneDefault(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Street:
                case ZoneType.Square: return SurfaceType.Stone;
                case ZoneType.Roof: return SurfaceType.Plaster;
                case ZoneType.Interior: return SurfaceType.Tile;
                case ZoneType.Basement: return SurfaceType.Concrete;
                case ZoneType.Cabin: return SurfaceType.Metal;
                default: return SurfaceType.Sand; // OpenDesert
            }
        }

        /// <summary>The surface to use: <paramref name="hit"/> unless it is Default (or flesh/fabric from a body), else the zone floor.</summary>
        public static SurfaceType Choose(SurfaceType hit, ZoneType zone)
        {
            if (hit == SurfaceType.Default || hit == SurfaceType.Flesh) return ZoneDefault(zone);
            return hit;
        }

        /// <summary>
        /// Footstep volume by speed (WP06_SFX notes: walk at 1.0 of the cue, sprint +2 dB): <paramref name="walkVolume"/>
        /// at or below <paramref name="walkSpeed"/>, rising linearly to <paramref name="runVolume"/> at <paramref name="runSpeed"/>.
        /// </summary>
        public static float Volume(float speed, float walkSpeed, float runSpeed, float walkVolume, float runVolume)
        {
            if (runSpeed <= walkSpeed) return speed >= runSpeed ? runVolume : walkVolume;
            var t = (speed - walkSpeed) / (runSpeed - walkSpeed);
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return walkVolume + (runVolume - walkVolume) * t;
        }
    }
}
