using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Dev
{
    /// <summary>
    /// Dev range AI stand-in: fires bursts at the player through <see cref="ICombatService.FireAI"/> (AK numbers of GDD §14.1:
    /// bursts of 3-6, 600 RPM, 0.6-1.2 s pauses) with a fixed hit chance, so near misses (0.5-3 m off the head), cracks,
    /// suppression and green tracers can be checked; optional frag throws and RPG rockets at the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RangeShooter : MonoBehaviour
    {
        [SerializeField] private string _weaponId = WeaponIds.AK;
        [SerializeField, Range(0f, 1f)] private float _hitChance = 0.15f;
        [SerializeField] private float _damage = 20f;
        [SerializeField] private Team _team = Team.Legion;
        [SerializeField] private int _tracerEvery = 3;
        [SerializeField, Tooltip("Seconds between frag throws (0 = never).")]
        private float _grenadeEvery;
        [SerializeField, Tooltip("Seconds between rockets (0 = never).")]
        private float _rocketEvery;
        [SerializeField] private Transform _muzzle;

        private ServiceRef<ICombatService> _combat;
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<IPlayerHealth> _playerHealth;
        private int _burstLeft;
        private float _next;
        private float _nextGrenade;
        private float _nextRocket;

        private void Update()
        {
            var combat = _combat.Value;
            var rig = _rig.Value;
            if (combat == null || rig == null || rig.Eye == null) return;
            var now = Time.time;
            var muzzle = _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.45f;
            if (now >= _next)
            {
                if (_burstLeft <= 0)
                {
                    _burstLeft = Random.Range(3, 7);       // GDD §14.1 rifleman bursts of 3-6
                }
                _burstLeft--;
                _next = now + (_burstLeft > 0 ? 0.1f : Random.Range(0.6f, 1.2f)); // 600 RPM; 0.6-1.2 s pauses
                combat.FireAI(new AIShotRequest
                {
                    Muzzle = muzzle,
                    Target = _playerHealth.Value,
                    HitChance = _hitChance,
                    Damage = _damage,
                    WeaponId = _weaponId,
                    Team = _team,
                    TracerEvery = _tracerEvery,
                });
            }
            if (_grenadeEvery > 0f && now >= _nextGrenade)
            {
                _nextGrenade = now + _grenadeEvery;
                combat.ThrowGrenadeAI(muzzle, rig.Root != null ? rig.Root.position : rig.Eye.position, _team);
            }
            if (_rocketEvery > 0f && now >= _nextRocket)
            {
                _nextRocket = now + _rocketEvery;
                combat.FireRocket(muzzle, rig.Eye.position + Random.insideUnitSphere * 2f, _team, null);
            }
        }
    }
}
