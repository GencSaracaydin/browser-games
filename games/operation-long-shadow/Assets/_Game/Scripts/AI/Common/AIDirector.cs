using System.Collections.Generic;
using System.Diagnostics;
using LongShadow.Core;
using Unity.Profiling;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Shared AI state and the central tick (TECH_PLAN §7.6, §7.16): ticks every brain and squad member once per frame,
    /// runs perception round-robin under the 24-ray <see cref="LosBudget"/>, owns the attack tokens (GDD §14.3, §16),
    /// cover claims, the shared Legion grenade pool, the combat-state edge (<see cref="GameEvents.CombatStateChanged"/>),
    /// player suppression (near misses within 2 m, 1 s) and "the enemy under the crosshair". Lives on PF_AIServices in
    /// S_Persistent; <see cref="Ensure"/> creates one at runtime in test scenes. Measures its own cost (ms per frame).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.AI)]
    [DisallowMultipleComponent]
    public sealed class AIDirector : MonoBehaviour
    {
        /// <summary>Near misses within this distance suppress the player (GDD §14.3).</summary>
        public const float SuppressionRadius = 2f;
        /// <summary>Seconds of player suppression per near miss (GDD §14.3).</summary>
        public const float SuppressionSeconds = 1f;
        /// <summary>The player counts as "in the same cover" while within this distance of an anchor.</summary>
        public const float StaticRadius = 1.5f;
        /// <summary>Crosshair cone (degrees) for "the enemy the player is aiming at" (GDD §14.3 tokens).</summary>
        public const float AimConeDegrees = 3.5f;
        /// <summary>
        /// Owner-id prefix of the Beat 6 floodlight zones (B6.F1-F4): standing in one gives enemies the ×1.25 accuracy
        /// bonus (GDD §7.6). Flares and headlights only extend sight range.
        /// </summary>
        public const string FloodOwnerPrefix = "B6.F";

        private static AIDirector s_instance;

        private sealed class Agent
        {
            public int Id;
            public ITargetable Actor;
            public IAITickable Tick;
            public IPerceptionAgent Perception;
            public bool Enemy;
            public bool Removed;
        }

        private static readonly ProfilerMarker s_tickMarker = new ProfilerMarker("LS.AI.Tick");
        private static readonly ProfilerMarker s_perceptionMarker = new ProfilerMarker("LS.AI.Perception");

        private readonly List<Agent> _agents = new List<Agent>(32);
        private readonly List<CoverNode> _coverNodes = new List<CoverNode>(128);
        private readonly LosBudget _budget = new LosBudget();
        private readonly PerceptionScheduler _scheduler = new PerceptionScheduler();
        private readonly AttackTokenManager _tokens = new AttackTokenManager();
        private readonly CoverClaims _claims = new CoverClaims();
        private readonly LegionGrenadePool _legionGrenades = new LegionGrenadePool();
        private readonly CombatStateTracker _combat = new CombatStateTracker();
        private readonly Stopwatch _watch = new Stopwatch();

        private ServiceRef<ISettingsService> _settings;
        private ServiceRef<ILightStage> _lightStage;
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<ITargetRegistry> _targets;
        private int _nextId = 1;
        private bool _iterating;
        private bool _agentsDirty;
        private float _playerSuppressedUntil = float.NegativeInfinity;
        private Vector3 _staticAnchor;
        private float _staticSince;
        private bool _staticValid;
        private int _aimedAgent;
        private float _coverRefreshAt;
        private float _litRefreshAt;
        private DifficultyProfile _difficulty = DifficultyProfile.For(LongShadow.Core.Difficulty.Regular);
        private bool _difficultyDirty = true;

        /// <summary>The director, or null (edit mode / not created yet).</summary>
        public static AIDirector Instance => s_instance;

        /// <summary>Per-frame raycast budget (24, TECH_PLAN §7.6).</summary>
        public LosBudget Budget => _budget;
        /// <summary>Attack tokens (capacity follows the difficulty).</summary>
        public AttackTokenManager Tokens => _tokens;
        /// <summary>Cover claims.</summary>
        public CoverClaims Claims => _claims;
        /// <summary>Shared Legion grenade pool.</summary>
        public LegionGrenadePool LegionGrenades => _legionGrenades;
        /// <summary>Combat-state tracker.</summary>
        public CombatStateTracker Combat => _combat;
        /// <summary>Current difficulty profile (GDD §16).</summary>
        public DifficultyProfile Difficulty
        {
            get
            {
                RefreshDifficulty();
                return _difficulty;
            }
        }
        /// <summary>Agent id of the enemy under the player's crosshair (0 = none).</summary>
        public int AimedAgent => _aimedAgent;
        /// <summary>Average AI cost in ms (exponential moving average).</summary>
        public float AverageMs { get; private set; }
        /// <summary>Highest AI cost in one frame (ms) since the last <see cref="ResetStats"/>.</summary>
        public float PeakMs { get; private set; }
        /// <summary>Registered agents.</summary>
        public int AgentCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _agents.Count; i++)
                    if (!_agents[i].Removed) n++;
                return n;
            }
        }
        /// <summary>The player rig (may be null).</summary>
        public IPlayerRig Rig => _rig.Value;
        /// <summary>The player as a targetable (may be null).</summary>
        public ITargetable Player => _targets.Value?.Player;
        /// <summary>The target registry (may be null).</summary>
        public ITargetRegistry Targets => _targets.Value;

        /// <summary>Stage sight range (GDD §14.2; 70 m when no ILightStage).</summary>
        public float StageSightRange
        {
            get
            {
                var ls = _lightStage.Value;
                if (ls == null) return PerceptionMath.GoldenRange;
                var r = ls.AISightRange;
                return r > 0f ? r : PerceptionMath.StageRange(ls.Stage);
            }
        }

        /// <summary>Hearing scale from the LightStage (1 when none).</summary>
        public float HearingScale
        {
            get
            {
                var ls = _lightStage.Value;
                return ls != null && ls.HearingScale > 0f ? ls.HearingScale : 1f;
            }
        }

        /// <summary>True while the player stands in an active IlluminationZone (sight ×2 up to 70 m, GDD §14.2).</summary>
        public bool PlayerLit { get; private set; }

        /// <summary>True while the player stands in an active flood cone (accuracy ×1.25, GDD §7.6).</summary>
        public bool PlayerInFlood { get; private set; }

        /// <summary>LightStage s (0 when none).</summary>
        public float StageS => _lightStage.Value?.S ?? 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        /// <summary>Returns the director, creating one (and a TargetRegistry) at runtime when missing.</summary>
        public static AIDirector Ensure()
        {
            if (s_instance != null) return s_instance;
            if (!Application.isPlaying) return null;
            TargetRegistry.EnsureExists();
            var go = new GameObject("[AIDirector]");
            s_instance = go.AddComponent<AIDirector>();
            LSLog.WarnOnce("ai-autocreate-director", LogCategory.AI,
                "No AIDirector in the scene; created one (S_Persistent should contain PF_AIServices).");
            return s_instance;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                LSLog.Warn(LogCategory.AI, "Second AIDirector destroyed", this);
                Destroy(this);
                return;
            }
            s_instance = this;
            TargetRegistry.EnsureExists();
            _combat.Changed += GameEvents.RaiseCombatStateChanged;
        }

        private void OnEnable()
        {
            GameEvents.NearMiss += OnNearMiss;
            GameEvents.ShotFired += OnShotFired;
            GameEvents.SettingsChanged += OnSettingsChanged;
            GameEvents.SectionLoaded += OnSectionChanged;
            GameEvents.SectionUnloaded += OnSectionChanged;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.NearMiss -= OnNearMiss;
            GameEvents.ShotFired -= OnShotFired;
            GameEvents.SettingsChanged -= OnSettingsChanged;
            GameEvents.SectionLoaded -= OnSectionChanged;
            GameEvents.SectionUnloaded -= OnSectionChanged;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
        }

        private void OnDestroy()
        {
            _combat.Changed -= GameEvents.RaiseCombatStateChanged;
            if (s_instance == this) s_instance = null;
        }

        /// <summary>Registers an agent; returns its id (never 0).</summary>
        public int Register(ITargetable actor, IAITickable tick, IPerceptionAgent perception, bool enemy)
        {
            var a = new Agent { Id = _nextId++, Actor = actor, Tick = tick, Perception = perception, Enemy = enemy };
            _agents.Add(a);
            if (perception != null) _scheduler.Add(perception);
            return a.Id;
        }

        /// <summary>Unregisters an agent and drops its token, cover claim and combat flag.</summary>
        public void Unregister(int id)
        {
            for (var i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a.Id != id || a.Removed) continue;
                if (a.Perception != null) _scheduler.Remove(a.Perception);
                a.Removed = true;
                a.Tick = null;
                a.Actor = null;
                _agentsDirty = true;
                break;
            }
            if (!_iterating) CompactAgents();
            _tokens.Release(id);
            _claims.Release(id);
            _combat.Set(id, false);
            if (_aimedAgent == id) _aimedAgent = 0;
        }

        /// <summary>Consumes rays from this frame's budget (cover tests, closet checks, in-cover tests).</summary>
        public bool TryUseRays(int rays = 1) => _budget.TryUse(Time.frameCount, rays);

        /// <summary>True while the player is suppressed (near misses within 2 m in the last second).</summary>
        public bool PlayerSuppressed(float now) => now < _playerSuppressedUntil;

        /// <summary>Seconds the player has stayed within 1.5 m of one spot (Greycoat grenade trigger, GDD §14.1).</summary>
        public float PlayerStaticSeconds(float now) => _staticValid ? now - _staticSince : 0f;

        /// <summary>Every registered cover node (refreshed on section loads and every 3 s).</summary>
        public List<CoverNode> CoverNodes => _coverNodes;

        /// <summary>Resets the cost statistics.</summary>
        public void ResetStats()
        {
            AverageMs = 0f;
            PeakMs = 0f;
        }

        private void Update()
        {
            var now = Time.time;
            var dt = Time.deltaTime;
            _watch.Restart();
            RefreshDifficulty();
            TrackPlayer(now);
            if (now >= _coverRefreshAt) RefreshCoverNodes(now);
            using (s_perceptionMarker.Auto())
                _scheduler.Run(_budget, Time.frameCount, now);
            using (s_tickMarker.Auto())
            {
                _iterating = true;
                for (var i = 0; i < _agents.Count; i++)
                {
                    var a = _agents[i];
                    if (a.Removed || a.Tick == null) continue;
                    try
                    {
                        a.Tick.AITick(now, dt);
                    }
                    catch (System.Exception e)
                    {
                        LSLog.Exception(LogCategory.AI, e, this);
                    }
                }
                _iterating = false;
                CompactAgents();
            }
            _tokens.Tick(now);
            _watch.Stop();
            var ms = (float)_watch.Elapsed.TotalMilliseconds;
            AverageMs = AverageMs <= 0f ? ms : Mathf.Lerp(AverageMs, ms, 0.05f);
            if (ms > PeakMs) PeakMs = ms;
        }

        private void TrackPlayer(float now)
        {
            var player = Player;
            if (player == null || player.Root == null)
            {
                _staticValid = false;
                _aimedAgent = 0;
                PlayerLit = false;
                PlayerInFlood = false;
                return;
            }
            var p = player.Root.position;
            if (!_staticValid || (p - _staticAnchor).sqrMagnitude > StaticRadius * StaticRadius)
            {
                _staticAnchor = p;
                _staticSince = now;
                _staticValid = true;
            }
            _aimedAgent = FindAimedAgent();
            if (now >= _litRefreshAt)
            {
                _litRefreshAt = now + 0.2f;
                var aim = player.AimPoint != null ? player.AimPoint.position : p + Vector3.up * 1.3f;
                PlayerLit = IlluminationZone.IsLit(aim, out var zone);
                PlayerInFlood = PlayerLit && zone != null && zone.OwnerId != null &&
                                zone.OwnerId.StartsWith(FloodOwnerPrefix, System.StringComparison.Ordinal);
            }
        }

        private int FindAimedAgent()
        {
            var rig = _rig.Value;
            if (rig == null) return 0;
            var ray = rig.ViewRay;
            var best = 0;
            var bestAngle = 180f;
            for (var i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a.Removed || !a.Enemy || a.Actor == null || !a.Actor.IsAlive) continue;
                var aim = a.Actor.AimPoint;
                if (aim == null) continue;
                var to = aim.position - ray.origin;
                var d = to.magnitude;
                if (d < 0.5f || d > 160f) continue;
                // The cone widens up close so the whole body (about 0.35 m either side of the chest) counts.
                var allowed = Mathf.Max(AimConeDegrees, Mathf.Atan2(0.35f, d) * Mathf.Rad2Deg);
                var angle = Vector3.Angle(ray.direction, to);
                if (angle <= allowed && angle < bestAngle)
                {
                    bestAngle = angle;
                    best = a.Id;
                }
            }
            return best;
        }

        private void CompactAgents()
        {
            if (!_agentsDirty) return;
            _agentsDirty = false;
            for (var i = _agents.Count - 1; i >= 0; i--)
                if (_agents[i].Removed) _agents.RemoveAt(i);
        }

        private void RefreshCoverNodes(float now)
        {
            _coverRefreshAt = now + 3f;
            MarkerRegistry.Instance.GetAll(_coverNodes);
        }

        private void RefreshDifficulty()
        {
            if (!_difficultyDirty && Time.frameCount % 120 != 0) return;
            _difficultyDirty = false;
            var s = _settings.Value;
            _difficulty = s != null ? s.Difficulty : DifficultyProfile.For(LongShadow.Core.Difficulty.Regular);
            if (_difficulty.AttackTokens <= 0) _difficulty = DifficultyProfile.For(LongShadow.Core.Difficulty.Regular);
            _tokens.Capacity = _difficulty.AttackTokens;
            _legionGrenades.Interval = _difficulty.LegionGrenadeInterval > 0f ? _difficulty.LegionGrenadeInterval : 60f;
        }

        private void OnNearMiss(NearMissEvent e)
        {
            if (e.Distance <= SuppressionRadius) _playerSuppressedUntil = Time.time + SuppressionSeconds;
        }

        private void OnSettingsChanged() => _difficultyDirty = true;

        /// <summary>
        /// A player shot at the enemy under his crosshair counts as engaging it even when it misses (kill-share rule,
        /// GDD §15: the squad leaves the player's current target alone for 8 s).
        /// </summary>
        private void OnShotFired(ShotEvent s)
        {
            if (!s.IsPlayer || _aimedAgent == 0) return;
            for (var i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a.Id != _aimedAgent || a.Removed) continue;
                a.Actor?.NotifyEngagedByPlayer();
                return;
            }
        }

        private void OnSectionChanged(SectionId _) => _coverRefreshAt = 0f;

        private void OnCheckpointLoaded(string _)
        {
            _tokens.Clear();
            _claims.Clear();
            _legionGrenades.Reset(Time.time);
            _playerSuppressedUntil = float.NegativeInfinity;
        }
    }
}
