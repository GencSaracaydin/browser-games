using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Bone collider that forwards hits to the <see cref="IDamageable"/> in its parents (TECH_PLAN §6.4). While alive
    /// the collider is kinematic on layer Hitbox (SquadHitbox for the squad); after death the same colliders become
    /// ragdoll bodies on layer Ragdoll (WP05 RagdollBuilder).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour, IHitbox
    {
        [SerializeField] private HitZone _zone = HitZone.Torso;
        [SerializeField, Tooltip("Zone multiplier; 0 = default (head 2.0, limb 0.85, torso 1, GDD §13.1). Weapons may override.")]
        private float _multiplier;
        [SerializeField] private SurfaceType _surface = SurfaceType.Flesh;

        private IDamageable _owner;
        private bool _searched;

        /// <summary>Default zone multipliers of GDD §13.1 (most weapons: head x2.0, limbs x0.85).</summary>
        public static float DefaultMultiplier(HitZone zone) => zone == HitZone.Head ? 2.0f : zone == HitZone.Limb ? 0.85f : 1f;

        /// <inheritdoc/>
        public IDamageable Owner
        {
            get
            {
                if (!_searched || (_owner is Object o && o == null))
                {
                    _owner = GetComponentInParent<IDamageable>();
                    _searched = true;
                }
                return _owner;
            }
        }

        /// <inheritdoc/>
        public HitZone Zone => _zone;
        /// <inheritdoc/>
        public float Multiplier => _multiplier > 0f ? _multiplier : DefaultMultiplier(_zone);
        /// <inheritdoc/>
        public SurfaceType Surface => _surface;

        /// <summary>Configures the hitbox (builders).</summary>
        public void Configure(HitZone zone, float multiplier = 0f, SurfaceType surface = SurfaceType.Flesh)
        {
            _zone = zone;
            _multiplier = multiplier;
            _surface = surface;
        }

        /// <summary>Forwards damage to the owner with this hitbox's zone.</summary>
        public void ApplyHit(DamageInfo d)
        {
            var o = Owner;
            if (o == null) return;
            d.Zone = _zone;
            o.ApplyDamage(d);
        }

        private void OnTransformParentChanged() => _searched = false;
    }
}
