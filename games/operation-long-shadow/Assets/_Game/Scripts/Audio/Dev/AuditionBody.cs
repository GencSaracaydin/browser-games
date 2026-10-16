using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Dev
{
    /// <summary>
    /// Minimal <see cref="ICharacterBody"/> + <see cref="ITargetable"/> stand-in for the audio test range and the EditMode
    /// tests (footstep director, body foley): a body that does nothing but report a team and raise anim events on demand.
    /// It lives in the runtime assembly because Unity cannot add MonoBehaviours from editor-only (test) assemblies to
    /// GameObjects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuditionBody : MonoBehaviour, ICharacterBody, ITargetable
    {
        [SerializeField] private Team _team = Team.Legion;

        /// <summary>Team reported to the target registry.</summary>
        public Team BodyTeam
        {
            get => _team;
            set => _team = value;
        }

        public UnityEngine.AI.NavMeshAgent Agent => null;
        public void MoveTo(Vector3 destination, float speed) { }
        public void Stop() { }
        public bool HasArrived => true;
        public void SetStance(Stance stance) { }
        public void SetAim(Vector3? worldTarget) { }
        public void SetLookAt(Transform target, float weight) { }
        public void SetCoverPose(int pose) { }
        public void PlayAction(string actionId, Action onDone = null) => onDone?.Invoke();
        public bool IsActionPlaying => false;
        public Transform Muzzle => transform;
        public Transform RightHand => transform;
        public Transform Head => transform;
        public void SetWeaponVisual(string weaponId) { }
        public void Die(in DamageInfo d) { }
        public void SetAnimatorLod(int lod) { }
        public event Action<string> AnimEvent;

        /// <summary>Raises an animation event (footstep, body fall) as an animator would.</summary>
        public void Raise(string id) => AnimEvent?.Invoke(id);

        public Team Team => _team;
        public bool IsAlive => true;
        public Transform AimPoint => transform;
        public void ApplyDamage(in DamageInfo d) { }
        public string ActorId => name;
        public Transform Root => transform;
        public Vector3 Velocity => Vector3.zero;
        public PerchTag Perch => PerchTag.Ground;
        public bool SquadTarget { get; set; }
        public float LastEngagedByPlayer => 0f;
        public void NotifyEngagedByPlayer() { }
    }
}
