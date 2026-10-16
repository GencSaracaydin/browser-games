using System;
using UnityEngine;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// Recoil of a weapon (GDD §13.1 "Recoil per shot", "Recoil pattern", "Recovery", "Visual kick"). Angles are degrees of
    /// aim movement per shot while aiming down sights: x = pitch up, y = yaw right. The first <see cref="Pattern"/> shots of a
    /// burst are fixed (no randomness, so players can learn them); after that every shot is
    /// <see cref="SustainPitch"/> ± <see cref="PitchRandom"/> up and <see cref="YawBias"/> ± <see cref="YawRandom"/> right.
    /// Treat instances as immutable (shared by <see cref="WeaponTable"/>).
    /// </summary>
    [Serializable]
    public sealed class RecoilSpec
    {
        /// <summary>Kick of the first shot of a burst when the weapon has no fixed pattern.</summary>
        public float FirstShotPitch;
        /// <summary>Nominal per-shot vertical kick (GDD value).</summary>
        public float Pitch;
        /// <summary>Per-shot vertical kick once the fixed pattern is exhausted (RPK settles to 0.45°).</summary>
        public float SustainPitch;
        /// <summary>Random vertical range (±) after the pattern (tuning; GDD gives none for most weapons).</summary>
        public float PitchRandom;
        /// <summary>Mean horizontal drift per shot after the pattern (Commando drifts right +0.12°).</summary>
        public float YawBias;
        /// <summary>Random horizontal range (±) after the pattern.</summary>
        public float YawRandom;
        /// <summary>Fixed kicks for the first shots of a burst (x pitch up, y yaw right), element 0 = first shot.</summary>
        public Vector2[] Pattern = Array.Empty<Vector2>();
        /// <summary>Seconds after the last shot before the aim starts to recover.</summary>
        public float RecoveryDelay = 0.08f;
        /// <summary>Recovery speed in degrees per second.</summary>
        public float RecoveryRate = 8f;
        /// <summary>Share of each kick added as a visual-only view punch (GDD §13.1: "the camera takes 30 % of the aim kick").</summary>
        public float CameraShare = 0.3f;
        /// <summary>Model kick back along -Z in metres (GDD §13.1: 2.2 cm, recovers with a spring).</summary>
        public float ModelKickBack = 0.022f;
        /// <summary>Visual model rotation kick in degrees per shot (pitch up), on top of the aim kick (tuning).</summary>
        public float ModelKickPitch = 1.6f;
        /// <summary>Visual model roll kick range (±) in degrees (tuning).</summary>
        public float ModelKickRoll = 1.2f;
        /// <summary>Recoil scale while firing from the hip (tuning; GDD values are ADS).</summary>
        public float HipScale = 1f;
        /// <summary>Seconds without a shot after which the next shot starts a new burst (pattern index 0).</summary>
        public float BurstResetTime = 0.25f;

        /// <summary>Number of fixed pattern shots.</summary>
        public int PatternLength => Pattern?.Length ?? 0;
    }
}
