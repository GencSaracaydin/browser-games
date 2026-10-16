using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Animated humanoid body driven by AI brains and scripts (WP05 HumanoidBody; WP04 DebugCapsuleBody for tests).
    /// Animation events "Foot_L", "Foot_R", "ThrowRelease", "MagOut", "MagIn" arrive through <see cref="AnimEvent"/>.
    /// </summary>
    public interface ICharacterBody
    {
        /// <summary>Navigation agent (updateRotation off; the body turns itself).</summary>
        UnityEngine.AI.NavMeshAgent Agent { get; }
        /// <summary>Moves to a destination at a speed (m/s).</summary>
        void MoveTo(Vector3 destination, float speed);
        /// <summary>Stops moving.</summary>
        void Stop();
        /// <summary>True when the last MoveTo destination is reached.</summary>
        bool HasArrived { get; }
        /// <summary>Stand / Crouch / Sprint.</summary>
        void SetStance(Stance stance);
        /// <summary>Aims the upper body at a world point; null lowers the weapon.</summary>
        void SetAim(Vector3? worldTarget);
        /// <summary>Head/eyes look-at with weight 0..1; null clears.</summary>
        void SetLookAt(Transform target, float weight);
        /// <summary>Cover pose index (0 = none; animator "Cover" int).</summary>
        void SetCoverPose(int pose);
        /// <summary>Plays a full-body or upper-body action ("BoltCut", "ThrowSmokeWhite", "Reload", ...).</summary>
        void PlayAction(string actionId, Action onDone = null);
        /// <summary>True while an action plays.</summary>
        bool IsActionPlaying { get; }
        /// <summary>Weapon muzzle (world weapon).</summary>
        Transform Muzzle { get; }
        /// <summary>Right-hand socket.</summary>
        Transform RightHand { get; }
        /// <summary>Head bone.</summary>
        Transform Head { get; }
        /// <summary>Swaps the held world weapon (null = unarmed).</summary>
        void SetWeaponVisual(string weaponId);
        /// <summary>Death clip then ragdoll after 0.2 s, impulse from the damage.</summary>
        void Die(in DamageInfo d);
        /// <summary>Animation LOD 0 (full) .. 2 (culled rigs).</summary>
        void SetAnimatorLod(int lod);
        /// <summary>Animation events.</summary>
        event Action<string> AnimEvent;
    }
}
