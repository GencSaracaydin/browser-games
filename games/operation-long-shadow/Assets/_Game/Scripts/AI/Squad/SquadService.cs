using System.Collections.Generic;
using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Squad
{
    /// <summary>
    /// <see cref="ISquadService"/> (TECH_PLAN §7.7, GDD §15): owns Hollis, Sparrow and Tinker. Node-set execution (each
    /// member to its slot of a <see cref="SquadNodeSet"/>, arrived within 1.5 m, picking crouch/stand/lean from the cover
    /// at the node; a set whose section is not loaded yet is waited for, which is how moves across a seam wait at the
    /// seam), follow mode along the critical path (<see cref="SquadFollowPlanner"/>), never stopping in Doorway areas
    /// unless the set allows it, the catch-up teleport (more than 25 m behind and off-screen for 2 s → the nearest valid
    /// point behind the player, also off-screen), scripted actions with their props, scripted kills, chemlights, the
    /// look-at override and speaker tracking, squad frags (1 per member per beat at clusters of 2+ away from the player,
    /// 30 s cooldown), and the callouts (<see cref="SquadCallouts"/>). <see cref="ISaveable"/> "Squad".
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Squad)]
    [DisallowMultipleComponent]
    public sealed class SquadService : MonoBehaviour, ISquadService, ISaveable
    {
        /// <summary>Order update interval.</summary>
        public const float OrderInterval = 0.25f;
        /// <summary>Node arrival radius (GDD §15).</summary>
        public const float NodeArriveRadius = 1.5f;
        /// <summary>Catch-up distance (GDD §15).</summary>
        public const float CatchUpDistance = 25f;
        /// <summary>Off-screen time before a catch-up teleport (GDD §15).</summary>
        public const float CatchUpOffscreenSeconds = 2f;
        /// <summary>Squad frag cooldown (GDD §15).</summary>
        public const float FragCooldown = 30f;
        /// <summary>Closest squad throw (m).</summary>
        public const float FragRangeMin = 8f;
        /// <summary>Farthest squad throw (m, GDD §13.2 throw range).</summary>
        public const float FragRangeMax = 18f;
        /// <summary>Enemies within this distance of each other form a cluster.</summary>
        public const float ClusterRadius = 4f;
        /// <summary>A squad frag never lands closer than this to the player.</summary>
        public const float FragPlayerClearance = 10f;
        /// <summary>Follow nag: player idle this long with a FOLLOW marker (DIALOGUE CO_HOL_11).</summary>
        public const float FollowIdleSeconds = 20f;
        /// <summary>Follow nag: player this far from Hollis (DIALOGUE CO_HOL_12).</summary>
        public const float FollowBehindDistance = 30f;
        /// <summary>Enemy frags within this distance of the player get a "Grenade!" (DIALOGUE CO_*_07/08).</summary>
        public const float GrenadeWarnRadius = 6f;
        /// <summary>Save key.</summary>
        public const string Key = "Squad";

        /// <summary>High-priority targets whose death by the player earns "Good kill." (GDD §7.12 HighPriority markers).</summary>
        public static readonly string[] HighPriorityTargets = { "E4-01", "E10-07" };

        private static SquadService s_instance;

        private readonly SquadCallouts _callouts = new SquadCallouts();
        private readonly Dictionary<SquadMember, float> _offscreenSince = new Dictionary<SquadMember, float>();
        private readonly HashSet<string> _fragUsed = new HashSet<string>();
        private readonly List<CoverNode> _cover = new List<CoverNode>(64);
        private ServiceRef<ICriticalPath> _path;
        private ServiceRef<IMissionDirector> _mission;
        private ServiceRef<IPlayerHealth> _health;
        private ServiceRef<IObjectiveService> _objectives;
        private ServiceRef<ICheckpointService> _checkpoints;

        private SquadMode _mode = SquadMode.Follow;
        private string _setId;
        private SquadNodeSet _set;
        private bool _chemlights;
        private Transform _lookOverride;
        private float _lookWeight;
        private Transform _speaker;
        private string _speakerLine;
        private float _nextOrders;
        private float _nextFrag;
        private int _pathBeat = -1;
        private float _pathLength;
        private Vector3 _idleAnchor;
        private float _idleSince;
        private float _nextNag;
        private bool _wasLow;
        private float _nextCloseCall;
        private bool _registeredSave;
        private bool _waitingLogged;

        /// <summary>The service, or null.</summary>
        public static SquadService Instance => s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        /// <summary>Follow or node set.</summary>
        public SquadMode Mode => _mode;
        /// <summary>Current node set id (null in follow mode).</summary>
        public string CurrentSet => _setId;
        /// <summary>True when the current set may stop in Doorway areas (B8 reel).</summary>
        public bool CurrentSetAllowsDoorway => _mode == SquadMode.NodeSet && _set != null && _set.AllowDoorway;
        /// <summary>Look-at override (MID-A).</summary>
        public Transform LookOverride => _lookOverride;
        /// <summary>Look-at override weight.</summary>
        public float LookOverrideWeight => _lookWeight;
        /// <summary>Head of whoever is speaking now (squad member or the player), else null.</summary>
        public Transform CurrentSpeaker => _speaker;
        /// <summary>Callouts.</summary>
        public SquadCallouts Callouts => _callouts;
        /// <summary>Chemlights on.</summary>
        public bool ChemlightsOn => _chemlights;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                LSLog.Warn(LogCategory.Squad, "Second SquadService destroyed", this);
                Destroy(this);
                return;
            }
            s_instance = this;
            GameServices.Register<ISquadService>(this);
            AIDirector.Ensure();
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++) Adopt(all[i]);
            foreach (var m in GetComponentsInChildren<SquadMember>(true)) Adopt(m);
        }

        private void OnDestroy()
        {
            if (s_instance != this) return;
            s_instance = null;
            GameServices.Unregister<ISquadService>(this);
            _checkpoints.Value?.Unregister(this);
        }

        private void OnEnable()
        {
            GameEvents.VOLineStarted += OnLineStarted;
            GameEvents.VOLineFinished += OnLineFinished;
            GameEvents.GrenadeLanded += OnGrenadeLanded;
            GameEvents.ActorKilled += OnActorKilled;
            GameEvents.BeatStarted += OnBeatStarted;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.VOLineStarted -= OnLineStarted;
            GameEvents.VOLineFinished -= OnLineFinished;
            GameEvents.GrenadeLanded -= OnGrenadeLanded;
            GameEvents.ActorKilled -= OnActorKilled;
            GameEvents.BeatStarted -= OnBeatStarted;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
        }

        /// <summary>Takes a member under this service (members call it on enable).</summary>
        public void Adopt(SquadMember member)
        {
            if (member == null) return;
            member.Service = this;
            if (_chemlights) EnsureChemlight(member).SetOn(true);
        }

        /// <summary>A member by id, or null.</summary>
        public SquadMember Find(string id)
        {
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].MemberId == id) return all[i];
            return null;
        }

        private void Update()
        {
            if (!_registeredSave && _checkpoints.Value != null)
            {
                _checkpoints.Value.Register(this);
                _registeredSave = true;
            }
            var now = Time.time;
            if (now < _nextOrders) return;
            _nextOrders = now + OrderInterval;
            var director = AIDirector.Instance;
            var player = director?.Player;
            var rig = director?.Rig;
            var view = ViewForward(rig, player);
            if (_mode == SquadMode.NodeSet) UpdateNodeSet(director);
            else UpdateFollow(player, rig, view, now);
            if (player?.Root != null)
            {
                CatchUp(player, rig, view, now);
                _callouts.TickSpotting(SquadMember.All, player.Root.position, view, now);
                UpdateNags(player, now);
            }
            if (director != null && director.Combat.InCombat && now >= _nextFrag) ConsiderFrag(player, now);
        }

        private static Vector3 ViewForward(IPlayerRig rig, ITargetable player)
        {
            if (rig != null)
            {
                var d = rig.ViewRay.direction;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) return d.normalized;
            }
            if (player?.Root != null) return player.Root.forward;
            return Vector3.forward;
        }

        // ------------------------------------------------------------------ node sets

        private void UpdateNodeSet(AIDirector director)
        {
            if (_set == null && !MarkerRegistry.Instance.TryGet(_setId, out _set))
            {
                // The set's section is not loaded yet (seam): hold position until it registers (GDD §15).
                if (!_waitingLogged)
                {
                    _waitingLogged = true;
                    LSLog.Verbose(LogCategory.Squad, "Squad waiting for node set " + _setId + " to register");
                }
                return;
            }
            var combat = director != null && director.Combat.InCombat;
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null || m.IsBusy) continue;
                var pos = _set.PositionOf(m.MemberId);
                var dest = NavUtil.SampleStopPoint(pos, NodeArriveRadius, _set.AllowDoorway);
                PoseAt(_set.Get(m.MemberId), pos, combat, out var stance, out var pose);
                var distance = (dest - m.transform.position).magnitude;
                var speed = combat || distance > 8f ? SquadMember.RunSpeed : SquadMember.WalkSpeed;
                m.Order(dest, speed, _set.RotationOf(m.MemberId), stance, pose);
            }
        }

        /// <summary>
        /// GDD §15: "Within the node radius (1.5 m) each squadmate picks a cover pose: crouch, stand or lean". An authored
        /// pose wins; else a cover node within 1.5 m decides (full cover: stand pose, half cover: crouch), else crouch in
        /// combat and stand out of it.
        /// </summary>
        private void PoseAt(SquadNode node, Vector3 position, bool combat, out Stance stance, out int pose)
        {
            if (node.IsValid && node.CoverPose != 0)
            {
                stance = node.Stance == Stance.Crouch ? Stance.Crouch : Stance.Stand;
                pose = node.CoverPose;
                return;
            }
            if (node.IsValid && node.Stance == Stance.Crouch)
            {
                stance = Stance.Crouch;
                pose = combat ? 1 : 0;
                return;
            }
            var director = AIDirector.Instance;
            if (director != null)
            {
                var nodes = director.CoverNodes;
                for (var i = 0; i < nodes.Count; i++)
                {
                    var c = nodes[i];
                    if (c == null || (c.Position - position).sqrMagnitude > NodeArriveRadius * NodeArriveRadius) continue;
                    stance = c.Standing ? Stance.Stand : Stance.Crouch;
                    pose = combat ? (c.Standing ? 2 : 1) : 0;
                    return;
                }
            }
            stance = combat ? Stance.Crouch : Stance.Stand;
            pose = 0;
        }

        // ------------------------------------------------------------------ follow

        private void UpdateFollow(ITargetable player, IPlayerRig rig, Vector3 view, float now)
        {
            if (player?.Root == null) return;
            var playerPos = player.Root.position;
            var path = _path.Value;
            var beat = _mission.Value?.Beat ?? 0;
            if (path != null && beat > 0 && beat != _pathBeat)
            {
                _pathBeat = beat;
                _pathLength = SquadFollowPlanner.PathLength(path, beat);
            }
            var usePath = path != null && beat > 0 && _pathLength > 1f;
            var playerSpeed = new Vector2(player.Velocity.x, player.Velocity.z).magnitude;
            var crouched = rig != null && rig.Stance == Stance.Crouch;
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null || m.IsBusy) continue;
                var index = SquadFollowPlanner.IndexOf(m.MemberId);
                if (index < 0) continue;
                var along = SquadFollowPlanner.Along[index];
                var lateral = SquadFollowPlanner.Lateral[index];
                var slot = usePath
                    ? SquadFollowPlanner.SlotOnPath(path, beat, _pathLength, playerPos, along, lateral)
                    : SquadFollowPlanner.SlotFromView(playerPos, view, along, lateral);
                if (m.HasOrder && !SquadFollowPlanner.NeedsRepath(index, m.transform.position, slot, playerPos)) continue;
                var dest = NavUtil.SampleStopPoint(slot, 3f, false);                            // never stop in a doorway
                var distance = (dest - m.transform.position).magnitude;
                var speed = playerSpeed > 5f ? SquadMember.SprintSpeed : distance > 6f || playerSpeed > 3f ? SquadMember.RunSpeed : SquadMember.WalkSpeed;
                m.Order(dest, speed, null, crouched ? Stance.Crouch : Stance.Stand, 0);
            }
        }

        // ------------------------------------------------------------------ catch-up

        private void CatchUp(ITargetable player, IPlayerRig rig, Vector3 view, float now)
        {
            var cam = rig?.WorldCamera;
            var playerPos = player.Root.position;
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null || m.IsBusy) continue;
                var d = m.transform.position - playerPos;
                var behind = Vector3.Dot(new Vector3(d.x, 0f, d.z), view) < 0f;
                var far = new Vector2(d.x, d.z).magnitude > CatchUpDistance;
                if (!far || !behind || !NavUtil.IsOffScreen(cam, m.Head.position))
                {
                    _offscreenSince.Remove(m);
                    continue;
                }
                if (!_offscreenSince.TryGetValue(m, out var since))
                {
                    _offscreenSince[m] = now;
                    continue;
                }
                if (now - since < CatchUpOffscreenSeconds) continue;
                if (TryCatchUp(m, playerPos, view, cam)) _offscreenSince.Remove(m);
            }
        }

        private bool TryCatchUp(SquadMember m, Vector3 playerPos, Vector3 view, Camera cam)
        {
            var index = Mathf.Max(0, SquadFollowPlanner.IndexOf(m.MemberId));
            var right = new Vector3(view.z, 0f, -view.x);
            for (var k = 0; k < 4; k++)
            {
                var back = 5f + 3f * k;
                var candidate = playerPos - view * back + right * ((index - 1) * 1.2f);
                if (!NavUtil.Sample(candidate, 3f, out var p)) continue;
                if (NavUtil.IsDoorway(p)) continue;
                if (!NavUtil.IsOffScreen(cam, p + Vector3.up * 1.6f)) continue;                // never pop in view
                var face = playerPos - p;
                face.y = 0f;
                m.Warp(p, face.sqrMagnitude > 1e-3f ? Quaternion.LookRotation(face.normalized, Vector3.up) : m.transform.rotation);
                LSLog.Verbose(LogCategory.Squad, m.MemberId + " caught up (teleport)");
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ frags

        private void ConsiderFrag(ITargetable player, float now)
        {
            _nextFrag = now + 1f;
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null || m.IsBusy || _fragUsed.Contains(m.MemberId) || m.Perception == null) continue;
                if (!FindCluster(m, player, out var centre)) continue;
                _fragUsed.Add(m.MemberId);
                _nextFrag = now + FragCooldown;                                                 // GDD §15: 30 s
                _callouts.Say(m, CalloutIds.FragOut(m.MemberId), now, true);
                var hand = m.Body?.RightHand;
                var target = centre;
                var member = m;
                m.PlayScripted(ActionIds.Throw, null, centre,
                    () => AICombat.ThrowGrenade(hand != null && hand != member.transform ? hand.position : member.EyePosition, target, Team.Squad),
                    null);
                return;
            }
        }

        private static bool FindCluster(SquadMember m, ITargetable player, out Vector3 centre)
        {
            centre = Vector3.zero;
            var mems = m.Perception.Memories;
            var playerPos = player?.Root != null ? player.Root.position : new Vector3(float.MaxValue, 0f, 0f);
            for (var i = 0; i < mems.Count; i++)
            {
                var a = mems[i];
                if (!a.Aware || !a.Visible || a.Target?.Root == null || !a.Target.IsAlive) continue;
                if (a.Distance < FragRangeMin || a.Distance > FragRangeMax) continue;
                var pa = a.Target.Root.position;
                var sum = pa;
                var n = 1;
                for (var k = 0; k < mems.Count; k++)
                {
                    if (k == i) continue;
                    var b = mems[k];
                    if (!b.Aware || b.Target?.Root == null || !b.Target.IsAlive || !Teams.AreHostile(Team.Squad, b.Target.Team)) continue;
                    var pb = b.Target.Root.position;
                    if ((pb - pa).sqrMagnitude > ClusterRadius * ClusterRadius) continue;
                    sum += pb;
                    n++;
                }
                if (n < 2) continue;                                                            // GDD §15: clusters of 2 or more
                var c = sum / n;
                if ((c - playerPos).sqrMagnitude < FragPlayerClearance * FragPlayerClearance) continue;
                centre = c;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ callout triggers

        /// <summary>A member started a reload (callout with a 20 s cooldown).</summary>
        public void OnMemberReload(SquadMember member, float now) => _callouts.OnReload(member, now);

        /// <summary>A member sidestepped for the player (hook for diagnostics).</summary>
        public void OnMemberYielded(SquadMember member) { }

        private void OnGrenadeLanded(Vector3 position, Team team)
        {
            if (!Teams.IsEnemySide(team)) return;
            var player = AIDirector.Instance?.Player;
            if (player?.Root == null || (player.Root.position - position).sqrMagnitude > GrenadeWarnRadius * GrenadeWarnRadius) return;
            SquadMember nearest = null;
            var best = float.MaxValue;
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null) continue;
                var d = (m.transform.position - player.Root.position).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                nearest = m;
            }
            if (nearest != null) _callouts.Say(nearest, CalloutIds.Grenade(nearest.MemberId), Time.time, true);
        }

        private void OnActorKilled(KillInfo k)
        {
            if (!k.ByPlayer || string.IsNullOrEmpty(k.VictimId)) return;
            for (var i = 0; i < HighPriorityTargets.Length; i++)
            {
                if (HighPriorityTargets[i] != k.VictimId) continue;
                var hollis = Find(SquadIds.Hollis);
                if (hollis != null) _callouts.Say(hollis, CalloutIds.GoodKill, Time.time, true);
                return;
            }
        }

        private void UpdateNags(ITargetable player, float now)
        {
            var p = player.Root.position;
            if ((p - _idleAnchor).sqrMagnitude > 1.5f * 1.5f)
            {
                _idleAnchor = p;
                _idleSince = now;
            }
            // CO_TIN_10: the player dropped below 25 % and survived (back above 90 %).
            var health = _health.Value;
            if (health != null && health.Max > 0f)
            {
                var ratio = health.Current / health.Max;
                if (ratio < 0.25f && health.IsAlive) _wasLow = true;
                else if (_wasLow && ratio >= 0.9f)
                {
                    _wasLow = false;
                    var tinker = Find(SquadIds.Tinker);
                    if (tinker != null && now >= _nextCloseCall && _callouts.Say(tinker, CalloutIds.CloseCall, now)) _nextCloseCall = now + 60f;
                }
            }
            if (now < _nextNag || !FollowObjectiveActive()) return;
            var hollis = Find(SquadIds.Hollis);
            if (hollis == null) return;
            if ((hollis.transform.position - p).sqrMagnitude > FollowBehindDistance * FollowBehindDistance)
            {
                if (_callouts.Say(hollis, CalloutIds.KeepUp, now)) _nextNag = now + 30f;
            }
            else if (now - _idleSince >= FollowIdleSeconds)
            {
                if (_callouts.Say(hollis, CalloutIds.OnMe, now)) _nextNag = now + 30f;
            }
        }

        private bool FollowObjectiveActive()
        {
            var o = _objectives.Value;
            if (o == null) return false;
            var markers = o.Markers;
            if (markers == null) return false;
            for (var i = 0; i < markers.Count; i++)
                if (markers[i].Kind == MarkerKind.Follow) return true;
            return false;
        }

        private void OnLineStarted(string lineId, string speakerId)
        {
            _speakerLine = lineId;
            var squadId = SpeakerIds.ToSquadId(speakerId);
            if (!string.IsNullOrEmpty(squadId))
            {
                var m = Find(squadId);
                _speaker = m != null ? m.Head : null;
                return;
            }
            if (speakerId == SpeakerIds.Calder)
            {
                var rig = AIDirector.Instance?.Rig;
                _speaker = rig?.Eye;
                return;
            }
            _speaker = null;                                                                    // radio voices: no head to look at
        }

        private void OnLineFinished(string lineId)
        {
            if (lineId != _speakerLine) return;
            _speaker = null;
            _speakerLine = null;
        }

        private void OnBeatStarted(int beat)
        {
            _fragUsed.Clear();                                                                  // GDD §15: 1 frag per member per beat
            _pathBeat = -1;
        }

        private void OnCheckpointLoaded(string cpId) => _callouts.Reset();

        // ------------------------------------------------------------------ ISquadService

        /// <inheritdoc/>
        public void SetNodeSet(string setId)
        {
            if (string.IsNullOrEmpty(setId)) return;
            var changed = _mode != SquadMode.NodeSet || _setId != setId;
            _mode = SquadMode.NodeSet;
            _setId = setId;
            _set = null;
            _waitingLogged = false;
            _nextOrders = 0f;
            ReleaseHolds();
            if (!changed) return;
            var director = AIDirector.Instance;
            if (director != null && director.Combat.InCombat)
            {
                var hollis = Find(SquadIds.Hollis) ?? (SquadMember.All.Count > 0 ? SquadMember.All[0] : null);
                if (hollis != null) _callouts.Say(hollis, CalloutIds.Moving(hollis.MemberId), Time.time);
            }
        }

        /// <inheritdoc/>
        public void Follow()
        {
            _mode = SquadMode.Follow;
            _setId = null;
            _set = null;
            _nextOrders = 0f;
            ReleaseHolds();
        }

        /// <inheritdoc/>
        public void TeleportToNodeSet(string setId)
        {
            if (!MarkerRegistry.Instance.TryGet(setId, out SquadNodeSet set))
            {
                LSLog.WarnOnce("squad-tp:" + setId, LogCategory.Squad, "TeleportToNodeSet: node set '" + setId + "' is not registered.");
                SetNodeSet(setId);
                return;
            }
            _mode = SquadMode.NodeSet;
            _setId = setId;
            _set = set;
            ReleaseHolds();
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null) continue;
                var p = NavUtil.SampleStopPoint(set.PositionOf(m.MemberId), NodeArriveRadius, true);
                m.Warp(p, set.RotationOf(m.MemberId));
            }
            _nextOrders = 0f;
        }

        /// <inheritdoc/>
        public bool AllArrived(string setId)
        {
            if (_mode != SquadMode.NodeSet || _setId != setId) return false;
            if (_set == null && !MarkerRegistry.Instance.TryGet(setId, out _set)) return false;
            var radius = Mathf.Max(NodeArriveRadius, _set.ArriveRadius);
            var all = SquadMember.All;
            if (all.Count == 0) return false;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m != null && !m.IsWithin(_set.PositionOf(m.MemberId), radius)) return false;
            }
            return true;
        }

        /// <inheritdoc/>
        public void PlayScripted(string member, string actionId, string markerId, System.Action done)
        {
            var m = Find(member);
            if (m == null)
            {
                LSLog.WarnOnce("squad-noscripted:" + member, LogCategory.Squad, "PlayScripted: no squad member '" + member + "'; completing at once.");
                done?.Invoke();
                return;
            }
            SceneMarker marker = null;
            var hasMarker = !string.IsNullOrEmpty(markerId) && MarkerRegistry.Instance.TryGet(markerId, out marker);
            if (!hasMarker && !string.IsNullOrEmpty(markerId))
                LSLog.WarnOnce("squad-nomarker:" + markerId, LogCategory.Squad, "PlayScripted: marker '" + markerId + "' not registered; " + member + " plays " + actionId + " where he stands.");
            Vector3? stand = null;
            Vector3? face = null;
            System.Action effect = null;
            var target = hasMarker ? marker.Position : m.transform.position + m.transform.forward * 8f;
            switch (actionId)
            {
                case ActionIds.ThrowSmokeWhite:
                    // B3: Tinker throws white smoke onto the marker; it lasts 20 s (GDD §7.3).
                    face = target;
                    effect = () => AICombat.SpawnSmoke(target, new Color(0.92f, 0.92f, 0.9f), 20f);
                    break;
                case ActionIds.PopSmokeGreen:
                    // B10: Hollis pops green smoke on the pad; it marks the LZ until Magpie lands (GDD §7.10).
                    face = target;
                    effect = () => AICombat.SpawnSmoke(target, new Color(0.35f, 0.85f, 0.3f), 100f);
                    break;
                case ActionIds.CrackChemlight:
                    effect = () => EnsureChemlight(m).SetOn(true);
                    break;
                default:
                    if (hasMarker)
                    {
                        var p = NavUtil.SampleStopPoint(marker.Position, NodeArriveRadius, true);
                        stand = p;
                        var off = marker.Position - p;
                        off.y = 0f;
                        face = off.sqrMagnitude > 0.09f ? marker.Position : p + marker.transform.forward;
                    }
                    break;
            }
            m.PlayScripted(actionId, stand, face, effect, done);
        }

        /// <inheritdoc/>
        public void ScriptedKill(string member, ITargetable target)
        {
            var m = Find(member);
            if (m == null || target == null) return;
            m.ScriptedKill(target);
            var line = CalloutIds.ScriptedKill(member);
            if (line != null) _callouts.Say(m, line, Time.time, true);
        }

        /// <inheritdoc/>
        public void SetChemlights(bool on)
        {
            _chemlights = on;
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
                if (all[i] != null) EnsureChemlight(all[i]).SetOn(on);
        }

        /// <inheritdoc/>
        public void SetLookTarget(Transform target, float weight)
        {
            _lookOverride = target;
            _lookWeight = target != null ? Mathf.Clamp01(weight) : 0f;
        }

        /// <inheritdoc/>
        public Transform Member(string id) => Find(id)?.transform;

        private void ReleaseHolds()
        {
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++) all[i]?.ClearHold();
        }

        private static Chemlight EnsureChemlight(SquadMember m)
        {
            if (m.Chemlight != null) return m.Chemlight;
            var existing = m.GetComponentInChildren<Chemlight>(true);
            if (existing != null)
            {
                m.SetChemlight(existing);
                return existing;
            }
            var go = new GameObject("Chemlight");
            go.transform.SetParent(m.transform, false);
            go.transform.localPosition = new Vector3(-0.14f, 1.32f, 0.12f);                       // left shoulder strap
            var c = go.AddComponent<Chemlight>();
            m.SetChemlight(c);
            return c;
        }

        // ------------------------------------------------------------------ ISaveable

        /// <inheritdoc/>
        public string SaveKey => Key;

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            w.Write("mode", (int)_mode);
            w.Write("set", _setId ?? string.Empty);
            w.Write("chem", _chemlights);
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null) continue;
                w.Write(m.MemberId + ".pos", m.transform.position);
                w.Write(m.MemberId + ".rot", m.transform.rotation);
            }
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            var set = r.ReadString("set", string.Empty);
            if ((SquadMode)r.ReadInt("mode") == SquadMode.NodeSet && !string.IsNullOrEmpty(set)) SetNodeSet(set);
            else Follow();
            SetChemlights(r.ReadBool("chem"));
            _callouts.Reset();
            var all = SquadMember.All;
            for (var i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null || !r.Has(m.MemberId + ".pos")) continue;
                m.Warp(r.ReadVector3(m.MemberId + ".pos"), r.ReadQuaternion(m.MemberId + ".rot", m.transform.rotation));
            }
        }
    }
}
