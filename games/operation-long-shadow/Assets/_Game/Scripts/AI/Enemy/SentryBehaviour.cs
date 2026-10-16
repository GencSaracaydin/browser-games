using System.Collections.Generic;
using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// Beat 2 pump-house sentries (GDD §7.2 "Silent opener", TECH_PLAN §7.6 SentryIdle): while unaware they smoke
    /// (<see cref="AIActionIds.SmokeIdle"/> loop) and chat, one asking "Ruvo tesk, ollan?" (BK_LEG_14) every 15 s and the
    /// other replying "Ollan, ollan." (BK_LEG_15) 2.5 s later. 30 HP while unaware and the narrower attention are handled
    /// by <see cref="EnemyBrain"/> (<see cref="SentryAttention"/>). Inactive on enemies whose table row is not a sentry,
    /// so it can sit on every rifleman prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SentryBehaviour : MonoBehaviour, IEnemyBehaviour
    {
        /// <summary>Seconds between chatter exchanges (GDD §7.2).</summary>
        public const float ChatterInterval = 15f;
        /// <summary>Delay of the reply.</summary>
        public const float ReplyDelay = 2.5f;

        private static readonly List<SentryBehaviour> s_active = new List<SentryBehaviour>(4);

        private EnemyBrain _brain;
        private bool _active;
        private bool _smoking;
        private float _nextAsk;
        private float _replyAt = float.PositiveInfinity;
        private SentryBehaviour _partner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();

        /// <summary>True while this enemy is a configured sentry.</summary>
        public bool IsSentry => _active;

        /// <inheritdoc/>
        public void OnConfigured(EnemyBrain brain)
        {
            _brain = brain;
            _active = brain.Context?.Def != null && brain.Context.Def.Sentry;
            _smoking = false;
            _partner = null;
            _replyAt = float.PositiveInfinity;
            s_active.Remove(this);
            if (!_active) return;
            s_active.Add(this);
            _nextAsk = Time.time + brain.Rng.Range(3f, 6f);
        }

        /// <inheritdoc/>
        public bool OverrideTick(EnemyBrain brain, float now, float dt)
        {
            if (!_active) return false;
            if (brain.State != EnemyState.Idle)
            {
                _smoking = false;
                if (brain.State == EnemyState.Combat) Deactivate();
                return false;
            }
            if (!_smoking && brain.Body != null)
            {
                _smoking = true;
                brain.Body.PlayAction(AIActionIds.SmokeIdle);
            }
            if (now >= _replyAt)
            {
                _replyAt = float.PositiveInfinity;
                brain.Barker.Bark(BarkIds.LegSentryReply, transform, brain.Team, now, 5f, true);
            }
            if (IsAsker && now >= _nextAsk)
            {
                _nextAsk = now + ChatterInterval;
                brain.Barker.Bark(BarkIds.LegSentryAsk, transform, brain.Team, now, 5f, true);
                var partner = FindPartner();
                if (partner != null) partner._replyAt = now + ReplyDelay;
            }
            return false;
        }

        /// <inheritdoc/>
        public void OnDeath(EnemyBrain brain, in DamageInfo d) => Deactivate();

        private void OnDisable() => Deactivate();

        private void Deactivate()
        {
            _active = false;
            _smoking = false;
            s_active.Remove(this);
        }

        /// <summary>The asker is the sentry with the lowest actor id among the active ones (deterministic).</summary>
        private bool IsAsker
        {
            get
            {
                var mine = _brain != null ? _brain.Actor.ActorId : null;
                for (var i = 0; i < s_active.Count; i++)
                {
                    var other = s_active[i];
                    if (other == this || other == null || other._brain == null) continue;
                    if (string.CompareOrdinal(other._brain.Actor.ActorId, mine) < 0) return false;
                }
                return s_active.Count > 1;
            }
        }

        private SentryBehaviour FindPartner()
        {
            if (_partner != null && _partner._active) return _partner;
            for (var i = 0; i < s_active.Count; i++)
            {
                var other = s_active[i];
                if (other != this && other != null && other._active && other._brain != null && other._brain.State == EnemyState.Idle)
                    return _partner = other;
            }
            return null;
        }
    }
}
