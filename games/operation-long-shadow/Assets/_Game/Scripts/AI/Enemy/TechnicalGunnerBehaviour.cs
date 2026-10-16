using System.Collections;
using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// E4-01, the DShK on the technical in Clock Square (GDD §7.4, §14.1; TECH_PLAN §7.6 TechnicalGunner): static on the
    /// truck bed, the gun traverses at 40°/s and fires 3-round bursts (550 RPM). 2 s after D028 starts (or on the mission
    /// event <see cref="AIEvents.TechnicalOpenFire"/>) it opens scripted fire on the arch where the squad is pinned (Q1),
    /// ignoring perception range; whenever it holds an attack token and sees the player it swings onto him instead. The
    /// gun shield (immune within ±60° of 234°) is <see cref="EnemyHealth"/>'s. When he dies (any cause) the ruptured fuel
    /// tank ignites 1.5 s later: a fireball through <see cref="AICombat.Explode"/> and the mission event
    /// <see cref="AIEvents.TechnicalIgnited"/> for WP10's static fire prop at B4.FireProp.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TechnicalGunnerBehaviour : MonoBehaviour, IEnemyBehaviour
    {
        /// <summary>Gun traverse (GDD §14.1: 40°/s).</summary>
        public const float TraverseDegreesPerSecond = 40f;
        /// <summary>Seconds after D028 starts before the gun opens up (GDD §7.4).</summary>
        public const float OpenFireDelay = 2f;
        /// <summary>The gun only fires when within this many degrees of its target.</summary>
        public const float FireCone = 3f;
        /// <summary>Fuel tank ignition delay after death (GDD §7.4).</summary>
        public const float IgnitionDelay = 1.5f;
        /// <summary>Line whose start cues the opening burst (GDD §7.4 "2 s after D028").</summary>
        public const string CueLine = "D028";
        /// <summary>Node set of the pinned squad at the arch (scripted target).</summary>
        public const string ArchNodeSet = "B4.NodeSet.Q1";
        /// <summary>Fallback aim marker at the arch.</summary>
        public const string ArchMarker = "B3.ArchTarget";
        /// <summary>Fire prop marker on the truck bed (fireball position).</summary>
        public const string FirePropMarker = "B4.FireProp";

        private EnemyBrain _brain;
        private EnemyShooter _scripted;
        private float _openAt = float.PositiveInfinity;
        private float _gunYaw;
        private bool _subscribed;
        private int _archIndex;
        private float _nextArchSwitch;
        private ServiceRef<ISquadService> _squad;

        /// <summary>True once the scripted fire at the arch has started.</summary>
        public bool ScriptedFireOpen => Time.time >= _openAt;

        /// <summary>Current gun yaw (compass degrees).</summary>
        public float GunYaw => _gunYaw;

        /// <inheritdoc/>
        public void OnConfigured(EnemyBrain brain)
        {
            _brain = brain;
            _scripted = new EnemyShooter(brain.Archetype);
            _openAt = float.PositiveInfinity;
            _gunYaw = MathX.BearingOf(transform.forward);
            if (!_subscribed)
            {
                GameEvents.VOLineStarted += OnLine;
                GameEvents.MissionEvent += OnMissionEvent;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            GameEvents.VOLineStarted -= OnLine;
            GameEvents.MissionEvent -= OnMissionEvent;
            _subscribed = false;
        }

        private void OnLine(string lineId, string speaker)
        {
            if (lineId == CueLine) _openAt = Mathf.Min(_openAt, Time.time + OpenFireDelay);
        }

        private void OnMissionEvent(string id)
        {
            if (id == AIEvents.TechnicalOpenFire) _openAt = Mathf.Min(_openAt, Time.time);
        }

        /// <inheritdoc/>
        public bool OverrideTick(EnemyBrain brain, float now, float dt)
        {
            if (brain.State != EnemyState.Combat) return false;
            var director = brain.Director;
            var player = director?.Player;
            ITargetable target = null;
            TargetMemory memory = null;
            var scripted = false;

            var pm = player != null ? brain.Perception.Find(player) : null;
            if (pm != null && pm.Aware && pm.Visible && director != null &&
                director.Tokens.Request(brain.AgentId, now, director.AimedAgent == brain.AgentId))
            {
                target = player;
                memory = pm;
            }
            else
            {
                director?.Tokens.Release(brain.AgentId);
                if (now >= _openAt)
                {
                    target = ArchTarget(now);
                    scripted = true;
                }
            }

            var aim = target != null ? Perception.AimOf(target) : ArchPoint();
            var desired = MathX.Bearing(transform.position, aim);
            _gunYaw = Mathf.MoveTowardsAngle(_gunYaw, desired, TraverseDegreesPerSecond * dt);   // GDD §14.1 traverse
            var dir = MathX.DirectionFromBearing(_gunYaw);
            var flat = new Vector3(aim.x - transform.position.x, 0f, aim.z - transform.position.z).magnitude;
            var traversed = transform.position + dir * Mathf.Max(5f, flat);
            traversed.y = aim.y;
            brain.Body?.SetAim(traversed);

            var aligned = Mathf.Abs(Mathf.DeltaAngle(_gunYaw, desired)) <= FireCone;
            var canFire = target != null && aligned;
            // Scripted fire at the arch is suppression on the pinned squad (VFX only, they cannot die, GDD §15).
            if (_scripted.Tick(now, canFire, brain.Actor.IsSuppressed(now), ref brain.Rng))
                brain.FireRound(target, memory, scripted, now, scripted);
            return true;
        }

        private ITargetable ArchTarget(float now)
        {
            var squad = _squad.Value;
            if (squad == null) return null;
            if (now >= _nextArchSwitch)
            {
                _nextArchSwitch = now + _brain.Rng.Range(2.5f, 4.5f);
                _archIndex = (_archIndex + 1) % SquadIds.All.Length;
            }
            for (var k = 0; k < SquadIds.All.Length; k++)
            {
                var m = squad.Member(SquadIds.All[(_archIndex + k) % SquadIds.All.Length]);
                var t = m != null ? m.GetComponent<ITargetable>() : null;
                if (t != null && t.IsAlive) return t;
            }
            return null;
        }

        private Vector3 ArchPoint()
        {
            var reg = MarkerRegistry.Instance;
            if (reg.TryGet(ArchNodeSet, out SceneMarker set)) return set.Position + Vector3.up * 1.2f;
            if (reg.TryGet(ArchMarker, out SceneMarker arch)) return arch.Position;
            return transform.position + transform.forward * 40f;
        }

        /// <inheritdoc/>
        public void OnDeath(EnemyBrain brain, in DamageInfo d)
        {
            brain.Director?.Tokens.Release(brain.AgentId);
            if (isActiveAndEnabled) StartCoroutine(Ignite());
            else IgniteNow();
        }

        private IEnumerator Ignite()
        {
            yield return new WaitForSeconds(IgnitionDelay);
            IgniteNow();
        }

        private void IgniteNow()
        {
            var pos = MarkerRegistry.Instance.TryGet(FirePropMarker, out SceneMarker prop) ? prop.Position : transform.position - Vector3.up * 0.6f;
            AICombat.Explode(pos, 60f, 2f, 6f, null, Team.Legion);                                 // fuel tank fireball (WP04 values)
            GameEvents.RaiseMissionEvent(AIEvents.TechnicalIgnited);
        }
    }
}
