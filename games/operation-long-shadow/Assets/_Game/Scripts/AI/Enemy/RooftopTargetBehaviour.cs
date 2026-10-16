using LongShadow.AI.Common;
using LongShadow.AI.Encounters;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// Beat 11 rooftop targets T1-T4 (GDD §7.11, §14.1; TECH_PLAN §7.6 RooftopTarget): no perception, always aware of the
    /// helicopter, they stand and fire at the door gunner only inside their window of exfil time (the B11.Exfil spline
    /// vehicle's <see cref="ISplineVehicle.Elapsed"/>, or seconds since activation when it is missing), and crouch
    /// behind the parapet outside it. T3 (<see cref="EnemySpawnDef.FireAt"/>) is hidden behind his parapet until t 28,
    /// stands, raises the launcher 1.2 s and fires the RPG at t 30 whatever happens: shot during the raise, he fires as
    /// he falls (<see cref="AIEvents.ExfilRpgLaunch"/> cues D081 and the climax).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RooftopTargetBehaviour : MonoBehaviour, IEnemyBehaviour
    {
        /// <summary>Launcher raise before the t 30 shot (GDD §7.11).</summary>
        public const float RaiseSeconds = 1.2f;

        private EnemyBrain _brain;
        private EnemySpawnDef _def;
        private ISplineVehicle _exfil;
        private bool _searched;
        private bool _hidden;
        private bool _raised;
        private bool _fired;

        /// <summary>True once T3 has launched.</summary>
        public bool Fired => _fired;

        /// <inheritdoc/>
        public void OnConfigured(EnemyBrain brain)
        {
            _brain = brain;
            _def = brain.Context?.Def;
            _exfil = null;
            _searched = false;
            _raised = false;
            _fired = false;
            _hidden = _def != null && _def.HiddenUntil > 0f;
            brain.SetHidden(_hidden);
            brain.Body?.SetStance(_hidden ? Stance.Crouch : Stance.Stand);
        }

        /// <summary>Exfil time t (s) for this target.</summary>
        public float ExfilTime(float now)
        {
            if (!_searched)
            {
                _searched = true;
                if (MarkerRegistry.Instance.TryGet(ExfilSchedule.ExfilVehicleId, out SceneMarker m)) _exfil = m.GetComponent<ISplineVehicle>();
            }
            if (_exfil != null && !(_exfil is Object o && o == null) && (_exfil.IsMoving || _exfil.Elapsed > 0f)) return _exfil.Elapsed;
            return now - (_brain?.Context?.EncounterTime ?? 0f);
        }

        /// <inheritdoc/>
        public bool OverrideTick(EnemyBrain brain, float now, float dt)
        {
            if (_def == null) return false;
            var t = ExfilTime(now);
            var player = brain.Director?.Player;
            if (_hidden && t >= _def.HiddenUntil)
            {
                _hidden = false;
                brain.SetHidden(false);
                brain.Body?.SetStance(Stance.Stand);
            }
            if (_hidden) return true;
            var aim = HeliAimPoint(player);
            brain.Body?.SetAim(aim);

            if (_def.FireAt > 0f)
            {
                if (!_raised && t >= _def.FireAt - RaiseSeconds)
                {
                    _raised = true;
                    brain.Body?.PlayAction(ActionIds.RpgRaise);
                }
                if (!_fired && t >= _def.FireAt) Launch(brain);
                return true;
            }

            var inWindow = t >= _def.WindowStart && t <= _def.WindowEnd;
            brain.Body?.SetStance(inWindow ? Stance.Stand : Stance.Crouch);
            var canFire = inWindow && player != null;
            if (brain.Shooter.Tick(now, canFire, false, ref brain.Rng)) brain.FireRound(player, null, false, now, true);
            return true;
        }

        private Vector3 HeliAimPoint(ITargetable player)
        {
            if (player != null) return Perception.AimOf(player);
            if (_exfil != null && _exfil.Root != null) return _exfil.Root.position + Vector3.up;
            return transform.position + transform.forward * 50f + Vector3.up * 25f;
        }

        private void Launch(EnemyBrain brain)
        {
            if (_fired) return;
            _fired = true;
            var muzzle = brain.Body?.Muzzle != null ? brain.Body.Muzzle.position : transform.position + Vector3.up * 1.6f;
            var target = _exfil != null && _exfil.Root != null ? _exfil.Root.position : HeliAimPoint(brain.Director?.Player);
            AICombat.FireRocket(muzzle, target, brain.Team, null);
            GameEvents.RaiseMissionEvent(AIEvents.ExfilRpgLaunch);
        }

        /// <inheritdoc/>
        public void OnDeath(EnemyBrain brain, in DamageInfo d)
        {
            // "Fires at t 30 whatever happens (if shot during the raise he fires as he falls)" (GDD §7.11).
            if (_def != null && _def.FireAt > 0f && !_fired && !_hidden) Launch(brain);
        }
    }
}
