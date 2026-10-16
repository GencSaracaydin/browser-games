using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// Direction of a callout relative to the player's view (GDD §15): the planar angle between the view direction and
    /// the direction to the enemy; front within ±45°, left/right up to ±135°, rear beyond. Pure.
    /// </summary>
    public static class CalloutDirection
    {
        /// <summary>Half-width of the front sector (degrees).</summary>
        public const float FrontHalfAngle = 45f;
        /// <summary>Angle beyond which an enemy is behind (degrees).</summary>
        public const float RearAngle = 135f;
        /// <summary>Height above the player (m) that counts as "Rooftop" when the enemy has no perch tag (DIALOGUE CO_HOL_05).</summary>
        public const float RooftopHeight = 4f;

        /// <summary>Classifies an enemy position against the player's position and view direction.</summary>
        public static RelativeDirection Classify(Vector3 playerPosition, Vector3 viewForward, Vector3 enemyPosition)
        {
            var to = enemyPosition - playerPosition;
            to.y = 0f;
            viewForward.y = 0f;
            if (to.sqrMagnitude < 1e-4f || viewForward.sqrMagnitude < 1e-6f) return RelativeDirection.Front;
            return Classify(MathX.SignedAngleXZ(viewForward, to));
        }

        /// <summary>Classifies a signed planar angle (degrees, positive = clockwise = to the right).</summary>
        public static RelativeDirection Classify(float signedAngle)
        {
            var a = MathX.WrapAngle180(signedAngle);
            var abs = Mathf.Abs(a);
            if (abs <= FrontHalfAngle) return RelativeDirection.Front;
            if (abs > RearAngle) return RelativeDirection.Rear;
            return a > 0f ? RelativeDirection.Right : RelativeDirection.Left;
        }
    }
}
