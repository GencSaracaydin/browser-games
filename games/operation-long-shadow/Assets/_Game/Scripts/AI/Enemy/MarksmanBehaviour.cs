using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// E5-01, the Greycoat marksman on the mast platform (GDD §7.5, §14.1; TECH_PLAN §7.6 MarksmanBehaviour): sees 160 m
    /// through the scope whatever the LightStage (archetype fixed sight range), shows the <see cref="ScopeGlint"/> for
    /// 1.5 s before each shot, fires one round per 3.5 s with the motion table (0.8 still / 0.35 moving / 0.2 sprinting),
    /// takes an attack token like everyone else, ceases fire on <see cref="SequenceIds.MidAStart"/> (settles, BK_GRY_05
    /// faintly over the net) and is despawned at LightStage s ≥ 2.0. Raises <see cref="AIEvents.MarksmanFirstGlint"/> and
    /// <see cref="AIEvents.MarksmanFirstShot"/> once (cues for D035/D036).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarksmanBehaviour : MonoBehaviour, IEnemyBehaviour
    {
        /// <summary>Glint lead before each shot (GDD §7.5).</summary>
        public const float GlintLead = 1.5f;
        /// <summary>Seconds per shot (GDD §14.1).</summary>
        public const float ShotInterval = 3.5f;
        /// <summary>LightStage s at which he is withdrawn (GDD §7.5, 18:40).</summary>
        public const float WithdrawAtS = 2.0f;
        /// <summary>Volume of the faint BK_GRY_05 on his net.</summary>
        public const float SettleBarkVolume = 0.35f;

        [SerializeField] private ScopeGlint _glint;

        private EnemyBrain _brain;
        private float _glintAt = -1f;
        private float _nextCycle;
        private bool _ceased;
        private bool _firstGlint;
        private bool _firstShot;
        private bool _subscribed;
        private bool _withdrawn;
        private ServiceRef<IVOService> _vo;

        /// <summary>True after MID-A started.</summary>
        public bool Ceased => _ceased;
        /// <summary>The glint (may be null before configuration).</summary>
        public ScopeGlint Glint => _glint;

        /// <summary>Binds the glint (builders).</summary>
        public void Configure(ScopeGlint glint) => _glint = glint;

        /// <inheritdoc/>
        public void OnConfigured(EnemyBrain brain)
        {
            _brain = brain;
            _ceased = false;
            _withdrawn = false;
            _glintAt = -1f;
            _nextCycle = Time.time + brain.Rng.Range(1f, 2f);
            if (_glint == null) _glint = GetComponentInChildren<ScopeGlint>(true);
            if (_glint == null)
            {
                var go = new GameObject("ScopeGlint");
                var head = brain.Body?.Head;
                go.transform.SetParent(head != null && head != transform ? head : transform, false);
                go.transform.localPosition = head != null && head != transform ? new Vector3(0.04f, 0.0f, 0.35f) : new Vector3(0.04f, 1.62f, 0.35f);
                _glint = go.AddComponent<ScopeGlint>();
            }
            _glint.Hide();
            if (!_subscribed)
            {
                GameEvents.MissionEvent += OnMissionEvent;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                GameEvents.MissionEvent -= OnMissionEvent;
                _subscribed = false;
            }
            if (_glint != null) _glint.Hide();
        }

        private void OnMissionEvent(string id)
        {
            if (id != SequenceIds.MidAStart || _ceased || _brain == null || !_brain.IsActive) return;
            Cease();
        }

        /// <summary>Stops firing for good (MID-A): settles and murmurs BK_GRY_05 on the net.</summary>
        public void Cease()
        {
            _ceased = true;
            _glintAt = -1f;
            if (_glint != null) _glint.Hide();
            _brain?.Director?.Tokens.Release(_brain.AgentId);
            _brain?.Body?.SetAim(null);
            _vo.Value?.Play(BarkIds.GryMarksman, new VOOptions { ForceRadio = true, Volume = SettleBarkVolume, Priority = VOPriority.Bark });
        }

        /// <inheritdoc/>
        public bool OverrideTick(EnemyBrain brain, float now, float dt)
        {
            var director = brain.Director;
            if (!_withdrawn && director != null && director.StageS >= WithdrawAtS)
            {
                _withdrawn = true;
                if (_glint != null) _glint.Hide();
                Encounters.EncounterSpawner.Instance?.DespawnEnemy(brain.Actor.ActorId);       // GDD §7.5: withdrawn at 18:40
                return true;
            }
            if (_ceased) return true;
            if (brain.State != EnemyState.Combat)
            {
                if (_glint != null) _glint.Hide();
                return false;                                                                  // perception makes him aware
            }
            var player = director?.Player;
            var memory = player != null ? brain.Perception.Find(player) : null;
            var visible = memory != null && memory.Aware && memory.Visible;
            if (!visible)
            {
                if (_glintAt >= 0f)
                {
                    _glintAt = -1f;
                    _glint?.Hide();
                    _nextCycle = Mathf.Max(_nextCycle, now + 0.5f);
                }
                director?.Tokens.Release(brain.AgentId);
                if (memory != null) brain.Body?.SetAim(memory.LastKnown + Vector3.up * 1.3f);
                return true;
            }
            brain.Body?.SetAim(Perception.AimOf(player));
            var token = director.Tokens.Request(brain.AgentId, now, director.AimedAgent == brain.AgentId);
            if (_glintAt < 0f)
            {
                if (now < _nextCycle || !token) return true;
                _glintAt = now;
                _glint?.Show();
                if (!_firstGlint)
                {
                    _firstGlint = true;
                    GameEvents.RaiseMissionEvent(AIEvents.MarksmanFirstGlint);
                }
                return true;
            }
            if (now - _glintAt < GlintLead) return true;
            // GDD §14.1: one round, then the next glint no earlier than 3.5 s after this shot (glint 1.5 s before it).
            brain.FireRound(player, memory, false, now);
            _glintAt = -1f;
            _glint?.Hide();
            _nextCycle = now + ShotInterval - GlintLead;
            if (!_firstShot)
            {
                _firstShot = true;
                GameEvents.RaiseMissionEvent(AIEvents.MarksmanFirstShot);
            }
            return true;
        }

        /// <inheritdoc/>
        public void OnDeath(EnemyBrain brain, in DamageInfo d)
        {
            if (_glint != null) _glint.Hide();
        }
    }
}
