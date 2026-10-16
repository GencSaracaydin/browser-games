using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// Fuse of a frag held in the hand (GDD §13.2: "4.0 s fuse from the pin pull. Hold G to cook; fuse ticks show on the
    /// HUD"). Mutable struct: keep it in a field.
    /// </summary>
    public struct GrenadeCook
    {
        /// <summary>True while a live grenade is in the hand.</summary>
        public bool Live;
        /// <summary>Seconds since the pin pull.</summary>
        public float Elapsed;
        /// <summary>Total fuse.</summary>
        public float Fuse;

        /// <summary>Seconds left on the fuse.</summary>
        public float Remaining => Live ? Mathf.Max(0f, Fuse - Elapsed) : 0f;
        /// <summary>Cook progress 0..1 for the HUD.</summary>
        public float Cook01 => Live && Fuse > 0f ? Mathf.Clamp01(Elapsed / Fuse) : 0f;
        /// <summary>Whole seconds elapsed (fuse ticks).</summary>
        public int WholeSeconds => Mathf.FloorToInt(Elapsed);

        /// <summary>Pulls the pin.</summary>
        public void Start(float fuse)
        {
            Live = true;
            Elapsed = 0f;
            Fuse = fuse;
        }

        /// <summary>Advances the fuse; returns true on the frame it runs out while still in the hand.</summary>
        public bool Tick(float dt)
        {
            if (!Live) return false;
            Elapsed += dt;
            if (Elapsed < Fuse) return false;
            Live = false;
            return true;
        }

        /// <summary>The grenade left the hand; returns the fuse left for the projectile.</summary>
        public float Release()
        {
            var left = Remaining;
            Live = false;
            return left;
        }
    }
}
