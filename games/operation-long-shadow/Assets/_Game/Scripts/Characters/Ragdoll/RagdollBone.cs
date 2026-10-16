using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Ragdoll
{
    /// <summary>
    /// One physical bone of a body (built by <c>RagdollBuilder</c>): the same collider is a kinematic hitbox while alive
    /// (layer Hitbox/SquadHitbox) and a dynamic ragdoll body after death (layer Ragdoll), TECH_PLAN §6.4.
    /// </summary>
    [Serializable]
    public sealed class RagdollBone
    {
        [SerializeField] private HumanBodyBones _human;
        [SerializeField] private Transform _bone;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private Collider _collider;
        [SerializeField] private CharacterJoint _joint;
        [SerializeField] private Hitbox _hitbox;

        public RagdollBone() { }

        public RagdollBone(HumanBodyBones human, Transform bone, Rigidbody body, Collider collider, CharacterJoint joint, Hitbox hitbox)
        {
            _human = human;
            _bone = bone;
            _body = body;
            _collider = collider;
            _joint = joint;
            _hitbox = hitbox;
        }

        public HumanBodyBones Human => _human;
        public Transform Bone => _bone;
        public Rigidbody Body => _body;
        public Collider Collider => _collider;
        public CharacterJoint Joint => _joint;
        public Hitbox Hitbox => _hitbox;
    }
}
