using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>One bullet leaving the barrel, decided by <see cref="WeaponRuntime.TakeShot"/>.</summary>
    public struct ShotSolution
    {
        /// <summary>Index of the shot in the current burst (0 = first; GDD §13.1 fixed patterns use it).</summary>
        public int BurstIndex;
        /// <summary>Aim kick in degrees (x pitch up, y yaw right), already scaled for hip/ADS.</summary>
        public Vector2 Kick;
        /// <summary>Spread cone half-angle at the moment of the shot (degrees).</summary>
        public float SpreadDegrees;
        /// <summary>Uniform randoms for the deviation inside the cone (SpreadModel.Deviate).</summary>
        public Vector2 SpreadRandom;
        /// <summary>This round is a tracer (GDD §13.4).</summary>
        public bool Tracer;
        /// <summary>The magazine is empty after this shot.</summary>
        public bool LastRound;
        /// <summary>This shot overheated the door gun.</summary>
        public bool Overheated;
    }
}
