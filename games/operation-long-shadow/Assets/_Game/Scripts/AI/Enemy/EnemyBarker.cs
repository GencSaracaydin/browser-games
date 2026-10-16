using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// Enemy barks and squelch clicks through <see cref="IVOService"/> (TECH_PLAN §7.6 EnemyBarker, GDD §14.4) with three
    /// cooldowns so a firefight never turns into a chorus: a global one shared by every enemy, one per line id, and one
    /// per speaker. Missing VO service = silent (logged once by GameServices). Statics reset on SubsystemRegistration.
    /// </summary>
    public sealed class EnemyBarker
    {
        /// <summary>Minimum gap between any two enemy barks (s).</summary>
        public const float GlobalCooldown = 0.9f;
        /// <summary>Default gap before the same line id repeats (s).</summary>
        public const float DefaultLineCooldown = 6f;
        /// <summary>Minimum gap between two barks of the same speaker (s).</summary>
        public const float SpeakerCooldown = 2.5f;
        /// <summary>Minimum gap between two click sequences of the same speaker (s).</summary>
        public const float ClickCooldown = 2.5f;

        private static float s_globalNext = float.NegativeInfinity;
        private static readonly Dictionary<string, float> s_lineNext = new Dictionary<string, float>();
        private static ServiceRef<IVOService> s_vo;

        private float _selfNext = float.NegativeInfinity;
        private float _clicksNext = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_globalNext = float.NegativeInfinity;
            s_lineNext.Clear();
        }

        /// <summary>Clears every cooldown (tests, checkpoint reload).</summary>
        public static void ResetCooldowns() => ResetStatics();

        /// <summary>
        /// Plays a bark if every cooldown allows it (or <paramref name="force"/>). Returns true when it was sent to the
        /// VO service (or would have been, when <paramref name="vo"/> is null and there is no service: tests).
        /// </summary>
        public bool Bark(string lineId, Transform speaker, Team team, float now, float lineCooldown = DefaultLineCooldown,
                         bool force = false, IVOService vo = null)
        {
            if (string.IsNullOrEmpty(lineId)) return false;
            if (!force)
            {
                if (now < s_globalNext || now < _selfNext) return false;
                if (s_lineNext.TryGetValue(lineId, out var next) && now < next) return false;
            }
            s_globalNext = now + GlobalCooldown;
            _selfNext = now + SpeakerCooldown;
            s_lineNext[lineId] = now + Mathf.Max(0f, lineCooldown);
            var service = vo ?? s_vo.Value;
            service?.PlayBark(lineId, speaker, team);
            return true;
        }

        /// <summary>Greycoat squelch clicks (1 hold, 2 move, 3 target sighted; GDD §14.4).</summary>
        public bool Clicks(int count, Transform speaker, float now, bool force = false, IVOService vo = null)
        {
            if (count <= 0) return false;
            if (!force && now < _clicksNext) return false;
            _clicksNext = now + ClickCooldown;
            var service = vo ?? s_vo.Value;
            service?.PlayClicks(count, speaker);
            return true;
        }

        /// <summary>Resets this speaker's own cooldowns (pool reuse).</summary>
        public void ResetSelf()
        {
            _selfNext = float.NegativeInfinity;
            _clicksNext = float.NegativeInfinity;
        }
    }
}
