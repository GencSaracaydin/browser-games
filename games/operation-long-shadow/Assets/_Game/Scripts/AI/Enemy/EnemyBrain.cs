using System;
using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// The enemy state machine (TECH_PLAN §7.6 EnemyBrain, GDD §14.3): Idle → (Suspicious, Beat 2 sentries) → Combat
    /// {MoveToCover, Cover, Peek, Open, Flank, Suppress, Hold, Throw, Flee, Reload, Perched, EnterPerch, Run, Ride} → Dead.
    /// Ticked by <see cref="AIDirector"/> (never by Update), perception scheduled under the shared 24-ray budget.
    /// <para>Combat rules: cover from hand-placed <see cref="CoverNode"/>s (Legion half cover, predictable pop-up peeks,
    /// re-evaluated every 6-9 s and pushing when the player is suppressed; Greycoats full cover, leaning, moving every
    /// 4-6 s with two squelch clicks), attack tokens before firing at the player (tokenless enemies shoot squadmates or
    /// just aim), hit chance per <see cref="HitChance"/>, burst cadence per <see cref="EnemyShooter"/>, grenades (the
    /// shared Legion pool; one per Greycoat after the player sits in one cover), grenade avoidance (Legion 60 %,
    /// Greycoat always), morale fallback (driven by the spawner), Greycoat pairs (suppressor + flanker on the designated
    /// route), barks and clicks through <see cref="EnemyBarker"/>. Specials plug in as <see cref="IEnemyBehaviour"/>s.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    public sealed class EnemyBrain : MonoBehaviour, IAITickable, IPerceptionOwner
    {
        /// <summary>Seconds between tactical decisions (target, token, cover, grenades).</summary>
        public const float DecisionInterval = 0.2f;
        /// <summary>Keep firing at the last known position this long after line of sight breaks (suppressive fire).</summary>
        public const float SuppressiveFireWindow = 2.5f;
        /// <summary>Greycoats call "Selt." after losing the player this long.</summary>
        public const float LostContactSeconds = 8f;
        /// <summary>Hostile frags within this distance make enemies flee (GDD §14.3).</summary>
        public const float GrenadeFleeRadius = 5f;
        /// <summary>Legion shout "Pirrha!" for frags within this distance (DIALOGUE BK_LEG_04).</summary>
        public const float GrenadeBarkRadius = 6f;
        /// <summary>Closest throw (m).</summary>
        public const float ThrowRangeMin = 7f;
        /// <summary>Farthest throw (GDD §13.2: 18 m).</summary>
        public const float ThrowRangeMax = 18f;
        /// <summary>No throw when an ally stands this close to the target point.</summary>
        public const float ThrowFriendlyClearance = 6f;
        /// <summary>Flee distance from a frag.</summary>
        public const float FleeDistance = 9f;
        /// <summary>Longest flee.</summary>
        public const float FleeMaxSeconds = 3.5f;
        /// <summary>A move that takes longer than this is abandoned and re-planned.</summary>
        public const float MoveTimeout = 14f;
        /// <summary>Planar distance that counts as arrived.</summary>
        public const float ArriveDistance = 0.7f;
        /// <summary>Lateral eye offset while leaning out of cover.</summary>
        public const float LeanEyeOffset = 0.45f;
        /// <summary>Wave-mates within this distance hear a contact shout.</summary>
        public const float AllyAlertRadius = 30f;
        /// <summary>Animator LOD distances (TECH_PLAN §7.8: rigs off beyond 40 m).</summary>
        public const float LodNear = 25f;
        /// <summary>Far LOD distance.</summary>
        public const float LodFar = 45f;
        /// <summary>A suspicious sentry calms down after the meter stays empty this long.</summary>
        public const float SuspicionCalmSeconds = 4f;

        private EnemyActor _actor;
        private ICharacterBody _body;
        private Archetype _archetype;
        private EnemySpawnContext _context;
        private Perception _perception;
        private EnemyShooter _shooter;
        private readonly EnemyBarker _barker = new EnemyBarker();
        private IEnemyBehaviour[] _behaviours = Array.Empty<IEnemyBehaviour>();
        private AIDirector _director;
        private AIRandom _rng;
        private Func<CoverNode, bool> _isExposed;
        private Vector3 _exposureThreat;

        private int _agentId;
        private EnemyState _state = EnemyState.Inactive;
        private CombatMode _mode;
        private CombatMode _resumeMode;
        private float _modeSince;
        private float _modeUntil;
        private float _nextDecision;
        private float _nextLod;
        private float _nextCoverEval;
        private float _calmSince;
        private float _combatSince;

        private ITargetable _target;
        private TargetMemory _targetMemory;
        private bool _hasToken;
        private bool _hadContact;
        private float _lastContact = float.NegativeInfinity;
        private bool _lostContactBarked;
        private bool _firstContactDone;
        private bool _squadSighted;
        private float _playerCoverCheckAt;
        private bool _playerInCover;

        private CoverNode _cover;
        private bool _peekLeft;
        private bool _leaning;
        private Vector3 _moveTarget;
        private float _moveStarted;
        private CombatMode _afterMove;
        private Vector3[] _route;
        private int _routeIndex;
        private bool _grenadeAvailable;
        private bool _fragOnArrival;
        private bool _throwReleased;
        private Vector3 _throwTarget;
        private bool _holding;
        private string[] _holdEvents;
        private bool _preferSquad;
        private string _preferSquadUntil;
        private bool _hidden;
        private bool _riding;
        private float _rideMovingSince = float.NegativeInfinity;
        private EnemyBrain _partner;
        private float _partnerWaitUntil;
        private bool _pendingAlly;
        private float _pendingAllyAt;
        private ITargetable _pendingAllyTarget;
        private Vector3 _pendingAllyPosition;
        private float _shuffleAt;
        private bool _subscribed;
        private bool _bodySubscribed;
        private readonly LinkTraversal _link = new LinkTraversal();

        /// <summary>Raised once when the enemy enters combat (the spawner alerts wave-mates and plays music hooks).</summary>
        public event Action<EnemyBrain> CombatEntered;

        // ------------------------------------------------------------------ public surface

        /// <summary>Top-level state.</summary>
        public EnemyState State => _state;
        /// <summary>Combat sub-state.</summary>
        public CombatMode Mode => _mode;
        /// <summary>Identity and targeting surface.</summary>
        public EnemyActor Actor => _actor != null ? _actor : (_actor = GetComponent<EnemyActor>());
        /// <summary>Body.</summary>
        public ICharacterBody Body => _body;
        /// <summary>Archetype.</summary>
        public Archetype Archetype => _archetype ?? Actor.Archetype;
        /// <summary>Spawn context (table row, wave, route, vehicle).</summary>
        public EnemySpawnContext Context => _context;
        /// <summary>Perception.</summary>
        public Perception Perception => _perception;
        /// <summary>Bark and click cooldowns of this speaker.</summary>
        public EnemyBarker Barker => _barker;
        /// <summary>The AI director (null before configuration).</summary>
        public AIDirector Director => _director;
        /// <summary>Director agent id (0 when not registered).</summary>
        public int AgentId => _agentId;
        /// <summary>Who the brain is fighting (null when unaware).</summary>
        public ITargetable Target => _target;
        /// <summary>True while it holds an attack token (fires at the player).</summary>
        public bool HasToken => _hasToken;
        /// <summary>Claimed cover node (null when none).</summary>
        public CoverNode Cover => _cover;
        /// <summary>Weapon in use (archetype weapon or the table override).</summary>
        public string WeaponId { get; private set; }
        /// <summary>Beat 2 sentry detection meter 0..1 (HUD icon, GDD §14.2).</summary>
        public float Suspicion01 => _perception != null && _state != EnemyState.Dead ? _perception.Awareness01 : 0f;
        /// <summary>True while hidden by a behaviour (B11 T3 behind its parapet).</summary>
        public bool Hidden => _hidden;
        /// <summary>The Greycoat pair partner, or null.</summary>
        public EnemyBrain Partner => _partner;
        /// <summary>True while alive and configured.</summary>
        public bool IsActive => _state != EnemyState.Inactive && _state != EnemyState.Dead;
        /// <summary>Burst cadence and magazine (behaviours reuse it for scripted fire).</summary>
        public EnemyShooter Shooter => _shooter;
        /// <summary>Shots fired since configuration (tests, diagnostics).</summary>
        public int RoundsFired => _shooter?.RoundsFired ?? 0;
        /// <summary>Random stream of this brain (behaviours share it).</summary>
        public ref AIRandom Rng => ref _rng;

        // ------------------------------------------------------------------ IPerceptionOwner

        /// <inheritdoc/>
        public Team Team => Actor.Team;
        /// <inheritdoc/>
        public Vector3 EyePosition
        {
            get
            {
                var head = _body?.Head;
                var eye = head != null && head != transform ? head.position : transform.position + Vector3.up * 1.6f;
                if (_leaning) eye += (_peekLeft ? -transform.right : transform.right) * LeanEyeOffset;
                return eye;
            }
        }
        /// <inheritdoc/>
        public Vector3 Forward => transform.forward;
        /// <inheritdoc/>
        public float FixedSightRange => Archetype.FixedSightRange;
        /// <inheritdoc/>
        public bool InCombat => _state == EnemyState.Combat;
        /// <inheritdoc/>
        public bool PerceptionEnabled => IsActive && !_holding && !_hidden && !Archetype.AlwaysAware;

        /// <inheritdoc/>
        public void AttentionLimits(out float rangeCap, out float fovDegrees)
        {
            if (IsDistractedSentry)
            {
                rangeCap = SentryAttention.RangeCap;
                fovDegrees = SentryAttention.Fov;
                return;
            }
            rangeCap = 0f;
            fovDegrees = 0f;
        }

        /// <inheritdoc/>
        public float DetectionTimeScale(ITargetable target, float distance)
        {
            if (!IsDistractedSentry) return 1f;
            var rig = _director != null ? _director.Rig : null;
            if (rig == null || target == null || target.Team != Team.Player)
                return SentryAttention.Scale(distance, MotionClassifier.Classify(Stance.Stand, target?.Velocity ?? Vector3.zero), false);
            var stance = rig.Stance;
            return SentryAttention.Scale(distance, MotionClassifier.Classify(stance, rig.Velocity), stance == Stance.Crouch || stance == Stance.Slide);
        }

        private bool IsDistractedSentry => _context?.Def != null && _context.Def.Sentry && _state != EnemyState.Combat;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            _actor = GetComponent<EnemyActor>();
            _behaviours = GetComponents<IEnemyBehaviour>();
            _isExposed = IsNodeExposed;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            if (_agentId != 0) Retire();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            GameEvents.Noise += OnNoise;
            GameEvents.GrenadeLanded += OnGrenadeLanded;
            GameEvents.MissionEvent += OnNamedEvent;
            GameEvents.TriggerEntered += OnTriggerEntered;
            GameEvents.Interacted += OnNamedEvent;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            GameEvents.Noise -= OnNoise;
            GameEvents.GrenadeLanded -= OnGrenadeLanded;
            GameEvents.MissionEvent -= OnNamedEvent;
            GameEvents.TriggerEntered -= OnTriggerEntered;
            GameEvents.Interacted -= OnNamedEvent;
            _subscribed = false;
        }

        /// <summary>
        /// Configures the brain for a spawn (after <see cref="EnemyActor.Prepare"/>): archetype, weapon, perception,
        /// registration with the director, hold/prefer/route/pair data from the table row, and the entry mode (ride,
        /// climb to the perch, idle, or straight into combat when the wave spawns alerted).
        /// </summary>
        public void Configure(EnemySpawnContext context)
        {
            Retire();
            _context = context ?? new EnemySpawnContext();
            _actor = GetComponent<EnemyActor>();
            _body = _actor.Body;
            _archetype = _actor.Archetype;
            _director = AIDirector.Ensure();
            _rng = AIRandom.FromId(_actor.ActorId ?? name, (uint)Time.frameCount);
            _perception = new Perception(this, _rng.Value);
            _shooter = new EnemyShooter(_archetype);
            _barker.ResetSelf();
            var def = _context.Def;
            WeaponId = def != null && !string.IsNullOrEmpty(def.WeaponOverride) ? def.WeaponOverride : _archetype.WeaponId;
            _body?.SetWeaponVisual(WeaponId);
            _grenadeAvailable = _archetype.Grenades == GrenadeRule.GreycoatOneEach;
            _fragOnArrival = def != null && def.FragOnArrival && _grenadeAvailable;
            _holdEvents = def?.HoldUntil;
            _holding = _holdEvents != null && _holdEvents.Length > 0;
            _preferSquadUntil = def?.PreferSquadUntil;
            _preferSquad = !string.IsNullOrEmpty(_preferSquadUntil);
            _route = _context.Route;
            _routeIndex = 0;
            _partner = null;
            _cover = null;
            _target = null;
            _targetMemory = null;
            _hasToken = false;
            _hadContact = false;
            _firstContactDone = false;
            _squadSighted = false;
            _lostContactBarked = false;
            _hidden = false;
            _leaning = false;
            _pendingAlly = false;
            _riding = false;
            _lastContact = float.NegativeInfinity;
            var now = Time.time;
            _nextDecision = now + _rng.Range(0f, DecisionInterval);
            _nextLod = now;
            _nextCoverEval = now;
            _state = EnemyState.Idle;
            _mode = CombatMode.None;
            _modeSince = now;
            _agentId = _director != null ? _director.Register(_actor, this, _perception, true) : 0;
            if (!_bodySubscribed && _body != null)
            {
                _body.AnimEvent += OnAnimEvent;
                _bodySubscribed = true;
            }
            if (def != null && def.Sentry) _actor.Health?.OverrideMax(30f);   // GDD §7.2: 30 HP while unaware
            _body?.SetStance(Stance.Stand);
            _body?.SetCoverPose(0);
            _body?.SetAim(null);
            SetPerchPose(def != null && def.IsStatic);

            for (var i = 0; i < _behaviours.Length; i++) _behaviours[i].OnConfigured(this);

            if (_context.Vehicle != null)
            {
                BeginRide(now);
            }
            else if (_context.PerchPosition.HasValue && def != null && def.Kind == Encounters.SpawnKind.Closet)
            {
                EnterCombatState(now, false);
                StartMove(_context.PerchPosition.Value, _archetype.RunSpeed > 0f ? _archetype.RunSpeed : 3f, CombatMode.EnterPerch, CombatMode.Perched, now);
                if (_archetype.LegionBarks) _barker.Bark(BarkIds.LegHurry, transform, Team, now);
            }
            if (_context.Alerted || _archetype.AlwaysAware) AlertToPlayer(now, null);
        }

        /// <summary>Unregisters from the director and stops everything (despawn, pool return).</summary>
        public void Retire()
        {
            if (_director != null && _agentId != 0) _director.Unregister(_agentId);
            _agentId = 0;
            _hasToken = false;
            if (_bodySubscribed && _body != null)
            {
                _body.AnimEvent -= OnAnimEvent;
                _bodySubscribed = false;
            }
            // No re-parenting here: Retire also runs from OnDisable, when Unity forbids SetParent (the pool takes it back).
            _riding = false;
            if (_state != EnemyState.Dead) _state = EnemyState.Inactive;
            _mode = CombatMode.None;
            _perception?.Clear();
        }

        // ------------------------------------------------------------------ tick

        /// <inheritdoc/>
        public void AITick(float now, float dt)
        {
            if (!IsActive) return;
            _actor.SampleVelocity(dt);
            if (now >= _nextLod) UpdateLod(now);
            if (_pendingAlly && now >= _pendingAllyAt)
            {
                _pendingAlly = false;
                if (_state != EnemyState.Combat) AlertTo(_pendingAllyTarget, _pendingAllyPosition, now);
            }
            if (_riding)
            {
                TickRide(now);
                return;
            }
            if (_link.Tick(_body?.Agent, _body, dt)) return;                                 // crossing a NavMeshLink
            for (var i = 0; i < _behaviours.Length; i++)
                if (_behaviours[i].OverrideTick(this, now, dt)) return;
            switch (_state)
            {
                case EnemyState.Idle:
                case EnemyState.Suspicious:
                    TickUnaware(now);
                    break;
                case EnemyState.Combat:
                    TickCombat(now, dt);
                    break;
            }
        }

        private void UpdateLod(float now)
        {
            _nextLod = now + 0.5f;
            var player = _director?.Player;
            if (player?.Root == null || _body == null) return;
            var d = (player.Root.position - transform.position).sqrMagnitude;
            _body.SetAnimatorLod(d < LodNear * LodNear ? 0 : d < LodFar * LodFar ? 1 : 2);
        }

        private void TickUnaware(float now)
        {
            if (_holding) return;
            if (_perception.AnyAware)
            {
                EnterCombatState(now, true);
                return;
            }
            var meter = _perception.Awareness01;
            if (_context?.Def != null && _context.Def.Sentry)
            {
                if (meter > 0.05f)
                {
                    if (_state != EnemyState.Suspicious)
                    {
                        _state = EnemyState.Suspicious;
                        _calmSince = float.PositiveInfinity;
                    }
                    var m = MostSuspicious();
                    if (m != null) _body?.SetAim(m.LastKnown + Vector3.up * 1.4f);
                    _calmSince = float.PositiveInfinity;
                }
                else if (_state == EnemyState.Suspicious)
                {
                    if (float.IsPositiveInfinity(_calmSince)) _calmSince = now;
                    else if (now - _calmSince >= SuspicionCalmSeconds)
                    {
                        _state = EnemyState.Idle;
                        _body?.SetAim(null);
                    }
                }
            }
        }

        private TargetMemory MostSuspicious()
        {
            TargetMemory best = null;
            var mems = _perception.Memories;
            for (var i = 0; i < mems.Count; i++)
                if (best == null || mems[i].Detection > best.Detection) best = mems[i];
            return best;
        }

        private void TickCombat(float now, float dt)
        {
            if (now >= _nextDecision)
            {
                _nextDecision = now + DecisionInterval * _rng.Range(0.85f, 1.15f);
                Decide(now);
            }
            TickMode(now);
            UpdateFiring(now);
        }

        // ------------------------------------------------------------------ awareness

        /// <summary>Makes the enemy aware of the player (and the squad) at their current positions and enters combat.</summary>
        public void AlertToPlayer(float now, Vector3? lastKnown)
        {
            if (!IsActive) return;
            var reg = _director?.Targets;
            var player = reg?.Player;
            if (player != null) _perception.ForceAware(player, lastKnown ?? player.Root.position, now);
            if (reg != null)
            {
                s_squad.Clear();
                reg.GetHostilesOf(Team, s_squad);
                for (var i = 0; i < s_squad.Count; i++)
                {
                    var t = s_squad[i];
                    if (t.Team == Team.Squad && t.Root != null) _perception.ForceAware(t, t.Root.position, now);
                }
                s_squad.Clear();
            }
            _holding = false;
            // Alerted arrivals shout too ("3 clicks + BK_GRY_01" for the E4 pair, 3 clicks at the E7 breach, GDD §7.4, §7.7).
            if (_state != EnemyState.Combat) EnterCombatState(now, !_archetype.AlwaysAware);
        }

        private static readonly System.Collections.Generic.List<ITargetable> s_squad = new System.Collections.Generic.List<ITargetable>(8);

        /// <summary>Makes the enemy aware of one target (damage, an ally's shout) and enters combat.</summary>
        public void AlertTo(ITargetable target, Vector3 lastKnown, float now)
        {
            if (!IsActive) return;
            if (target != null) _perception.ForceAware(target, lastKnown, now);
            _holding = false;
            if (_state != EnemyState.Combat) EnterCombatState(now, true);
        }

        /// <summary>An ally shouted contact: become aware after a short reaction (0.4-0.9 s).</summary>
        public void AlertFromAlly(ITargetable target, Vector3 lastKnown, float now)
        {
            if (!IsActive || _state == EnemyState.Combat || _holding) return;
            _pendingAlly = true;
            _pendingAllyAt = now + _rng.Range(0.4f, 0.9f);
            _pendingAllyTarget = target;
            _pendingAllyPosition = lastKnown;
        }

        private void EnterCombatState(float now, bool bark)
        {
            if (_state == EnemyState.Combat) return;
            var wasUnaware = _state == EnemyState.Idle || _state == EnemyState.Suspicious;
            _state = EnemyState.Combat;
            _combatSince = now;
            _director?.Combat.Set(_agentId, true);
            if (_context?.Def != null && _context.Def.Sentry) _actor.Health?.SetMaxKeepDamage(_archetype.Hp);
            if (wasUnaware && _body != null && _body.IsActionPlaying) _body.PlayAction(AIActionIds.Stop);
            SelectTarget(now);
            if (bark) ContactBark(now);
            if (!_riding && (_mode == CombatMode.None || _mode == CombatMode.Hold)) ChooseEntryMode(now);
            CombatEntered?.Invoke(this);
        }

        private void ContactBark(float now)
        {
            if (_firstContactDone || _target == null) return;
            _firstContactDone = true;
            if (_archetype.Squelch)
            {
                _barker.Clicks(3, transform, now, true);                                         // GDD §14.4: 3 on spotting
                _barker.Bark(BarkIds.GryContact, transform, Team, now, 4f, true);
                return;
            }
            if (!_archetype.LegionBarks) return;
            if (_target.Team == Team.Squad && !_squadSighted)
            {
                _squadSighted = true;
                _barker.Bark(BarkIds.LegAmericans, transform, Team, now, 20f);                   // Beats 2-4 first sight
            }
            else _barker.Bark(BarkIds.LegContact, transform, Team, now, 8f);
        }

        private void ChooseEntryMode(float now)
        {
            var def = _context?.Def;
            if (IsStaticNow)
            {
                SetMode(CombatMode.Perched, now);
                return;
            }
            if (def != null && def.HoldSeconds > 0f && def.PushTo.HasValue)
            {
                SetMode(CombatMode.Hold, now);
                _modeUntil = now + def.HoldSeconds;
                if (!string.IsNullOrEmpty(def.HoldBark)) _barker.Bark(def.HoldBark, transform, Team, now, 30f, true);
                return;
            }
            if (def != null && def.Role == Encounters.PairRole.Suppressor)
            {
                if (_context.SuppressPosition.HasValue)
                    StartMove(_context.SuppressPosition.Value, _archetype.RunSpeed, CombatMode.MoveToCover, CombatMode.Suppress, now);
                else SeekCover(now, false, false, false, CombatMode.Suppress);
                return;
            }
            if (_route != null && _route.Length > 0 && _routeIndex < _route.Length)
            {
                if (def != null && def.Role == Encounters.PairRole.Flanker && _partner != null && _partner.IsActive)
                {
                    SetMode(CombatMode.Hold, now);
                    _modeUntil = now + 3f;                                                         // wait for the suppressor
                    _partnerWaitUntil = now + 3f;
                    return;
                }
                StartFlank(now);
                return;
            }
            SeekCover(now, false, false, false, CombatMode.Cover);
        }

        // ------------------------------------------------------------------ decisions

        private void Decide(float now)
        {
            SelectTarget(now);
            UpdateToken(now);
            UpdateContact(now);
            if (CanReconsiderCover && _target != null) ReconsiderCover(now);
            if (CanThrow) ConsiderGrenade(now);
        }

        private bool CanReconsiderCover =>
            !IsStaticNow && (_mode == CombatMode.Cover || _mode == CombatMode.Peek || _mode == CombatMode.Open);

        private bool CanThrow =>
            _mode == CombatMode.Cover || _mode == CombatMode.Peek || _mode == CombatMode.Open || _mode == CombatMode.Suppress;

        private bool IsStaticNow
        {
            get
            {
                if (_mode == CombatMode.Perched || _riding) return true;
                if (_archetype == null || !_archetype.UsesNavMesh) return true;
                return _context?.Def != null && _context.Def.IsStatic && _mode != CombatMode.EnterPerch;
            }
        }

        private void SelectTarget(float now)
        {
            var player = _director?.Player;
            if (_preferSquad)
            {
                var sq = BestSquadMemory();
                if (sq != null)
                {
                    SetTarget(sq);
                    return;
                }
            }
            if (player != null)
            {
                var pm = _perception.Find(player);
                if (pm != null && pm.Aware)
                {
                    SetTarget(pm);
                    return;
                }
            }
            SetTarget(_perception.Best());
        }

        private void SetTarget(TargetMemory m)
        {
            _targetMemory = m;
            _target = m?.Target;
        }

        private TargetMemory BestSquadMemory()
        {
            TargetMemory best = null;
            var mems = _perception.Memories;
            for (var i = 0; i < mems.Count; i++)
            {
                var m = mems[i];
                if (!m.Aware || m.Target == null || m.Target.Team != Team.Squad || !m.Target.IsAlive) continue;
                if (best == null || (m.Visible && !best.Visible) || (m.Visible == best.Visible && m.Distance < best.Distance)) best = m;
            }
            return best;
        }

        private bool TargetIsPlayer => _target != null && _target.Team == Team.Player;

        private void UpdateToken(float now)
        {
            var wants = TargetIsPlayer && FiringMode && _targetMemory != null &&
                        (_targetMemory.Visible || now - _targetMemory.LastSeen <= SuppressiveFireWindow);
            if (wants && _director != null)
            {
                _hasToken = _director.Tokens.Request(_agentId, now, _director.AimedAgent == _agentId);
                return;
            }
            _director?.Tokens.Release(_agentId);
            _hasToken = false;
        }

        private bool FiringMode =>
            _mode == CombatMode.Peek || _mode == CombatMode.Open || _mode == CombatMode.Perched ||
            _mode == CombatMode.Suppress || _mode == CombatMode.Hold ||
            (_mode == CombatMode.Cover && _modeUntil - Time.time < 0.5f);

        private void UpdateContact(float now)
        {
            if (_targetMemory == null) return;
            if (_targetMemory.Visible)
            {
                if (_hadContact && _lostContactBarked && _archetype.Squelch)
                {
                    _barker.Clicks(3, transform, now);
                    _barker.Bark(BarkIds.GryEyesOn, transform, Team, now + 0.5f, 10f);         // "Orvak." re-acquired
                }
                _hadContact = true;
                _lostContactBarked = false;
                _lastContact = now;
                if (!_squadSighted && _archetype.LegionBarks && _target != null && _target.Team == Team.Squad)
                {
                    _squadSighted = true;
                    _barker.Bark(BarkIds.LegAmericans, transform, Team, now, 20f);
                }
                return;
            }
            if (_hadContact && !_lostContactBarked && now - _lastContact >= LostContactSeconds)
            {
                _lostContactBarked = true;
                if (_archetype.Squelch) _barker.Bark(BarkIds.GryClear, transform, Team, now, 15f);  // "Selt."
            }
        }

        private Vector3 ThreatPosition
        {
            get
            {
                if (_targetMemory != null) return _targetMemory.Visible && _target?.Root != null ? _target.Root.position : _targetMemory.LastKnown;
                var p = _director?.Player;
                return p?.Root != null ? p.Root.position : transform.position + transform.forward * 20f;
            }
        }

        private void ReconsiderCover(float now)
        {
            var threat = ThreatPosition;
            var compromised = _cover != null && !_cover.Protects(threat);
            if (now < _nextCoverEval && !compromised && !(_mode == CombatMode.Open && now >= _shuffleAt)) return;
            if (now >= _nextCoverEval) _nextCoverEval = now + _rng.Range(_archetype.CoverReevalMin, Mathf.Max(_archetype.CoverReevalMin, _archetype.CoverReevalMax));
            var greycoat = _archetype.PrefersFullCover;
            var distance = Planar(threat - transform.position).magnitude;
            var advance = !greycoat && _director != null && _director.PlayerSuppressed(now) && TargetIsPlayer && distance > _archetype.RangeMin + 4f;
            var exclude = greycoat || advance || compromised ? _cover : null;
            var node = PickCover(threat, advance, false, greycoat, exclude, transform.position);
            if (node != null && node != _cover)
            {
                if (advance && _archetype.LegionBarks) _barker.Bark(BarkIds.LegPush, transform, Team, now, 10f);   // "Druna!"
                MoveToCover(node, now);
                return;
            }
            if (_cover == null && _mode == CombatMode.Open && now >= _shuffleAt)
            {
                // No cover in reach: close to the far end of the range band, else sidestep (GDD §14.1 ranges).
                if (distance > _archetype.RangeMax + 5f && !IsStaticNow)
                {
                    var toward = Planar(threat - transform.position).normalized;
                    var p = NavUtil.SampleStopPoint(transform.position + toward * 8f, 3f, true);
                    _body?.MoveTo(p, _archetype.RunSpeed);
                    _shuffleAt = now + _rng.Range(3f, 5f);
                }
                else Shuffle(now);
            }
        }

        // ------------------------------------------------------------------ modes

        private void SetMode(CombatMode mode, float now)
        {
            _mode = mode;
            _modeSince = now;
            _leaning = false;
            if (mode == CombatMode.Perched)
            {
                _body?.SetStance(Stance.Stand);
                _body?.SetCoverPose(0);
            }
        }

        private void TickMode(float now)
        {
            switch (_mode)
            {
                case CombatMode.MoveToCover:
                case CombatMode.EnterPerch:
                case CombatMode.Run:
                    TickMove(now);
                    break;
                case CombatMode.Flank:
                    TickFlank(now);
                    break;
                case CombatMode.Cover:
                    if (now >= _modeUntil && !_actor.IsSuppressed(now)) StartPeek(now);
                    break;
                case CombatMode.Peek:
                    if (now >= _modeUntil) StartHide(now);
                    break;
                case CombatMode.Open:
                    break;
                case CombatMode.Suppress:
                    TickSuppress(now);
                    break;
                case CombatMode.Hold:
                    TickHold(now);
                    break;
                case CombatMode.Throw:
                    if (now >= _modeUntil) FinishThrow(now);
                    break;
                case CombatMode.Flee:
                    if (now >= _modeUntil || ArrivedAt(_moveTarget)) SeekCover(now, false, false, false, CombatMode.Cover);
                    break;
                case CombatMode.Reload:
                    if (now >= _modeUntil) FinishReload();
                    break;
            }
        }

        private void SeekCover(float now, bool advance, bool retreat, bool flankBias, CombatMode thenMode)
        {
            if (IsStaticNow)
            {
                SetMode(CombatMode.Perched, now);
                return;
            }
            var node = PickCover(ThreatPosition, advance, retreat, flankBias, retreat ? _cover : null, transform.position);
            if (node != null)
            {
                MoveToCover(node, now, thenMode);
                return;
            }
            if (retreat)
            {
                // No node behind: back off along the threat axis.
                var away = Planar(transform.position - ThreatPosition).normalized;
                var p = NavUtil.SampleStopPoint(transform.position + away * 6f, 3f, true);
                StartMove(p, _archetype.RunSpeed, CombatMode.MoveToCover, CombatMode.Open, now);
                return;
            }
            ReleaseCover();
            SetMode(thenMode == CombatMode.Suppress ? CombatMode.Suppress : CombatMode.Open, now);
            _shuffleAt = now + _rng.Range(3f, 5f);
            _body?.Stop();
        }

        private CoverNode PickCover(Vector3 threat, bool advance, bool retreat, bool flankBias, CoverNode exclude, Vector3 around)
        {
            if (_director == null) return null;
            var q = new CoverQuery
            {
                Agent = _agentId,
                AgentPosition = around,
                ThreatPosition = threat,
                PreferredMin = _archetype.RangeMin,
                PreferredMax = _archetype.RangeMax,
                SearchRadius = retreat ? 35f : 30f,
                PreferStanding = _archetype.PrefersFullCover,
                PreferredGroup = _context?.Def?.CoverGroup ?? _context?.Wave?.CoverGroup,
                Advance = advance,
                Retreat = retreat,
                FlankBias = flankBias,
                Exclude = exclude,
            };
            _exposureThreat = threat + Vector3.up * 1.5f;
            var rays = _director.Budget.Remaining(Time.frameCount) >= 6 ? 2 : 0;
            return CoverSelector.Select(q, _director.CoverNodes, _director.Claims, _isExposed, rays);
        }

        private bool IsNodeExposed(CoverNode n)
        {
            if (_director == null || !_director.TryUseRays()) return false;
            var p = n.Position + Vector3.up * (n.Standing ? 1.45f : 0.95f);
            return !Physics.Linecast(_exposureThreat, p, LayerIds.AISight, QueryTriggerInteraction.Ignore);
        }

        private void MoveToCover(CoverNode node, float now, CombatMode thenMode = CombatMode.Cover)
        {
            if (_director == null || node == null || !_director.Claims.TryClaim(node, _agentId))
            {
                SetMode(CombatMode.Open, now);
                return;
            }
            var moving = _cover != null || _mode == CombatMode.Peek || _mode == CombatMode.Open;
            _cover = node;
            if (_archetype.Squelch) _barker.Clicks(2, transform, now);                            // GDD §14.4: 2 before moving
            else if (moving && _archetype.LegionBarks && _rng.Chance(0.4f)) _barker.Bark(BarkIds.LegCoverMe, transform, Team, now, 6f);
            StartMove(node.Position, _archetype.RunSpeed, CombatMode.MoveToCover, thenMode, now);
        }

        private void ReleaseCover()
        {
            if (_cover != null) _director?.Claims.Release(_agentId);
            _cover = null;
        }

        private void StartMove(Vector3 destination, float speed, CombatMode moveMode, CombatMode thenMode, float now)
        {
            SetMode(moveMode, now);
            _afterMove = thenMode;
            _moveTarget = destination;
            _moveStarted = now;
            _body?.SetCoverPose(0);
            _body?.SetStance(Stance.Stand);
            _body?.SetAim(null);
            _body?.MoveTo(destination, Mathf.Max(0.5f, speed));
        }

        private void TickMove(float now)
        {
            if (_mode == CombatMode.EnterPerch && now - _moveStarted > 6f)
            {
                // Perch unreachable on the NavMesh (closet behind a balcony): step out of the closet onto it.
                Teleport(_moveTarget, transform.rotation);
                ArriveFromMove(now);
                return;
            }
            if (ArrivedAt(_moveTarget))
            {
                ArriveFromMove(now);
                return;
            }
            if (now - _moveStarted > MoveTimeout)
            {
                ReleaseCover();
                SeekCover(now, false, false, false, CombatMode.Cover);
            }
        }

        private void ArriveFromMove(float now)
        {
            _body?.Stop();
            switch (_afterMove)
            {
                case CombatMode.Perched:
                    var perchId = _context?.Def?.PerchId;
                    _actor.SetPerch(_context != null ? _context.Perch : PerchTag.Ground);
                    SetPerchPose(true);
                    var agent = _body?.Agent;
                    if (agent != null) agent.enabled = false;
                    SetMode(CombatMode.Perched, now);
                    if (!string.IsNullOrEmpty(perchId) && MarkerRegistry.Instance.TryGet(perchId, out PerchNode pn) && pn.transform.forward.sqrMagnitude > 0f)
                        transform.rotation = Quaternion.LookRotation(Planar(pn.transform.forward).normalized, Vector3.up);
                    break;
                case CombatMode.Suppress:
                    SetMode(CombatMode.Suppress, now);
                    ApplyCoverPose(false);
                    break;
                case CombatMode.Open:
                    SetMode(CombatMode.Open, now);
                    _shuffleAt = now + _rng.Range(3f, 5f);
                    break;
                default:
                    StartHide(now);
                    break;
            }
            // GDD §7.9 wave 2: "one throws a frag on arrival" (else the regular trigger covers it later).
            if (_fragOnArrival && _grenadeAvailable && TargetIsPlayer && _targetMemory != null &&
                Planar(_targetMemory.LastKnown - transform.position).magnitude <= ThrowRangeMax + 2f)
                TryThrow(now, true);
        }

        private void StartHide(float now)
        {
            if (_cover == null)
            {
                SetMode(CombatMode.Open, now);
                _shuffleAt = now + _rng.Range(3f, 5f);
                return;
            }
            SetMode(CombatMode.Cover, now);
            ApplyCoverPose(false);
            var greycoat = _archetype.PrefersFullCover;
            // Legion "peek predictably": a near-constant rhythm; Greycoats vary it (GDD §14.1).
            var hide = greycoat ? _rng.Range(0.8f, 2.4f) : 1.6f * _rng.Range(0.92f, 1.08f);
            if (_actor.IsSuppressed(now)) hide += 1.2f;
            _modeUntil = now + hide;
            if (_shooter.NeedsReload || (_shooter.Magazine < _archetype.Magazine / 4 && _archetype.Magazine > 0)) StartReload(now);
        }

        private void StartPeek(float now)
        {
            if (_cover == null)
            {
                SetMode(CombatMode.Open, now);
                return;
            }
            SetMode(CombatMode.Peek, now);
            var greycoat = _archetype.PrefersFullCover;
            if (greycoat || (_cover.LeanLeft != _cover.LeanRight)) _peekLeft = _cover.LeanLeft && (!_cover.LeanRight || _rng.Chance(0.5f));
            ApplyCoverPose(true);
            _modeUntil = now + (greycoat ? _rng.Range(1.0f, 2.2f) : 2.0f * _rng.Range(0.92f, 1.08f));
        }

        private void ApplyCoverPose(bool peeking)
        {
            if (_body == null) return;
            if (_cover == null)
            {
                _body.SetCoverPose(0);
                _body.SetStance(Stance.Stand);
                return;
            }
            if (!peeking)
            {
                _leaning = false;
                _body.SetStance(_cover.Standing ? Stance.Stand : Stance.Crouch);
                _body.SetCoverPose(_cover.Standing ? 2 : 1);
                return;
            }
            // Full cover: lean out (3 left, 4 right). Half cover: pop up over it (Legion's predictable peek).
            if (_cover.Standing && (_cover.LeanLeft || _cover.LeanRight))
            {
                _leaning = true;
                _body.SetStance(Stance.Stand);
                _body.SetCoverPose(_peekLeft ? 3 : 4);
            }
            else
            {
                _leaning = false;
                _body.SetStance(Stance.Stand);
                _body.SetCoverPose(0);
            }
        }

        private void Shuffle(float now)
        {
            _shuffleAt = now + _rng.Range(3f, 5f);
            if (IsStaticNow) return;
            var side = transform.right * (_rng.Sign() * _rng.Range(1.5f, 2.5f));
            var p = NavUtil.SampleStopPoint(transform.position + side, 1.5f, true);
            _body?.MoveTo(p, _archetype.WalkSpeed > 0f ? _archetype.WalkSpeed : 1.5f);
        }

        private void StartFlank(float now)
        {
            if (_route == null || _routeIndex >= _route.Length)
            {
                SeekCover(now, false, false, true, CombatMode.Cover);
                return;
            }
            SetMode(CombatMode.Flank, now);
            if (_archetype.Squelch)
            {
                _barker.Clicks(2, transform, now, true);                                          // GDD §14.4
                _barker.Bark(BarkIds.GrySuppressMove, transform, Team, now + 0.5f, 10f);          // "Trekt, isk!"
            }
            else if (_archetype.LegionBarks) _barker.Bark(BarkIds.LegFlank, transform, Team, now, 10f);   // "Vossek!"
            _moveTarget = _route[_routeIndex];
            _moveStarted = now;
            _body?.SetStance(Stance.Stand);
            _body?.SetCoverPose(0);
            _body?.SetAim(null);
            _body?.MoveTo(_moveTarget, _archetype.RunSpeed);
        }

        private void TickFlank(float now)
        {
            if (ArrivedAt(_moveTarget, 1.5f) || now - _moveStarted > MoveTimeout)
            {
                _routeIndex++;
                if (_route == null || _routeIndex >= _route.Length)
                {
                    SeekCover(now, false, false, true, CombatMode.Cover);
                    return;
                }
                _moveTarget = _route[_routeIndex];
                _moveStarted = now;
                _body?.MoveTo(_moveTarget, _archetype.RunSpeed);
            }
        }

        /// <summary>True while following a flank route (the pair suppressor keeps firing until it ends).</summary>
        public bool IsFlanking => _mode == CombatMode.Flank || (_mode == CombatMode.Hold && _route != null && _routeIndex == 0 && _partner != null);

        private void TickSuppress(float now)
        {
            ApplyCoverPose(true);
            var partnerBusy = _partner != null && _partner.IsActive && _partner.IsFlanking;
            if (!partnerBusy && now - _modeSince > 6f)
            {
                if (_cover != null) StartHide(now);
                else SeekCover(now, false, false, false, CombatMode.Cover);
            }
        }

        private void TickHold(float now)
        {
            var def = _context?.Def;
            if (_partner != null && _partner.Mode == CombatMode.Suppress && now - _modeSince > 0.8f && _route != null && _routeIndex == 0)
                _modeUntil = Mathf.Min(_modeUntil, now);
            if (now < _modeUntil) return;
            if (_route != null && _routeIndex < _route.Length && (def == null || def.HoldSeconds <= 0f))
            {
                StartFlank(now);
                return;
            }
            if (def != null && def.PushTo.HasValue)
            {
                var node = PickCover(ThreatPosition, false, false, false, null, def.PushTo.Value);
                if (node != null) MoveToCover(node, now);
                else StartMove(def.PushTo.Value, _archetype.RunSpeed, CombatMode.MoveToCover, CombatMode.Open, now);
                return;
            }
            SeekCover(now, false, false, false, CombatMode.Cover);
        }

        // ------------------------------------------------------------------ firing

        private void UpdateFiring(float now)
        {
            ITargetable shootAt = null;
            TargetMemory mem = null;
            var suppressOnly = false;
            if (FiringMode && _mode != CombatMode.Cover && !_hidden && _target != null)
            {
                if (TargetIsPlayer)
                {
                    if (_hasToken)
                    {
                        mem = _targetMemory;
                        if (mem != null && mem.Visible) shootAt = _target;
                        else if (mem != null && now - mem.LastSeen <= SuppressiveFireWindow && _mode != CombatMode.Perched)
                        {
                            shootAt = _target;
                            suppressOnly = true;
                        }
                    }
                    else
                    {
                        mem = BestVisibleSquad();
                        shootAt = mem?.Target;
                    }
                }
                else
                {
                    mem = _targetMemory;
                    if (mem != null && mem.Visible) shootAt = _target;
                }
            }

            if (_body != null && _mode != CombatMode.MoveToCover && _mode != CombatMode.Flank && _mode != CombatMode.Flee && _mode != CombatMode.Run && _mode != CombatMode.EnterPerch)
            {
                if (shootAt != null) _body.SetAim(Perception.AimOf(shootAt));
                else if (_targetMemory != null) _body.SetAim(_targetMemory.LastKnown + Vector3.up * 1.3f);
            }

            var canFire = shootAt != null && !_shooter.NeedsReload && _mode != CombatMode.Reload && _mode != CombatMode.Throw;
            if (_shooter.Tick(now, canFire, _actor.IsSuppressed(now), ref _rng)) FireRound(shootAt, mem, suppressOnly, now);
            if (_shooter.NeedsReload && _mode != CombatMode.Reload && _mode != CombatMode.Throw && _mode != CombatMode.Flee) StartReload(now);
        }

        private TargetMemory BestVisibleSquad()
        {
            TargetMemory best = null;
            var mems = _perception.Memories;
            for (var i = 0; i < mems.Count; i++)
            {
                var m = mems[i];
                if (!m.Aware || !m.Visible || m.Target == null || m.Target.Team != Team.Squad || !m.Target.IsAlive) continue;
                if (best == null || m.Distance < best.Distance) best = m;
            }
            return best;
        }

        /// <summary>
        /// Fires one round at a target through <see cref="AICombat"/> with the GDD §14.3 hit chance. Behaviours call it
        /// for scripted fire (the technical at the arch, rooftop targets) with <paramref name="ignorePerception"/>.
        /// </summary>
        public void FireRound(ITargetable target, TargetMemory memory, bool suppressOnly, float now, bool ignorePerception = false)
        {
            if (target == null || _body == null) return;
            var muzzle = _body.Muzzle != null ? _body.Muzzle.position : EyePosition;
            var aimPoint = Perception.AimOf(target);
            var distance = Vector3.Distance(muzzle, aimPoint);
            var isPlayer = target.Team == Team.Player;
            var rig = _director?.Rig;
            var motion = isPlayer && rig != null
                ? MotionClassifier.Classify(rig.Stance, rig.Velocity)
                : MotionClassifier.Classify(Stance.Stand, target.Velocity);
            var difficulty = _director != null ? _director.Difficulty : DifficultyProfile.For(Difficulty.Regular);
            var marksman = _archetype.MotionAccuracyTable;
            var inputs = new HitChanceInputs
            {
                BaseAccuracy = marksman ? HitChance.MarksmanAccuracy(motion) : _archetype.Accuracy20,
                Distance = distance,
                RangeMax = _archetype.RangeMax,
                FlatRange = marksman,
                TargetMotion = motion,
                ApplyMotionFactor = !marksman,
                TargetInCover = isPlayer && PlayerInCoverFrom(muzzle, now),
                TimeOnTarget = ignorePerception ? HitChance.RampSeconds : memory?.TimeOnTarget(now) ?? 0f,
                TargetInFlood = isPlayer && _director != null && _director.PlayerInFlood,
                DifficultyMultiplier = difficulty.EnemyAccuracyMultiplier,
                ShooterSuppressed = _actor.IsSuppressed(now),
            };
            var request = new AIShotRequest
            {
                Shooter = _actor,
                Muzzle = muzzle,
                Target = target,
                HitChance = HitChance.Compute(inputs),
                Damage = ArchetypeTable.DamageToPlayer(_archetype, difficulty),
                WeaponId = WeaponId,
                Team = Team,
                TracerEvery = _archetype.TracerEvery,
                SuppressOnly = suppressOnly,
            };
            AICombat.Fire(request);
        }

        /// <summary>
        /// GDD §14.3 "in-cover factor": the player counts as in cover from this shooter when his chest is visible but his
        /// hips are not (one ray, cached 0.5 s, within the frame's ray budget).
        /// </summary>
        private bool PlayerInCoverFrom(Vector3 muzzle, float now)
        {
            if (now < _playerCoverCheckAt) return _playerInCover;
            _playerCoverCheckAt = now + 0.5f;
            var player = _director?.Player;
            if (player?.Root == null || _director == null || !_director.TryUseRays()) return _playerInCover;
            var hips = player.Root.position + Vector3.up * 0.55f;
            _playerInCover = Physics.Linecast(muzzle, hips, LayerIds.AISight, QueryTriggerInteraction.Ignore);
            return _playerInCover;
        }

        private void StartReload(float now)
        {
            if (_archetype.Magazine <= 0) return;
            _resumeMode = _mode == CombatMode.Peek ? CombatMode.Cover : _mode;
            SetMode(CombatMode.Reload, now);
            ApplyCoverPose(false);
            _modeUntil = now + Mathf.Max(1f, _archetype.ReloadSeconds) + 1.5f;
            if (_archetype.LegionBarks && _rng.Chance(0.6f)) _barker.Bark(BarkIds.LegReload, transform, Team, now, 8f);  // "Ollo!"
            _body?.PlayAction(ActionIds.Reload, OnReloadDone);
        }

        private void OnReloadDone()
        {
            if (_mode == CombatMode.Reload) FinishReload();
        }

        private void FinishReload()
        {
            _shooter.Refill();
            var now = Time.time;
            switch (_resumeMode)
            {
                case CombatMode.Perched:
                case CombatMode.Suppress:
                case CombatMode.Open:
                case CombatMode.Hold:
                    SetMode(_resumeMode, now);
                    break;
                default:
                    StartHide(now);
                    break;
            }
        }

        // ------------------------------------------------------------------ grenades

        private void ConsiderGrenade(float now)
        {
            if (!TargetIsPlayer || _targetMemory == null || _director == null) return;
            if (now - Mathf.Max(_targetMemory.LastSeen, _targetMemory.LastHeard) > 6f) return;
            var d = Planar(_targetMemory.LastKnown - transform.position).magnitude;
            if (d < ThrowRangeMin || d > ThrowRangeMax) return;
            switch (_archetype.Grenades)
            {
                case GrenadeRule.LegionSharedPool:
                    // Lob one at a player who hides (not visible for 1.5 s) or sits still in one place (GDD §14.1 pool).
                    var hiding = !_targetMemory.Visible && now - _targetMemory.LastSeen >= 1.5f;
                    var camping = _director.PlayerStaticSeconds(now) >= 5f;
                    if ((hiding || camping) && NoAllyNear(_targetMemory.LastKnown) && _director.LegionGrenades.Available(now))
                        TryThrow(now, false);
                    break;
                case GrenadeRule.GreycoatOneEach:
                    if (!_grenadeAvailable) return;
                    var trigger = _director.Difficulty.GreycoatGrenadeTrigger > 0f ? _director.Difficulty.GreycoatGrenadeTrigger : 6f;
                    if ((_fragOnArrival || _director.PlayerStaticSeconds(now) >= trigger) && NoAllyNear(_targetMemory.LastKnown))
                        TryThrow(now, true);
                    break;
            }
        }

        private bool NoAllyNear(Vector3 point)
        {
            var reg = _director?.Targets;
            if (reg == null) return true;
            return reg.CountWithin(Team.Legion, point, ThrowFriendlyClearance) + reg.CountWithin(Team.Greycoat, point, ThrowFriendlyClearance) == 0;
        }

        private void TryThrow(float now, bool greycoat)
        {
            if (_targetMemory == null || _mode == CombatMode.Throw) return;
            if (!greycoat && (_director == null || !_director.LegionGrenades.TryTake(now))) return;
            if (greycoat)
            {
                _grenadeAvailable = false;
                _fragOnArrival = false;
            }
            _resumeMode = _mode == CombatMode.Peek ? CombatMode.Cover : _mode;
            SetMode(CombatMode.Throw, now);
            _throwReleased = false;
            var scatter = new Vector3(_rng.Range(-1.2f, 1.2f), 0f, _rng.Range(-1.2f, 1.2f));
            _throwTarget = _targetMemory.LastKnown + scatter;
            _modeUntil = now + Mathf.Max(1.5f, ActionDurations.Timeout(ActionIds.Throw));
            _body?.Stop();
            _body?.SetStance(Stance.Stand);
            _body?.SetCoverPose(0);
            _body?.SetAim(_throwTarget + Vector3.up);
            _barker.Bark(greycoat ? BarkIds.GryGrenade : BarkIds.LegGrenade, transform, Team, now, 5f, true);
            if (_body != null) _body.PlayAction(ActionIds.Throw, OnThrowDone);
            else ReleaseGrenade();
        }

        private void OnAnimEvent(string id)
        {
            if (id == AnimEventIds.ThrowRelease && _mode == CombatMode.Throw) ReleaseGrenade();
        }

        private void OnThrowDone()
        {
            if (_mode == CombatMode.Throw) FinishThrow(Time.time);
        }

        private void ReleaseGrenade()
        {
            if (_throwReleased) return;
            _throwReleased = true;
            var hand = _body?.RightHand;
            var from = hand != null && hand != transform ? hand.position : transform.position + Vector3.up * 1.7f;
            AICombat.ThrowGrenade(from, _throwTarget, Team);
        }

        private void FinishThrow(float now)
        {
            ReleaseGrenade();
            if (_resumeMode == CombatMode.Perched || _resumeMode == CombatMode.Open || _resumeMode == CombatMode.Suppress)
            {
                SetMode(_resumeMode, now);
                return;
            }
            StartHide(now);
        }

        private void OnGrenadeLanded(Vector3 position, Team team)
        {
            if (!IsActive || !Teams.AreHostile(Team, team)) return;
            var d = Planar(position - transform.position).magnitude;
            var now = Time.time;
            if (d <= GrenadeBarkRadius && _archetype.LegionBarks) _barker.Bark(BarkIds.LegGrenade, transform, Team, now, 3f);
            if (_state != EnemyState.Combat && d <= 25f)
            {
                var player = _director?.Player;
                AlertTo(player, player?.Root != null ? player.Root.position : position, now);
            }
            if (d > GrenadeFleeRadius || IsStaticNow || _mode == CombatMode.Flee) return;
            if (!_rng.Chance(_archetype.FleeGrenadeChance)) return;                                // GDD §14.3: Legion 60 %, Greycoat 100 %
            var away = Planar(transform.position - position);
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            var dest = NavUtil.SampleStopPoint(position + away.normalized * FleeDistance, 3f, true);
            ReleaseCover();
            SetMode(CombatMode.Flee, now);
            _modeUntil = now + FleeMaxSeconds;
            _moveTarget = dest;
            _body?.SetStance(Stance.Stand);
            _body?.SetCoverPose(0);
            _body?.SetAim(null);
            _body?.MoveTo(dest, _archetype.SprintSpeed > 0f ? _archetype.SprintSpeed : 5f);
        }

        // ------------------------------------------------------------------ vehicles

        private void BeginRide(float now)
        {
            var v = _context.Vehicle;
            var seat = _context.Seat != null ? _context.Seat : v.Root;
            var agent = _body?.Agent;
            if (agent != null) agent.enabled = false;
            transform.SetParent(seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _riding = true;
            _rideMovingSince = float.NegativeInfinity;
            SetPerchPose(true);
            _actor.SetPerch(PerchTag.Vehicle);
            SetMode(CombatMode.None, now);
            if (!v.IsMoving && !v.Arrived) v.Play();
        }

        private void TickRide(float now)
        {
            var v = _context?.Vehicle;
            if (v == null || (v is UnityEngine.Object o && o == null))
            {
                EndRide(true);
                return;
            }
            if (v.IsMoving && float.IsNegativeInfinity(_rideMovingSince)) _rideMovingSince = now;
            // Arrived, or stopped after moving, or never started within 3 s (a truck nobody drives): the ride is over.
            var stopped = !v.IsMoving && (!float.IsNegativeInfinity(_rideMovingSince) || now - _context.SpawnTime > 3f);
            if (_context.Def != null && _context.Def.StaysOnVehicle)
            {
                // RPG gunner on the bed: perched on the vehicle for good (RpgBehaviour drives him).
                if (v.Arrived || stopped)
                {
                    _riding = false;
                    if (_state != EnemyState.Combat) EnterCombatState(now, false);
                    SetMode(CombatMode.Perched, now);
                }
                return;
            }
            if (v.Arrived || stopped) EndRide(true);
        }

        private void EndRide(bool dismount)
        {
            _riding = false;
            if (transform.parent != null) transform.SetParent(null, true);
            if (!dismount) return;
            var now = Time.time;
            var v = _context?.Vehicle;
            var drop = transform.position;
            if (v != null && v.SeatCount > 0 && v.GetSeat(0) != null)
            {
                var index = _context.Def != null ? _context.Def.Seat : 1;
                var tail = v.GetSeat(0);
                drop = tail.position + tail.right * ((index - 2) * 0.9f);
            }
            var ground = NavUtil.SampleStopPoint(drop, 4f, true);
            Teleport(ground, Facing(ground, ThreatPosition));
            SetPerchPose(false);
            _actor.SetPerch(PerchTag.Ground);
            var agent = _body?.Agent;
            if (agent != null && NavUtil.Sample(ground, 3f, out var p))
            {
                transform.position = p;
                agent.enabled = true;
                agent.Warp(p);
                agent.updateRotation = false;
                LinkTraversal.Prepare(agent);
            }
            if (_archetype.LegionBarks && _context.Def != null && _context.Def.Seat <= 1)
                _barker.Bark(BarkIds.LegForZaroud, transform, Team, now, 20f, true);               // GDD §7.10 trucks unload
            _body?.PlayAction(ActionIds.Drop);
            if (_state != EnemyState.Combat) AlertToPlayer(now, null);
            else ChooseEntryMode(now);
        }

        // ------------------------------------------------------------------ external commands

        /// <summary>Morale break (GDD §14.1: 60 % of the wave dead): fall back one node.</summary>
        public void FallBack(float now)
        {
            if (!IsActive || IsStaticNow) return;
            if (_state != EnemyState.Combat) EnterCombatState(now, false);
            SeekCover(now, false, true, false, CombatMode.Cover);
        }

        /// <summary>Greycoat pair partner died: "Vaun dret!" and retreat (DIALOGUE BK_GRY_08).</summary>
        public void OnPartnerDied(float now)
        {
            _partner = null;
            if (!IsActive) return;
            if (_archetype.Squelch) _barker.Bark(BarkIds.GryFallBack, transform, Team, now, 10f, true);
            if (_state == EnemyState.Combat && !IsStaticNow) SeekCover(now, false, true, false, CombatMode.Cover);
        }

        /// <summary>Sets the Greycoat pair partner (spawner).</summary>
        public void SetPartner(EnemyBrain partner) => _partner = partner;

        /// <summary>
        /// Scripted run (GDD §7.2 failure branch: the left sentry runs north shouting "Tovek!"): sprints to a point and
        /// then fights from cover there.
        /// </summary>
        public void RunTo(Vector3 destination, string bark)
        {
            if (!IsActive) return;
            var now = Time.time;
            if (!string.IsNullOrEmpty(bark)) _barker.Bark(bark, transform, Team, now, 5f, true);
            var player = _director?.Player;
            if (player != null) _perception.ForceAware(player, player.Root.position, now);
            if (_state != EnemyState.Combat)
            {
                _state = EnemyState.Combat;
                _combatSince = now;
                _director?.Combat.Set(_agentId, true);
                if (_context?.Def != null && _context.Def.Sentry) _actor.Health?.SetMaxKeepDamage(_archetype.Hp);
                if (_body != null && _body.IsActionPlaying) _body.PlayAction(AIActionIds.Stop);
                _firstContactDone = true;
                CombatEntered?.Invoke(this);
            }
            ReleaseCover();
            StartMove(destination, _archetype.SprintSpeed > 0f ? _archetype.SprintSpeed : 5f, CombatMode.Run, CombatMode.Cover, now);
        }

        /// <summary>Hides the enemy (B11 T3 behind his parapet): invisible, invulnerable, blind, no hitboxes.</summary>
        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            var h = _actor.Health;
            if (h != null) h.Invulnerable = hidden;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.IndexOf("Glint", StringComparison.Ordinal) < 0) r.enabled = !hidden;
            foreach (var c in GetComponentsInChildren<Hitbox>(true))
            {
                var col = c.GetComponent<Collider>();
                if (col != null) col.enabled = !hidden;
            }
        }

        private void SetPerchPose(bool perch) =>
            gameObject.SendMessage("SetPerch", perch, SendMessageOptions.DontRequireReceiver);

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            var agent = _body?.Agent;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(position);
            transform.SetPositionAndRotation(position, rotation);
        }

        // ------------------------------------------------------------------ reactions

        /// <summary>Damage taken (from <see cref="EnemyActor"/>): become aware of the attacker, pain bark, duck.</summary>
        public void OnDamaged(in DamageInfo d)
        {
            if (!IsActive || _hidden) return;
            var now = Time.time;
            if (d.Amount > 0f && _archetype.LegionBarks && _actor.IsAlive) _barker.Bark(BarkIds.LegPain, transform, Team, now, 3f);
            var attacker = AttackerOf(d);
            if (_state != EnemyState.Combat)
            {
                AlertTo(attacker, attacker?.Root != null ? attacker.Root.position : transform.position - d.Direction * 10f, now);
                return;
            }
            if (attacker != null && attacker.Root != null) _perception.ForceAware(attacker, attacker.Root.position, now);
            if (_mode == CombatMode.Peek && d.ByPlayer && _rng.Chance(_archetype.PrefersFullCover ? 0.3f : 0.6f)) StartHide(now);
        }

        private ITargetable AttackerOf(in DamageInfo d)
        {
            if (d.InstigatorTeam == Team.Player) return _director?.Player;
            if (d.Instigator == null) return null;
            return d.Instigator.GetComponentInParent<ITargetable>();
        }

        /// <summary>Squad fire suppressed this enemy (×0.5 rate and accuracy for 2 s, GDD §14.3): Legion duck.</summary>
        public void OnSuppressed(Team by)
        {
            if (!IsActive || _state != EnemyState.Combat) return;
            if (_mode == CombatMode.Peek && !_archetype.PrefersFullCover && _rng.Chance(0.5f)) StartHide(Time.time);
        }

        /// <summary>Death (from <see cref="EnemyActor"/>).</summary>
        public void OnDeath(in DamageInfo d)
        {
            if (_state == EnemyState.Dead) return;
            for (var i = 0; i < _behaviours.Length; i++) _behaviours[i].OnDeath(this, d);
            var drops = _context?.Def?.DropWeapon ?? _archetype.DropsWeapon;
            var hand = _body?.RightHand;
            if (drops) AICombat.SpawnPickup(WeaponId, hand != null && hand != transform ? hand.position : transform.position + Vector3.up * 1f);  // GDD §13.1
            if (_director != null && _agentId != 0) _director.Unregister(_agentId);
            _agentId = 0;
            _state = EnemyState.Dead;
            _mode = CombatMode.None;
            _hasToken = false;
            _cover = null;
            _perception?.Clear();
            if (_partner != null && _partner.IsActive) _partner.OnPartnerDied(Time.time);
            _partner = null;
        }

        private void OnNoise(NoiseEvent e)
        {
            if (!IsActive || _perception == null) return;
            var now = Time.time;
            var scale = _director != null ? _director.HearingScale : 1f;
            if (_perception.Hear(e, now, scale) && _state != EnemyState.Combat && _perception.AnyAware) EnterCombatState(now, true);
        }

        private void OnTriggerEntered(string id, Collider c) => OnNamedEvent(id);

        private void OnNamedEvent(string id)
        {
            if (!IsActive || string.IsNullOrEmpty(id)) return;
            var now = Time.time;
            if (_holding && _holdEvents != null)
            {
                for (var i = 0; i < _holdEvents.Length; i++)
                {
                    if (_holdEvents[i] != id) continue;
                    _holding = false;
                    AlertToPlayer(now, null);
                    break;
                }
            }
            if (_preferSquad && id == _preferSquadUntil)
            {
                _preferSquad = false;
                var player = _director?.Player;
                if (player != null) _perception.ForceAware(player, player.Root.position, now);
            }
        }

        // ------------------------------------------------------------------ helpers

        private bool ArrivedAt(Vector3 p, float distance = ArriveDistance)
        {
            var d = Planar(p - transform.position);
            if (d.sqrMagnitude <= distance * distance) return true;
            return _body != null && _body.HasArrived && Time.time - _moveStarted > 0.3f && d.sqrMagnitude <= 4f;
        }

        private static Vector3 Planar(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static Quaternion Facing(Vector3 from, Vector3 to)
        {
            var d = Planar(to - from);
            return d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
        }
    }
}
