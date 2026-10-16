using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// E10-07, the Legion RPG gunner on truck 2's bed (GDD §7.10, §14.1; TECH_PLAN §7.6 RPGBehaviour). Scripted targets
    /// only (perception is irrelevant): until hold time t 60 he fires at the bleacher where the squad holds P1 (4 m
    /// splash can hurt the player); at t 63, if alive, he fires at Magpie on her final approach, which makes WP07 start
    /// the wave-off (<see cref="AIEvents.RpgFiredAtMagpie"/>). From that moment he is a <see cref="ITargetable.SquadTarget"/>,
    /// and if he is still alive 8 s later (when he would telegraph again) Hollis kills him. Every rocket is telegraphed:
    /// a shout and a 1.2 s launcher raise (<see cref="ActionIds.RpgRaise"/>), then 8 s to reload. t counts from the
    /// encounter's activation (the end of D070).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RpgBehaviour : MonoBehaviour, IEnemyBehaviour
    {
        /// <summary>Telegraph (GDD §14.1: 1.2 s).</summary>
        public const float Telegraph = 1.2f;
        /// <summary>Reload (GDD §14.1: 8 s).</summary>
        public const float ReloadSeconds = 8f;
        /// <summary>Bleacher fire ends at hold time t 60 (GDD §7.10).</summary>
        public const float BleacherUntil = 60f;
        /// <summary>He fires at Magpie at hold time t 63 (GDD §7.10).</summary>
        public const float MagpieAt = 63f;
        /// <summary>Hollis kills him this long after the wave-off shot if he still lives (GDD §7.10).</summary>
        public const float HollisKillDelay = 8f;
        /// <summary>Squad node set at the bleacher (P1).</summary>
        public const string BleacherNodeSet = "B10.NodeSet.P1";
        /// <summary>Magpie's approach vehicle id.</summary>
        public const string HeliVehicleId = "B10.HeliApproach";
        /// <summary>Pad marker (fallback aim).</summary>
        public const string PadMarker = "B10.Pad";

        private enum Phase { Ready, Raising, Reloading, Done }

        private EnemyBrain _brain;
        private Phase _phase;
        private float _phaseUntil;
        private bool _atMagpie;
        private bool _firedAtMagpie;
        private float _hollisKillAt = float.PositiveInfinity;
        private Vector3 _aim;
        private ServiceRef<ISquadService> _squad;

        /// <summary>Rockets fired (tests, diagnostics).</summary>
        public int RocketsFired { get; private set; }
        /// <summary>True once he has fired at Magpie (the one wave-off).</summary>
        public bool FiredAtMagpie => _firedAtMagpie;

        /// <inheritdoc/>
        public void OnConfigured(EnemyBrain brain)
        {
            _brain = brain;
            _phase = Phase.Ready;
            _phaseUntil = 0f;
            _atMagpie = false;
            _firedAtMagpie = false;
            _hollisKillAt = float.PositiveInfinity;
            RocketsFired = 0;
        }

        /// <inheritdoc/>
        public bool OverrideTick(EnemyBrain brain, float now, float dt)
        {
            var t = now - (brain.Context?.EncounterTime ?? 0f);
            if (_firedAtMagpie && now >= _hollisKillAt)
            {
                _hollisKillAt = float.PositiveInfinity;
                if (brain.Actor.IsAlive) _squad.Value?.ScriptedKill(SquadIds.Hollis, brain.Actor);
            }
            switch (_phase)
            {
                case Phase.Raising:
                    brain.Body?.SetAim(_atMagpie ? MagpiePoint() : _aim);
                    if (now >= _phaseUntil) Launch(brain, now);
                    return true;
                case Phase.Reloading:
                    if (now >= _phaseUntil) _phase = _firedAtMagpie ? Phase.Done : Phase.Ready;
                    return true;
                case Phase.Done:
                    return true;
            }
            if (t >= MagpieAt - Telegraph && !_firedAtMagpie && HeliAvailable())
            {
                BeginRaise(brain, now, true);
                return true;
            }
            if (t < BleacherUntil - Telegraph)
            {
                _aim = BleacherPoint(brain);
                BeginRaise(brain, now, false);
            }
            return true;
        }

        private void BeginRaise(EnemyBrain brain, float now, bool magpie)
        {
            _atMagpie = magpie;
            _phase = Phase.Raising;
            _phaseUntil = now + Telegraph;
            brain.Barker.Bark(BarkIds.LegContact, transform, brain.Team, now, 4f, true);           // the shout of the telegraph
            brain.Body?.SetAim(magpie ? MagpiePoint() : _aim);
            brain.Body?.PlayAction(ActionIds.RpgRaise);
        }

        private void Launch(EnemyBrain brain, float now)
        {
            var target = _atMagpie ? MagpiePoint() : _aim;
            var muzzle = brain.Body?.Muzzle != null ? brain.Body.Muzzle.position : transform.position + Vector3.up * 1.6f;
            AICombat.FireRocket(muzzle, target, brain.Team, null);
            RocketsFired++;
            _phase = Phase.Reloading;
            _phaseUntil = now + ReloadSeconds;
            if (!_atMagpie) return;
            _firedAtMagpie = true;
            brain.Actor.SquadTarget = true;                                                          // GDD §7.10 after the wave-off
            _hollisKillAt = now + HollisKillDelay;
            GameEvents.RaiseMissionEvent(AIEvents.RpgFiredAtMagpie);
        }

        private Vector3 BleacherPoint(EnemyBrain brain)
        {
            Vector3 centre;
            if (MarkerRegistry.Instance.TryGet(BleacherNodeSet, out SceneMarker set)) centre = set.Position;
            else
            {
                var squad = _squad.Value;
                var h = squad?.Member(SquadIds.Sparrow);
                centre = h != null ? h.position : new Vector3(340f, -1f, 390.5f);                   // Appendix A B10.LZLine
            }
            // A few metres of scatter along the bleacher: the back wall takes most of it.
            return centre + new Vector3(brain.Rng.Range(-6f, 6f), brain.Rng.Range(0.6f, 1.6f), brain.Rng.Range(-1.5f, 2.5f));
        }

        private bool HeliAvailable()
        {
            var v = Heli();
            return v != null || MarkerRegistry.Instance.Has(PadMarker);
        }

        private Vector3 MagpiePoint()
        {
            var v = Heli();
            if (v != null && v.Root != null) return v.Root.position;
            return MarkerRegistry.Instance.TryGet(PadMarker, out SceneMarker pad) ? pad.Position + new Vector3(60f, 25f, 5f) : transform.position + new Vector3(-30f, 30f, -20f);
        }

        private static ISplineVehicle Heli()
        {
            if (!MarkerRegistry.Instance.TryGet(HeliVehicleId, out SceneMarker m)) return null;
            return m.GetComponent<ISplineVehicle>();
        }

        /// <inheritdoc/>
        public void OnDeath(EnemyBrain brain, in DamageInfo d)
        {
            _phase = Phase.Done;
            _hollisKillAt = float.PositiveInfinity;
        }
    }
}
