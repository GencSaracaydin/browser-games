using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Aim recovery after recoil (GDD §13.1 "Recovery 8°/s after 0.08 s"). Tracks the recoil "debt" (how far the shots moved
    /// the aim and the player has not compensated yet) and, once <c>delay</c> has passed since the last shot, returns it at
    /// <c>rate</c> degrees per second. Pulling the mouse against the recoil pays the debt down, so a player who compensates
    /// is never dragged below his target. Mutable struct: keep it in a field.
    /// </summary>
    public struct RecoilRecovery
    {
        /// <summary>Outstanding pitch (degrees up).</summary>
        public float DebtPitch;
        /// <summary>Outstanding yaw (degrees right).</summary>
        public float DebtYaw;
        /// <summary>Seconds since the last kick.</summary>
        public float SinceKick;

        /// <summary>Records a kick that moved the aim.</summary>
        public void OnKick(float pitchUp, float yawRight)
        {
            DebtPitch += pitchUp;
            DebtYaw += yawRight;
            SinceKick = 0f;
        }

        /// <summary>
        /// Reports the player's own look movement (degrees, pitch up / yaw right, excluding recoil). Movement against the debt
        /// reduces it; movement with it changes nothing.
        /// </summary>
        public void OnPlayerLook(float pitchUp, float yawRight)
        {
            if (DebtPitch > 0f && pitchUp < 0f) DebtPitch = Mathf.Max(0f, DebtPitch + pitchUp);
            else if (DebtPitch < 0f && pitchUp > 0f) DebtPitch = Mathf.Min(0f, DebtPitch + pitchUp);
            if (DebtYaw > 0f && yawRight < 0f) DebtYaw = Mathf.Max(0f, DebtYaw + yawRight);
            else if (DebtYaw < 0f && yawRight > 0f) DebtYaw = Mathf.Min(0f, DebtYaw + yawRight);
        }

        /// <summary>
        /// Advances time and returns the aim correction to apply now (degrees; x pitch, y yaw; opposite to the debt).
        /// Pitch and yaw recover together along the debt vector at <paramref name="rate"/> degrees per second.
        /// </summary>
        public Vector2 Step(float dt, float delay, float rate)
        {
            SinceKick += dt;
            if (SinceKick < delay || rate <= 0f) return Vector2.zero;
            var debt = new Vector2(DebtPitch, DebtYaw);
            var mag = debt.magnitude;
            if (mag <= 1e-5f)
            {
                DebtPitch = DebtYaw = 0f;
                return Vector2.zero;
            }
            // Only the part of this frame after the delay counts on the first recovering frame.
            var effective = Mathf.Min(dt, SinceKick - delay);
            var step = Mathf.Min(mag, rate * effective);
            var correction = -debt / mag * step;
            DebtPitch += correction.x;
            DebtYaw += correction.y;
            return correction;
        }

        /// <summary>Forgets the debt (weapon switch, mount, checkpoint).</summary>
        public void Reset()
        {
            DebtPitch = DebtYaw = 0f;
            SinceKick = 0f;
        }
    }
}
