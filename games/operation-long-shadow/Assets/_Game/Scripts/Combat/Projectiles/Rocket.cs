using System;
using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Service;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Projectiles
{
    /// <summary>
    /// RPG rocket for <see cref="ICombatService.FireRocket"/> (GDD §14.1: 90 direct, 4 m splash with falloff). Flies
    /// through the target point at a visual speed with a slight corkscrew wobble, sweeps a sphere each frame, and detonates
    /// on the first hit (direct damage to that damageable, splash to the rest) or as an airburst at the end of its range.
    /// The smoke trail keeps drifting after the blast. A fly-by whoosh plays when it passes the listener; passing within
    /// 3 m of the player's head raises <see cref="GameEvents.NearMiss"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Rocket : MonoBehaviour
    {
        private static readonly RaycastHit[] s_hits = new RaycastHit[8];

        /// <summary>Layers the rocket collides with.</summary>
        public const int HitMask = (1 << LayerIds.Default) | (1 << LayerIds.Vehicle) | (1 << LayerIds.Character) | (1 << LayerIds.Hitbox) |
                                   (1 << LayerIds.SquadHitbox) | (1 << LayerIds.Debris) | (1 << LayerIds.Player);

        private Vector3 _origin;
        private Vector3 _dir;
        private Vector3 _side;
        private Vector3 _up;
        private float _travel;
        private float _wobblePhase;
        private Team _team;
        private GameObject _instigator;
        private Action<Vector3> _onImpact;
        private Action<Rocket> _release;
        private bool _flying;
        private float _afterlife;
        private bool _flybyPlayed;
        private bool _nearMissRaised;
        private Renderer[] _renderers;
        private ParticleSystem[] _trail;
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<IPlayerRig> _rig;

        /// <summary>True while flying.</summary>
        public bool IsFlying => _flying;

        /// <summary>Sets the pool callback and caches parts (called once by the pool).</summary>
        public void Bind(Action<Rocket> release, ParticleSystem[] trail)
        {
            _release = release;
            _trail = trail;
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>Fires from <paramref name="from"/> through <paramref name="to"/>.</summary>
        public void Launch(Vector3 from, Vector3 to, Team team, GameObject instigator, Action<Vector3> onImpact)
        {
            var d = to - from;
            _dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            _side = Vector3.Cross(Vector3.up, _dir);
            if (_side.sqrMagnitude < 1e-4f) _side = Vector3.right;
            _side.Normalize();
            _up = Vector3.Cross(_dir, _side).normalized;
            _origin = from;
            _travel = 0f;
            _wobblePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _team = team;
            _instigator = instigator;
            _onImpact = onImpact;
            _flying = true;
            _afterlife = 0f;
            _flybyPlayed = false;
            _nearMissRaised = false;
            gameObject.SetActive(false);
            transform.SetPositionAndRotation(from, Quaternion.LookRotation(_dir));
            gameObject.SetActive(true);
            SetVisible(true);
            if (_trail != null)
                foreach (var ps in _trail)
                    if (ps != null)
                    {
                        ps.Clear(true);
                        ps.Play(true);
                    }
        }

        /// <summary>Removes the rocket without exploding (checkpoint reload).</summary>
        public void Cancel()
        {
            _flying = false;
            _release?.Invoke(this);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (!_flying)
            {
                _afterlife += dt;
                if (_afterlife > 3f) _release?.Invoke(this);
                return;
            }
            var spec = WeaponTable.Rocket;
            var prev = transform.position;
            _travel += spec.Speed * dt;
            var wobble = Mathf.Clamp01(_travel / 15f) * spec.Wobble;
            var w = _travel * 0.45f + _wobblePhase;
            var next = _origin + _dir * _travel + _side * (Mathf.Sin(w) * wobble) + _up * (Mathf.Cos(w * 0.7f) * wobble * 0.6f);
            var step = next - prev;
            var len = step.magnitude;
            if (len > 1e-4f)
            {
                var n = Physics.SphereCastNonAlloc(prev, 0.08f, step / len, s_hits, len, HitMask, QueryTriggerInteraction.Ignore);
                var best = -1;
                for (var i = 0; i < n; i++)
                {
                    if (s_hits[i].distance <= 0f && s_hits[i].point == Vector3.zero) continue; // started inside: ignore
                    if (IsOwn(s_hits[i].collider)) continue;
                    if (best < 0 || s_hits[i].distance < s_hits[best].distance) best = i;
                }
                if (best >= 0)
                {
                    var h = s_hits[best];
                    Detonate(h.point, h.normal, h.collider);
                    return;
                }
                transform.SetPositionAndRotation(next, Quaternion.LookRotation(step / len));
            }
            ListenerChecks(prev, next);
            if (_travel >= spec.MaxDistance) Detonate(next, -_dir, null);
        }

        private bool IsOwn(Collider c)
        {
            if (c == null) return true;
            if (_instigator != null && c.transform.IsChildOf(_instigator.transform)) return true;
            return c.transform.IsChildOf(transform);
        }

        private void ListenerChecks(Vector3 prev, Vector3 next)
        {
            var rig = _rig.Value;
            if (rig == null || rig.Eye == null) return;
            var head = rig.Eye.position;
            var d = NearMissMath.ClosestApproach(prev, next, head, out var closest, out var t);
            if (!_flybyPlayed && d < 20f && t < 1f)
            {
                _flybyPlayed = true;
                _audio.Value?.Play(CombatCues.RocketFlyby, closest);
            }
            if (!_nearMissRaised && d <= WeaponTable.NearMissRadius && t > 0f && t < 1f && Teams.IsEnemySide(_team))
            {
                _nearMissRaised = true;
                GameEvents.RaiseNearMiss(new NearMissEvent { Point = closest, Direction = _dir, Distance = d, ShooterTeam = _team, WeaponId = WeaponIds.RPG7 });
            }
        }

        private void Detonate(Vector3 point, Vector3 normal, Collider hit)
        {
            _flying = false;
            _afterlife = 0f;
            SetVisible(false);
            if (_trail != null)
                foreach (var ps in _trail)
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var spec = WeaponTable.Rocket;
            IDamageable direct = null;
            if (hit != null)
            {
                direct = CombatResolver.DamageableOf(hit);
                if (direct != null && direct.IsAlive)
                {
                    var info = new DamageInfo(spec.DirectDamage, DamageType.Explosion, HitZone.Torso, point, _dir, _instigator, _team, WeaponIds.RPG7);
                    direct.ApplyDamage(info);
                }
            }
            CombatResolver.Explode(point + normal * 0.1f, spec.SplashDamage, spec.LethalRadius, spec.SplashRadius, _instigator, _team,
                WeaponIds.RPG7, FxIds.ExplosionRocket, CombatCues.ExplosionRocket, spec.NoiseRadius, direct);
            var cb = _onImpact;
            _onImpact = null;
            try
            {
                cb?.Invoke(point);
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Combat, e, this);
            }
        }

        private void SetVisible(bool on)
        {
            if (_renderers == null) return;
            for (var i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && !(_renderers[i] is ParticleSystemRenderer)) _renderers[i].enabled = on;
        }
    }
}
