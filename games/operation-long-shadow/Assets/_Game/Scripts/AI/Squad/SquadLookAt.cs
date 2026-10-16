using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// A squadmate's head look-at (TECH_PLAN §7.7 SquadLookAt, GDD §15): an override from
    /// <see cref="ISquadService.SetLookTarget"/> (MID-A: the sun) beats everything; otherwise the current speaker in
    /// dialogue, then the player within 3 m, then the threat in combat. Calls the body only when the choice changes.
    /// </summary>
    public sealed class SquadLookAt
    {
        /// <summary>The player counts as "close" within this distance (GDD §15).</summary>
        public const float PlayerNearRadius = 3f;

        private Transform _current;
        private float _weight = -1f;

        /// <summary>Current source (diagnostics, tests).</summary>
        public LookSource Current { get; private set; }

        /// <summary>The priority rule (pure).</summary>
        public static LookSource Choose(bool hasOverride, bool hasSpeaker, bool playerNear, bool hasThreat)
        {
            if (hasOverride) return LookSource.Override;
            if (hasSpeaker) return LookSource.Speaker;
            if (playerNear) return LookSource.Player;
            return hasThreat ? LookSource.Threat : LookSource.None;
        }

        /// <summary>Updates the body's look-at.</summary>
        public void Tick(ICharacterBody body, Transform self, Transform overrideTarget, float overrideWeight, Transform speaker,
                         Transform playerHead, ITargetable threat)
        {
            if (body == null) return;
            var toPlayer = playerHead != null ? playerHead.position - self.position : Vector3.zero;
            toPlayer.y = 0f;
            var playerNear = playerHead != null && toPlayer.sqrMagnitude <= PlayerNearRadius * PlayerNearRadius;
            var source = Choose(overrideTarget != null, speaker != null, playerNear, threat != null && threat.AimPoint != null);
            Transform t;
            float w;
            switch (source)
            {
                case LookSource.Override: t = overrideTarget; w = Mathf.Clamp01(overrideWeight); break;
                case LookSource.Speaker: t = speaker; w = 0.85f; break;
                case LookSource.Player: t = playerHead; w = 0.75f; break;
                case LookSource.Threat: t = threat.AimPoint; w = 1f; break;
                default: t = null; w = 0f; break;
            }
            Current = source;
            if (t == _current && Mathf.Abs(w - _weight) < 0.02f) return;
            _current = t;
            _weight = w;
            body.SetLookAt(t, w);
        }
    }
}
