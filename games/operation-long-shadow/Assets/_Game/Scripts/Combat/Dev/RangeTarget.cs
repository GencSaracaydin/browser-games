using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Dev
{
    /// <summary>
    /// Dev range dummy (Dev_CombatRange scene): a <see cref="Health"/> with head/torso/limb <see cref="Hitbox"/> colliders
    /// on layer Hitbox, logging every hit (weapon, zone, distance, damage, HP left) so the GDD §13.1 rows "body hits to kill
    /// a Legion rifleman, close/far" and the head multipliers can be checked by shooting; it stands up again after 3 s.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class RangeTarget : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds before a dead dummy stands up again.")]
        private float _respawnSeconds = 3f;

        private Health _health;
        private float _deadFor = -1f;
        private int _hits;
        private Renderer[] _renderers;
        private ServiceRef<IPlayerRig> _rig;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Killed += OnKilled;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Killed -= OnKilled;
        }

        private void OnDamaged(Health h, DamageInfo d)
        {
            _hits++;
            var rig = _rig.Value;
            var dist = rig != null && rig.Eye != null ? Vector3.Distance(rig.Eye.position, d.Point) : 0f;
            LSLog.Info(LogCategory.Combat, "[Range] " + name + " hit " + _hits + ": " + d.WeaponId + " " + d.Zone + " at " + dist.ToString("0.0") +
                                           " m for " + d.Amount.ToString("0.0") + " (HP " + h.Current.ToString("0") + "/" + h.Max.ToString("0") + ")", this);
        }

        private void OnKilled(Health h, DamageInfo d)
        {
            LSLog.Info(LogCategory.Combat, "[Range] " + name + " down after " + _hits + " hit(s) (" + d.Type + ", " + d.Zone + ")", this);
            _deadFor = 0f;
            SetTint(new Color(0.25f, 0.25f, 0.25f));
        }

        private void Update()
        {
            if (_deadFor < 0f) return;
            _deadFor += Time.deltaTime;
            if (_deadFor < _respawnSeconds) return;
            _deadFor = -1f;
            _hits = 0;
            _health.ResetFull();
            SetTint(Color.white);
        }

        private void SetTint(Color c)
        {
            foreach (var r in _renderers)
                if (r != null) r.material.color = c * (r.sharedMaterial != null ? r.sharedMaterial.color : Color.white);
        }
    }
}
