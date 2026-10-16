using System;
using System.Collections.Generic;
using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// Hollis, Sparrow or Tinker (TECH_PLAN §7.7 SquadMember, GDD §15): identity (member id, callout prefix, speaker id),
    /// an invulnerable <see cref="Health"/> (enemy rounds are VFX only), an <see cref="ITargetable"/> of team Squad,
    /// perception under the shared ray budget, its gunfight (<see cref="SquadCombat"/>), head look-at
    /// (<see cref="SquadLookAt"/>), the "no blocking" yield (sidestep within 0.3 s when the player comes within 1 m; the
    /// root capsule is a trigger so he pushes through), scripted actions with their props (smoke, chemlight) and
    /// scripted kills. Movement orders come from <see cref="SquadService"/>; the body is any <see cref="ICharacterBody"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadMember : MonoBehaviour, ITargetable, IAITickable, IPerceptionOwner
    {
        /// <summary>Walk speed (m/s).</summary>
        public const float WalkSpeed = 2.2f;
        /// <summary>Run speed (m/s).</summary>
        public const float RunSpeed = 4.4f;
        /// <summary>Sprint speed, keeping up with a sprinting player (6.9 m/s, GDD §12).</summary>
        public const float SprintSpeed = 6.6f;
        /// <summary>The player within this distance makes a squadmate yield (GDD §15).</summary>
        public const float YieldRadius = 1f;
        /// <summary>Sidestep length (m).</summary>
        public const float YieldStep = 1.6f;
        /// <summary>Sidestep duration (s).</summary>
        public const float YieldSeconds = 0.7f;
        /// <summary>A scripted move that takes longer than this snaps (off-screen) to its mark.</summary>
        public const float ScriptedMoveTimeout = 15f;

        private static readonly List<SquadMember> s_all = new List<SquadMember>(3);

        [SerializeField, Tooltip("HOL / SPA / TIN (SquadIds).")] private string _memberId = SquadIds.Hollis;
        [SerializeField] private string _weaponId = WeaponIds.Commando;
        [SerializeField] private Chemlight _chemlight;

        private Health _health;
        private ICharacterBody _body;
        private Perception _perception;
        private SquadCombat _combat;
        private readonly SquadLookAt _look = new SquadLookAt();
        private AIDirector _director;
        private int _agentId;
        private AIRandom _rng;
        private ServiceRef<ITargetRegistry> _targets;
        private Vector3 _velocity;
        private Vector3 _lastPosition;
        private bool _subscribed;

        // orders
        private Vector3 _orderPosition;
        private Quaternion? _orderFacing;
        private float _orderSpeed;
        private Stance _orderStance = Stance.Stand;
        private int _orderPose;
        private bool _hasOrder;
        private bool _arrivedApplied;
        private float _yieldUntil;
        private float _nextAgentProbe;
        private readonly LinkTraversal _link = new LinkTraversal();

        // scripted
        private string _scriptAction;
        private Vector3? _scriptStand;
        private Vector3? _scriptFace;
        private Action _scriptDone;
        private Action _scriptEffect;
        private bool _scriptStarted;
        private bool _scriptEffectDone;
        private bool _scriptFinished;
        private float _scriptDeadline;
        private float _scriptMoveStarted;
        private bool _loopHold;

        /// <summary>Every enabled squad member.</summary>
        public static IReadOnlyList<SquadMember> All => s_all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_all.Clear();

        /// <summary>Member id (HOL / SPA / TIN).</summary>
        public string MemberId => _memberId;
        /// <summary>DIALOGUE.json speaker id (HOLLIS / SPARROW / TINKER).</summary>
        public string SpeakerId => SpeakerIds.FromSquadId(_memberId);
        /// <summary>Body.</summary>
        public ICharacterBody Body => _body;
        /// <summary>Perception (team Squad).</summary>
        public Perception Perception => _perception;
        /// <summary>Gunfight.</summary>
        public SquadCombat Combat => _combat;
        /// <summary>Look-at state.</summary>
        public SquadLookAt Look => _look;
        /// <summary>Head transform (look-at target for the others, VO speaker).</summary>
        public Transform Head => _body?.Head != null ? _body.Head : transform;
        /// <summary>The squad service that owns the member (may be null in test scenes).</summary>
        public SquadService Service { get; internal set; }
        /// <summary>Chemlight (created on demand).</summary>
        public Chemlight Chemlight => _chemlight;
        /// <summary>True while a scripted action (or a held loop) owns the member.</summary>
        public bool IsBusy => _scriptAction != null || _loopHold;
        /// <summary>True while sidestepping for the player.</summary>
        public bool IsYielding => Time.time < _yieldUntil;
        /// <summary>Last ordered destination.</summary>
        public Vector3 OrderPosition => _orderPosition;
        /// <summary>True when an order exists.</summary>
        public bool HasOrder => _hasOrder;

        /// <summary>Binds a chemlight (service, builders).</summary>
        public void SetChemlight(Chemlight chemlight) => _chemlight = chemlight;

        /// <summary>Sets identity (builders).</summary>
        public void Setup(string memberId, string weaponId, Chemlight chemlight)
        {
            _memberId = memberId;
            _weaponId = string.IsNullOrEmpty(weaponId) ? WeaponIds.Commando : weaponId;
            _chemlight = chemlight;
        }

        // ------------------------------------------------------------------ ITargetable / IPerceptionOwner

        /// <inheritdoc/>
        public string ActorId => _memberId;
        /// <inheritdoc/>
        public Transform Root => transform;
        /// <inheritdoc/>
        public Vector3 Velocity => _velocity;
        /// <inheritdoc/>
        public PerchTag Perch => PerchTag.Ground;
        /// <inheritdoc/>
        public bool SquadTarget { get => false; set { } }
        /// <inheritdoc/>
        public float LastEngagedByPlayer => float.NegativeInfinity;
        /// <inheritdoc/>
        public void NotifyEngagedByPlayer() { }
        /// <inheritdoc/>
        public Team Team => Team.Squad;
        /// <inheritdoc/>
        public bool IsAlive => isActiveAndEnabled;
        /// <inheritdoc/>
        public Transform AimPoint => _health != null ? _health.AimPoint : transform;
        /// <inheritdoc/>
        public void ApplyDamage(in DamageInfo d)
        {
            if (_health != null) _health.ApplyDamage(d);                                       // invulnerable: VFX and hit reactions only
        }
        /// <inheritdoc/>
        public Vector3 EyePosition => Head != transform ? Head.position : transform.position + Vector3.up * 1.6f;
        /// <inheritdoc/>
        public Vector3 Forward => transform.forward;
        /// <inheritdoc/>
        public float FixedSightRange => 0f;
        /// <inheritdoc/>
        public bool InCombat => _director != null && _director.Combat.InCombat;
        /// <inheritdoc/>
        public bool PerceptionEnabled => isActiveAndEnabled;
        /// <inheritdoc/>
        public void AttentionLimits(out float rangeCap, out float fovDegrees)
        {
            rangeCap = 0f;
            fovDegrees = 0f;
        }
        /// <inheritdoc/>
        public float DetectionTimeScale(ITargetable target, float distance) => 1f;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();
            _health.Invulnerable = true;                                                        // GDD §15: never a fail state
            _health.SetTeam(Team.Squad);
            _health.ActorId = _memberId;
            _body = GetComponent<ICharacterBody>();
            _combat = new SquadCombat(this);
            _rng = AIRandom.FromId(_memberId, 77u);
            // GDD §15 "push-through collision": the player walks through squadmates.
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null) capsule.isTrigger = true;
            if (_chemlight == null) _chemlight = GetComponentInChildren<Chemlight>(true);
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (!s_all.Contains(this)) s_all.Add(this);
            _director = AIDirector.Ensure();
            _perception = new Perception(this, _rng.Value);
            if (_director != null) _agentId = _director.Register(this, this, _perception, false);
            (_targets.Value ?? TargetRegistry.EnsureExists())?.Register(this);
            _body?.SetWeaponVisual(_weaponId);
            if (!_subscribed && _body != null)
            {
                _body.AnimEvent += OnAnimEvent;
                _subscribed = true;
            }
            GameEvents.Noise += OnNoise;
            SquadService.Instance?.Adopt(this);
        }

        private void OnDisable()
        {
            s_all.Remove(this);
            if (_director != null && _agentId != 0) _director.Unregister(_agentId);
            _agentId = 0;
            _targets.Value?.Unregister(this);
            if (_subscribed && _body != null)
            {
                _body.AnimEvent -= OnAnimEvent;
                _subscribed = false;
            }
            GameEvents.Noise -= OnNoise;
        }

        private void OnNoise(NoiseEvent e)
        {
            if (_perception == null) return;
            _perception.Hear(e, Time.time, _director != null ? _director.HearingScale : 1f);
        }

        // ------------------------------------------------------------------ orders (SquadService)

        /// <summary>
        /// Orders a move. Re-paths only when the destination changes by more than 0.5 m. On arrival the member takes
        /// <paramref name="stance"/> / <paramref name="coverPose"/> and turns to <paramref name="facing"/> if given.
        /// </summary>
        public void Order(Vector3 destination, float speed, Quaternion? facing, Stance stance, int coverPose)
        {
            var changed = !_hasOrder || (destination - _orderPosition).sqrMagnitude > 0.25f || Mathf.Abs(speed - _orderSpeed) > 0.5f;
            _orderFacing = facing;
            _orderStance = stance;
            _orderPose = coverPose;
            if (!changed) return;
            _hasOrder = true;
            _arrivedApplied = false;
            _orderPosition = destination;
            _orderSpeed = speed;
            if (IsBusy || IsYielding) return;
            _body?.SetCoverPose(0);
            _body?.SetStance(Stance.Stand);
            _body?.MoveTo(destination, speed);
        }

        /// <summary>Teleports (catch-up, checkpoint, node-set teleport) and keeps the position as the current order.</summary>
        public void Warp(Vector3 position, Quaternion rotation)
        {
            var agent = _body?.Agent;
            _body?.Stop();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(position);
            transform.SetPositionAndRotation(position, rotation);
            _lastPosition = position;
            _velocity = Vector3.zero;
            _orderPosition = position;
            _orderFacing = rotation;
            _hasOrder = true;
            _arrivedApplied = false;
            _combat?.Reset();
        }

        /// <summary>Ends a held loop (drill) so orders move the member again.</summary>
        public void ClearHold()
        {
            if (!_loopHold) return;
            _loopHold = false;
            _body?.PlayAction(AIActionIds.Stop);
        }

        /// <summary>True when within <paramref name="radius"/> m (planar) of a point and roughly on its level.</summary>
        public bool IsWithin(Vector3 point, float radius)
        {
            var d = point - transform.position;
            if (Mathf.Abs(d.y) > 2.5f) return false;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// Plays a scripted action (TECH_PLAN §7.7): walks to <paramref name="standAt"/> if given, turns toward
        /// <paramref name="faceAt"/>, plays the body action, runs <paramref name="effect"/> at its effect point (or at the
        /// end) and <paramref name="done"/> when it finishes. Loops (drill) report done once established and hold the
        /// member until <see cref="ClearHold"/> or the next order set.
        /// </summary>
        public void PlayScripted(string actionId, Vector3? standAt, Vector3? faceAt, Action effect, Action done)
        {
            CancelScripted(true);
            _loopHold = false;
            _scriptAction = actionId;
            _scriptStand = standAt;
            _scriptFace = faceAt;
            _scriptEffect = effect;
            _scriptDone = done;
            _scriptStarted = false;
            _scriptEffectDone = false;
            _scriptFinished = false;
            _scriptMoveStarted = Time.time;
            _scriptDeadline = float.PositiveInfinity;
            if (standAt.HasValue) _body?.MoveTo(standAt.Value, RunSpeed);
        }

        private void CancelScripted(bool complete)
        {
            if (_scriptAction == null) return;
            var done = _scriptDone;
            _scriptAction = null;
            _scriptDone = null;
            _scriptEffect = null;
            if (complete && !_scriptFinished) done?.Invoke();
        }

        /// <summary>
        /// Scripted lethal shot (GDD §7.2 "Mark", §7.5 R3 nest, §7.10 RPG): marks the target as a squad target, fires one
        /// sure round through <see cref="AICombat"/> and makes certain the target dies.
        /// </summary>
        public void ScriptedKill(ITargetable target)
        {
            if (target == null || !target.IsAlive) return;
            target.SquadTarget = true;
            var aim = Perception.AimOf(target);
            _body?.SetAim(aim);
            var muzzle = _body?.Muzzle != null ? _body.Muzzle.position : EyePosition;
            AICombat.Fire(new AIShotRequest
            {
                Shooter = this,
                Muzzle = muzzle,
                Target = target,
                HitChance = 1f,
                Damage = 1000f,
                WeaponId = _weaponId,
                Team = Team.Squad,
                TracerEvery = 1,
                SuppressOnly = false,
            });
            if (target.IsAlive && target is Component c)
            {
                var h = c.GetComponent<Health>();
                if (h != null) h.Kill(new DamageInfo(1000f, DamageType.Bullet, HitZone.Head, aim, (aim - muzzle).normalized, gameObject, Team.Squad, _weaponId));
            }
        }

        // ------------------------------------------------------------------ tick

        /// <summary>
        /// The prefab ships with its agent disabled (S_Persistent has no NavMesh): it is enabled once the member stands
        /// within 2 m of a NavMesh (after a section loads), so Unity never warns about off-mesh agents.
        /// </summary>
        private void EnsureAgent(float now)
        {
            var agent = _body?.Agent;
            if (agent == null || agent.enabled || now < _nextAgentProbe) return;
            _nextAgentProbe = now + 0.5f;
            if (!NavUtil.Sample(transform.position, 2f, out var p)) return;
            transform.position = p;
            agent.enabled = true;
            LinkTraversal.Prepare(agent);
            if (agent.isOnNavMesh) agent.Warp(p);
            if (_hasOrder && !IsBusy) _body.MoveTo(_orderPosition, _orderSpeed > 0f ? _orderSpeed : WalkSpeed);
        }

        /// <inheritdoc/>
        public void AITick(float now, float dt)
        {
            EnsureAgent(now);
            if (_link.Tick(_body?.Agent, _body, dt)) return;                                 // crossing a NavMeshLink (vault, climb, ladder)
            if (dt > 0f)
            {
                var agent = _body?.Agent;
                _velocity = NavUtil.Usable(agent) ? agent.velocity : (transform.position - _lastPosition) / dt;
                _lastPosition = transform.position;
            }
            TickYield(now);
            if (_scriptAction != null) TickScripted(now);
            else if (_hasOrder && !IsYielding && !_loopHold) TickArrival();
            var service = Service;
            var player = _director?.Player;
            var rig = _director?.Rig;
            var running = _velocity.sqrMagnitude > RunSpeed * RunSpeed * 0.8f;
            _combat.Tick(now, !running && !IsYielding && !IsBusy, ref _rng);
            if (_scriptAction == null && _combat.Target != null && !running) _body?.SetAim(Perception.AimOf(_combat.Target));
            else if (_scriptAction == null && _combat.Target == null) _body?.SetAim(null);
            var playerHead = rig?.Eye != null ? rig.Eye : player?.AimPoint;
            _look.Tick(_body, transform, service?.LookOverride, service?.LookOverrideWeight ?? 0f, SpeakerFor(service), playerHead, _combat.Target);
        }

        private Transform SpeakerFor(SquadService service)
        {
            var speaker = service?.CurrentSpeaker;
            if (speaker == null) return null;
            if (speaker == Head || speaker == transform) return null;                            // the speaker himself looks ahead
            return speaker;
        }

        private void TickArrival()
        {
            if (_arrivedApplied || !IsWithin(_orderPosition, 0.6f) && !(_body != null && _body.HasArrived && IsWithin(_orderPosition, 1.5f))) return;
            _arrivedApplied = true;
            _body?.Stop();
            _body?.SetStance(_orderStance);
            _body?.SetCoverPose(_orderPose);
            if (_orderFacing.HasValue && _combat.Target == null) transform.rotation = _orderFacing.Value;
        }

        private void TickYield(float now)
        {
            if (IsBusy) return;
            var player = _director?.Player;
            if (player?.Root == null) return;
            var d = transform.position - player.Root.position;
            if (Mathf.Abs(d.y) > 1.5f) return;
            d.y = 0f;
            if (d.sqrMagnitude > YieldRadius * YieldRadius || now < _yieldUntil) return;
            // GDD §15: sidestep within 0.3 s, away from the player's path.
            var pv = player.Velocity;
            pv.y = 0f;
            var side = pv.sqrMagnitude > 0.04f ? new Vector3(pv.z, 0f, -pv.x).normalized : new Vector3(d.z, 0f, -d.x).normalized;
            if (Vector3.Dot(side, d) < 0f) side = -side;
            var allowDoorway = Service != null && Service.CurrentSetAllowsDoorway;
            var dest = NavUtil.SampleStopPoint(transform.position + side * YieldStep + d.normalized * 0.4f, 1.5f, allowDoorway);
            _yieldUntil = now + YieldSeconds;
            _arrivedApplied = false;
            _body?.SetCoverPose(0);
            _body?.SetStance(Stance.Stand);
            _body?.MoveTo(dest, RunSpeed);
            Service?.OnMemberYielded(this);
        }

        private void TickScripted(float now)
        {
            if (!_scriptStarted)
            {
                if (_scriptStand.HasValue && !IsWithin(_scriptStand.Value, 0.5f))
                {
                    if (now - _scriptMoveStarted < ScriptedMoveTimeout)
                    {
                        if (!IsYielding && _body != null && _body.HasArrived) _body.MoveTo(_scriptStand.Value, RunSpeed);
                        return;
                    }
                    if (!NavUtil.IsOffScreen(_director?.Rig?.WorldCamera, Head.position)) return;
                    Warp(_scriptStand.Value, transform.rotation);
                }
                _body?.Stop();
                _body?.SetCoverPose(0);
                _body?.SetStance(Stance.Stand);
                _body?.SetAim(null);
                if (_scriptFace.HasValue)
                {
                    var f = _scriptFace.Value - transform.position;
                    f.y = 0f;
                    if (f.sqrMagnitude > 1e-3f) transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
                }
                _scriptStarted = true;
                var timeout = ActionDurations.Timeout(_scriptAction);
                _scriptDeadline = timeout > 0f ? now + timeout : float.PositiveInfinity;
                if (ActionDurations.Of(_scriptAction) < 0f) _loopHold = true;
                if (_body != null) _body.PlayAction(_scriptAction, OnScriptedDone);
                else OnScriptedDone();
                return;
            }
            if (now >= _scriptDeadline) OnScriptedDone();
        }

        private void OnAnimEvent(string id)
        {
            if (_scriptAction == null || !_scriptStarted) return;
            if (id == AnimEventIds.ActionHit || id == AnimEventIds.ThrowRelease) RunEffect();
        }

        private void RunEffect()
        {
            if (_scriptEffectDone) return;
            _scriptEffectDone = true;
            _scriptEffect?.Invoke();
        }

        private void OnScriptedDone()
        {
            if (_scriptFinished || _scriptAction == null) return;
            _scriptFinished = true;
            RunEffect();
            var done = _scriptDone;
            _scriptAction = null;
            _scriptDone = null;
            _scriptEffect = null;
            _arrivedApplied = false;
            done?.Invoke();
        }
    }
}
