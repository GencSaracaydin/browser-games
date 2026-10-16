using UnityEngine;

namespace LongShadow.Player.Movement
{
    /// <summary>One frame of input for <see cref="PlayerMotor.Tick"/> (sampled by PlayerRig from <c>IInputReader</c>).</summary>
    public struct MotorInput
    {
        /// <summary>WASD, length &lt;= 1 (x = strafe, y = forward).</summary>
        public Vector2 Move;
        /// <summary>Sprint held.</summary>
        public bool Sprint;
        /// <summary>Jump/mantle pressed this frame.</summary>
        public bool JumpDown;
        /// <summary>Crouch/slide pressed this frame.</summary>
        public bool CrouchDown;
        /// <summary>Compass heading of the body in degrees (0 = +Z); the frame WASD moves in.</summary>
        public float Heading;
        /// <summary>View pitch in degrees (positive up); ladders use it.</summary>
        public float Pitch;
        /// <summary>Aiming (weapon ADS or binoculars raised): ends sprint.</summary>
        public bool Ads;
        /// <summary>ADS blend 0..1 (blends walk 4.4 → ADS walk 2.6).</summary>
        public float AdsBlend;
    }
}
