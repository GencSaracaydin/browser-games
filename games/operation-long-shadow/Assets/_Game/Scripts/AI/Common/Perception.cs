using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Sight and hearing of one agent (TECH_PLAN §7.6 Perception, GDD §14.2). At 10 Hz, scheduled round-robin under the
    /// 24-ray budget, it lists hostiles from <see cref="ITargetRegistry"/>, keeps those within the sight range (stage
    /// range, doubled up to 70 m on a lit player, or a fixed range for the marksman) and the field of view (120°, 160°
    /// within 8 m), and casts one line-of-sight ray (AISight mask: Default | Vehicle | SmokeLOS) per candidate. Visible
    /// targets fill a detection meter over 0.4-1.2 s by distance (crouch ×1.5, sprint ×0.6); once full the agent is aware.
    /// Noises (<see cref="Hear"/>) give a last known position; shots and explosions make it aware at once.
    /// </summary>
    public sealed class Perception : IPerceptionAgent
    {
        /// <summary>Update interval (10 Hz, TECH_PLAN §7.16).</summary>
        public const float Interval = 0.1f;
        /// <summary>Detection time for new targets once the agent is already fighting.</summary>
        public const float CombatDetectionSeconds = 0.25f;
        /// <summary>Unseen and unheard targets are forgotten after this long.</summary>
        public const float ForgetSeconds = 45f;
        /// <summary>Detection meter decay per second while the target is out of sight (unaware agents).</summary>
        public const float DetectionDecay = 0.35f;
        /// <summary>Suspicion added by a non-alarming noise (footsteps, voices).</summary>
        public const float SuspicionPerNoise = 0.35f;

        private static readonly List<ITargetable> s_hostiles = new List<ITargetable>(16);

        private readonly IPerceptionOwner _owner;
        private readonly List<TargetMemory> _memories = new List<TargetMemory>(8);
        private readonly List<TargetMemory> _free = new List<TargetMemory>(4);
        private readonly List<TargetMemory> _candidates = new List<TargetMemory>(8);
        private readonly List<float> _candidateRanges = new List<float>(8);
        private ServiceRef<ITargetRegistry> _targets;
        private ServiceRef<IPlayerRig> _rig;
        private float _nextAt;
        private float _lastRun = float.NegativeInfinity;
        private float _stageRange = PerceptionMath.GoldenRange;

        /// <summary>Creates a perception for an owner; <paramref name="phase"/> staggers the first update (0..1 of the interval).</summary>
        public Perception(IPerceptionOwner owner, float phase = 0f)
        {
            _owner = owner;
            _nextAt = Mathf.Clamp01(phase) * Interval;
        }

        /// <summary>Known targets.</summary>
        public IReadOnlyList<TargetMemory> Memories => _memories;
        /// <summary>Rays cast by the last update.</summary>
        public int RaysLastRun { get; private set; }
        /// <summary>Time of the last update.</summary>
        public float LastRun => _lastRun;

        /// <summary>Highest detection meter (1 when aware of anything): the Beat 2 sentry HUD meter.</summary>
        public float Awareness01
        {
            get
            {
                var a = 0f;
                for (var i = 0; i < _memories.Count; i++)
                {
                    var m = _memories[i];
                    if (m.Aware) return 1f;
                    if (m.Detection > a) a = m.Detection;
                }
                return a;
            }
        }

        /// <summary>True if aware of at least one living hostile.</summary>
        public bool AnyAware
        {
            get
            {
                for (var i = 0; i < _memories.Count; i++)
                    if (_memories[i].Aware && Alive(_memories[i].Target)) return true;
                return false;
            }
        }

        /// <summary>True if any aware target is visible now.</summary>
        public bool AnyVisible
        {
            get
            {
                for (var i = 0; i < _memories.Count; i++)
                    if (_memories[i].Aware && _memories[i].Visible && Alive(_memories[i].Target)) return true;
                return false;
            }
        }

        /// <inheritdoc/>
        public bool PerceptionDue(float now) => _owner.PerceptionEnabled && now >= _nextAt;

        /// <inheritdoc/>
        public int PreparePerception(float now)
        {
            _candidates.Clear();
            _candidateRanges.Clear();
            var reg = _targets.Value;
            if (reg == null) return 0;
            reg.GetHostilesOf(_owner.Team, s_hostiles);
            var eye = _owner.EyePosition;
            var fwd = _owner.Forward;
            var director = AIDirector.Instance;
            _stageRange = director != null ? director.StageSightRange : PerceptionMath.GoldenRange;
            var player = reg.Player;
            _owner.AttentionLimits(out var rangeCap, out var fovOverride);
            for (var i = 0; i < s_hostiles.Count; i++)
            {
                var t = s_hostiles[i];
                if (!Alive(t)) continue;
                var aim = AimOf(t);
                var to = aim - eye;
                var d = to.magnitude;
                var lit = director != null && ReferenceEquals(t, player) && director.PlayerLit;
                var range = PerceptionMath.EffectiveRange(_stageRange, lit, _owner.FixedSightRange);
                if (rangeCap > 0f && range > rangeCap) range = rangeCap;
                var mem = GetOrCreate(t);
                mem.Distance = d;
                if (d > range) continue;
                if (fovOverride > 0f ? !PerceptionMath.InFov(fwd, to, d, fovOverride) : !PerceptionMath.InFov(fwd, to, d)) continue;
                _candidates.Add(mem);
                _candidateRanges.Add(range);
            }
            s_hostiles.Clear();
            return _candidates.Count;
        }

        /// <inheritdoc/>
        public void RunPerception(float now)
        {
            var dt = float.IsNegativeInfinity(_lastRun) ? Interval : Mathf.Clamp(now - _lastRun, 0f, 0.5f);
            var eye = _owner.EyePosition;
            RaysLastRun = 0;
            for (var i = 0; i < _memories.Count; i++) _memories[i].LastTouched = -1f; // marks "not a candidate"
            for (var i = 0; i < _candidates.Count; i++)
            {
                var m = _candidates[i];
                var aim = AimOf(m.Target);
                RaysLastRun++;
                var blocked = Physics.Linecast(eye, aim, LayerIds.AISight, QueryTriggerInteraction.Ignore);
                UpdateVisibility(m, !blocked, now, dt, _candidateRanges[i]);
                m.LastTouched = now;
            }
            for (var i = _memories.Count - 1; i >= 0; i--)
            {
                var m = _memories[i];
                if (m.LastTouched < 0f)
                {
                    UpdateVisibility(m, false, now, dt, _stageRange);
                    m.LastTouched = Mathf.Max(m.LastSeen, m.LastHeard);
                }
                if (!Alive(m.Target) || (!m.Visible && now - Mathf.Max(m.LastSeen, m.LastHeard) > ForgetSeconds && m.Detection <= 0f))
                {
                    _memories.RemoveAt(i);
                    m.Reset(null);
                    _free.Add(m);
                }
            }
            _candidates.Clear();
            _candidateRanges.Clear();
            _lastRun = now;
            _nextAt = now + Interval;
        }

        /// <summary>Applies a visibility sample (also used by tests).</summary>
        public void UpdateVisibility(TargetMemory m, bool visible, float now, float dt, float range)
        {
            if (visible)
            {
                if (!m.Visible) m.VisibleSince = now;
                m.Visible = true;
                m.LastSeen = now;
                if (m.Target?.Root != null) m.LastKnown = m.Target.Root.position;
                if (!m.Aware)
                {
                    float seconds;
                    if (_owner.InCombat) seconds = CombatDetectionSeconds;
                    else
                    {
                        var scale = _owner.DetectionTimeScale(m.Target, m.Distance);
                        seconds = scale > 0f ? PerceptionMath.DetectionSeconds(m.Distance, range, StanceOf(m.Target)) * scale : -1f;
                    }
                    if (seconds > 0f)
                    {
                        m.Detection = Mathf.Min(1f, m.Detection + dt / Mathf.Max(0.05f, seconds));
                        if (m.Detection >= 1f) m.Aware = true;
                    }
                }
            }
            else
            {
                m.Visible = false;
                if (!m.Aware) m.Detection = Mathf.Max(0f, m.Detection - DetectionDecay * dt);
            }
        }

        /// <summary>
        /// Processes a noise (GDD §14.2 hearing). Returns true if heard. Shots and explosions make the agent aware of the
        /// source; footsteps and voices add suspicion.
        /// </summary>
        public bool Hear(in NoiseEvent e, float now, float hearingScale)
        {
            if (!_owner.PerceptionEnabled || !Teams.AreHostile(_owner.Team, e.Source)) return false;
            if (!PerceptionMath.CanHear(e, _owner.EyePosition, hearingScale)) return false;
            var source = SourceOf(e);
            if (source == null) return false;
            var m = GetOrCreate(source);
            m.LastHeard = now;
            m.LastKnown = e.Position;
            if (PerceptionMath.IsAlarming(e.Kind))
            {
                m.Detection = 1f;
                m.Aware = true;
            }
            else if (!m.Aware)
            {
                m.Detection = Mathf.Min(1f, m.Detection + SuspicionPerNoise);
                if (m.Detection >= 1f) m.Aware = true;
            }
            return true;
        }

        /// <summary>Makes the agent aware of a target (alerts, damage taken, scripted retargets).</summary>
        public TargetMemory ForceAware(ITargetable target, Vector3 lastKnown, float now)
        {
            if (!Alive(target)) return null;
            var m = GetOrCreate(target);
            m.Aware = true;
            m.Detection = 1f;
            m.LastKnown = lastKnown;
            m.LastHeard = Mathf.Max(m.LastHeard, now);
            return m;
        }

        /// <summary>Adds suspicion to a target (ally killed nearby, sentry glimpse) without making the agent aware.</summary>
        public void AddSuspicion(ITargetable target, float amount, Vector3 lastKnown, float now)
        {
            if (!Alive(target)) return;
            var m = GetOrCreate(target);
            if (m.Aware) return;
            m.Detection = Mathf.Clamp01(m.Detection + amount);
            m.LastKnown = lastKnown;
            m.LastHeard = Mathf.Max(m.LastHeard, now);
            if (m.Detection >= 1f) m.Aware = true;
        }

        /// <summary>Memory of a target, or null.</summary>
        public TargetMemory Find(ITargetable target)
        {
            for (var i = 0; i < _memories.Count; i++)
                if (ReferenceEquals(_memories[i].Target, target)) return _memories[i];
            return null;
        }

        /// <summary>
        /// Best target: an aware, visible target (the preferred one if visible, else the nearest); otherwise the most
        /// recently seen or heard aware target. Null if unaware of everything.
        /// </summary>
        public TargetMemory Best(ITargetable preferred = null, bool preferredOnly = false)
        {
            TargetMemory bestVisible = null;
            TargetMemory bestKnown = null;
            for (var i = 0; i < _memories.Count; i++)
            {
                var m = _memories[i];
                if (!m.Aware || !Alive(m.Target)) continue;
                if (preferredOnly && preferred != null && !ReferenceEquals(m.Target, preferred)) continue;
                if (m.Visible)
                {
                    if (preferred != null && ReferenceEquals(m.Target, preferred)) return m;
                    if (bestVisible == null || m.Distance < bestVisible.Distance) bestVisible = m;
                }
                var t = Mathf.Max(m.LastSeen, m.LastHeard);
                if (bestKnown == null || t > Mathf.Max(bestKnown.LastSeen, bestKnown.LastHeard)) bestKnown = m;
            }
            return bestVisible ?? bestKnown;
        }

        /// <summary>Forgets everything (respawn, checkpoint).</summary>
        public void Clear()
        {
            for (var i = 0; i < _memories.Count; i++)
            {
                _memories[i].Reset(null);
                _free.Add(_memories[i]);
            }
            _memories.Clear();
            _candidates.Clear();
            _candidateRanges.Clear();
            _lastRun = float.NegativeInfinity;
        }

        /// <summary>Delays the next update (spawn staggering).</summary>
        public void Schedule(float at) => _nextAt = at;

        private TargetMemory GetOrCreate(ITargetable t)
        {
            var m = Find(t);
            if (m != null) return m;
            if (_free.Count > 0)
            {
                m = _free[_free.Count - 1];
                _free.RemoveAt(_free.Count - 1);
            }
            else m = new TargetMemory();
            m.Reset(t);
            _memories.Add(m);
            return m;
        }

        private ITargetable SourceOf(in NoiseEvent e)
        {
            var reg = _targets.Value;
            if (reg == null) return null;
            if (e.Source == Team.Player) return reg.Player;
            reg.GetHostilesOf(_owner.Team, s_hostiles);
            ITargetable best = null;
            var bestD = float.MaxValue;
            for (var i = 0; i < s_hostiles.Count; i++)
            {
                var t = s_hostiles[i];
                if (t.Team != e.Source || t.Root == null) continue;
                var d = (t.Root.position - e.Position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                }
            }
            s_hostiles.Clear();
            return best;
        }

        private Stance StanceOf(ITargetable t)
        {
            var rig = _rig.Value;
            if (rig != null && t != null && t.Team == Team.Player) return rig.Stance;
            return Stance.Stand;
        }

        /// <summary>Aim point of a target (chest), else its root lifted 1.3 m.</summary>
        public static Vector3 AimOf(ITargetable t)
        {
            if (t == null) return Vector3.zero;
            var a = t.AimPoint;
            if (a != null) return a.position;
            return t.Root != null ? t.Root.position + Vector3.up * 1.3f : Vector3.zero;
        }

        private static bool Alive(ITargetable t)
        {
            if (t == null) return false;
            if (t is Object o && o == null) return false;
            return t.IsAlive;
        }
    }
}
