using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>What one <see cref="WeaponRuntime.Tick"/> produced. Call <see cref="WeaponRuntime.TakeShot"/> once per shot.</summary>
    public struct WeaponFrame
    {
        /// <summary>Shots due this frame (0..FireCadence.MaxShotsPerFrame).</summary>
        public int Shots;
        /// <summary>The trigger was pressed on an empty magazine (dry-fire click).</summary>
        public bool DryFire;
        /// <summary>Reload keys crossed this frame (start, mag out, mag in, charge, finish, cancel).</summary>
        public ReloadEvents Reload;
        /// <summary>The aim target changed to aiming this frame.</summary>
        public bool AdsStarted;
        /// <summary>The aim target changed to not aiming this frame.</summary>
        public bool AdsEnded;
        /// <summary>Aim correction from recoil recovery to apply this frame (degrees: x pitch up, y yaw right).</summary>
        public Vector2 Recovery;
        /// <summary>The door gun overheated this frame.</summary>
        public bool Overheated;
        /// <summary>The door gun cooled down this frame.</summary>
        public bool Cooled;
    }
}
