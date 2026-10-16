using LongShadow.Characters.IK;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Body
{
    /// <summary>
    /// Bone and socket references of a humanoid body, resolved once from the Humanoid avatar (any source rig) and saved
    /// in the prefab by <c>BodyPrefabBuilder</c>; resolves itself at runtime when a field is empty. Also stores the
    /// rig-independent local axes the procedural solvers need (head forward, hand frames).
    /// Sockets (children of bones): <c>Socket_RightGrip</c> (weapon), <c>Socket_LeftGrip</c> (support hand),
    /// <c>Socket_Back</c> (stowed weapon), <c>Socket_Eyes</c> (look origin).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BodyRig : MonoBehaviour
    {
        public const string RightGripName = "Socket_RightGrip";
        public const string LeftGripName = "Socket_LeftGrip";
        public const string BackName = "Socket_Back";
        public const string EyesName = "Socket_Eyes";

        [SerializeField] private Animator _animator;
        [Header("Torso")]
        [SerializeField] private Transform _hips;
        [SerializeField] private Transform _spine;
        [SerializeField] private Transform _chest;
        [SerializeField] private Transform _upperChest;
        [SerializeField] private Transform _neck;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _jaw;
        [Header("Arms")]
        [SerializeField] private Transform _leftShoulder;
        [SerializeField] private Transform _leftUpperArm;
        [SerializeField] private Transform _leftLowerArm;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightShoulder;
        [SerializeField] private Transform _rightUpperArm;
        [SerializeField] private Transform _rightLowerArm;
        [SerializeField] private Transform _rightHand;
        [Header("Legs")]
        [SerializeField] private Transform _leftUpperLeg;
        [SerializeField] private Transform _leftLowerLeg;
        [SerializeField] private Transform _leftFoot;
        [SerializeField] private Transform _rightUpperLeg;
        [SerializeField] private Transform _rightLowerLeg;
        [SerializeField] private Transform _rightFoot;
        [Header("Sockets")]
        [SerializeField] private Transform _rightGrip;
        [SerializeField] private Transform _leftGrip;
        [SerializeField] private Transform _back;
        [SerializeField] private Transform _eyes;
        [Header("Rig-independent local axes (bind pose)")]
        [SerializeField] private Vector3 _headForwardLocal = Vector3.forward;
        [SerializeField] private Vector3 _leftFingersLocal = Vector3.right;
        [SerializeField] private Vector3 _leftPalmLocal = Vector3.down;
        [SerializeField] private Vector3 _rightFingersLocal = Vector3.right;
        [SerializeField] private Vector3 _rightPalmLocal = Vector3.down;
        [SerializeField, Tooltip("Standing height in metres measured by the builder (top of head).")]
        private float _height = 1.78f;

        private bool _resolved;

        public Animator Animator => _animator;
        public Transform Hips => _hips;
        public Transform Spine => _spine;
        public Transform Chest => _chest;
        public Transform UpperChest => _upperChest;
        public Transform Neck => _neck;
        public Transform Head => _head;
        public Transform Jaw => _jaw;
        public Transform LeftShoulder => _leftShoulder;
        public Transform LeftUpperArm => _leftUpperArm;
        public Transform LeftLowerArm => _leftLowerArm;
        public Transform LeftHand => _leftHand;
        public Transform RightShoulder => _rightShoulder;
        public Transform RightUpperArm => _rightUpperArm;
        public Transform RightLowerArm => _rightLowerArm;
        public Transform RightHand => _rightHand;
        public Transform LeftUpperLeg => _leftUpperLeg;
        public Transform LeftLowerLeg => _leftLowerLeg;
        public Transform LeftFoot => _leftFoot;
        public Transform RightUpperLeg => _rightUpperLeg;
        public Transform RightLowerLeg => _rightLowerLeg;
        public Transform RightFoot => _rightFoot;
        /// <summary>Weapon socket on the right hand (forward = barrel, up = top of the weapon).</summary>
        public Transform RightGrip => _rightGrip;
        /// <summary>Support-hand socket on the left hand; aligned to a weapon's LeftGrip by IK.</summary>
        public Transform LeftGrip => _leftGrip;
        /// <summary>Back socket for a stowed weapon.</summary>
        public Transform Back => _back;
        /// <summary>Eye midpoint (look-at origin, booth POV).</summary>
        public Transform Eyes => _eyes;
        /// <summary>Head bone local axis that points forward in the bind pose.</summary>
        public Vector3 HeadForwardLocal => _headForwardLocal;
        /// <summary>Standing height (m).</summary>
        public float Height => _height;
        /// <summary>The top torso bone that exists (UpperChest, else Chest, else Spine).</summary>
        public Transform TopSpine => _upperChest != null ? _upperChest : _chest != null ? _chest : _spine;
        /// <summary>True once the core bones are known.</summary>
        public bool IsValid => _hips != null && _head != null && _rightHand != null && _leftHand != null;

        private void Awake() => EnsureResolved();

        /// <summary>Fills empty references from the Humanoid avatar (no sockets are created at runtime unless missing).</summary>
        public void EnsureResolved()
        {
            if (_resolved && IsValid) return;
            _resolved = true;
            if (IsValid && _rightGrip != null && _leftGrip != null) return;
            Resolve(true);
        }

        /// <summary>
        /// Resolves every bone from the Animator's Humanoid avatar and creates missing sockets. Builders call this in the
        /// bind pose (prefab default pose, a T-pose) so the stored local axes are exact.
        /// </summary>
        public void Resolve(bool createSockets)
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            var a = _animator;
            if (a == null || a.avatar == null || !a.avatar.isHuman)
            {
                LSLog.WarnOnce("bodyrig-nohuman:" + name, LogCategory.Characters,
                    name + ": BodyRig needs an Animator with a Humanoid avatar; IK and sockets are disabled.", this);
                return;
            }
            _hips = Bone(a, HumanBodyBones.Hips, _hips);
            _spine = Bone(a, HumanBodyBones.Spine, _spine);
            _chest = Bone(a, HumanBodyBones.Chest, _chest);
            _upperChest = Bone(a, HumanBodyBones.UpperChest, _upperChest);
            _neck = Bone(a, HumanBodyBones.Neck, _neck);
            _head = Bone(a, HumanBodyBones.Head, _head);
            _jaw = Bone(a, HumanBodyBones.Jaw, _jaw);
            _leftShoulder = Bone(a, HumanBodyBones.LeftShoulder, _leftShoulder);
            _leftUpperArm = Bone(a, HumanBodyBones.LeftUpperArm, _leftUpperArm);
            _leftLowerArm = Bone(a, HumanBodyBones.LeftLowerArm, _leftLowerArm);
            _leftHand = Bone(a, HumanBodyBones.LeftHand, _leftHand);
            _rightShoulder = Bone(a, HumanBodyBones.RightShoulder, _rightShoulder);
            _rightUpperArm = Bone(a, HumanBodyBones.RightUpperArm, _rightUpperArm);
            _rightLowerArm = Bone(a, HumanBodyBones.RightLowerArm, _rightLowerArm);
            _rightHand = Bone(a, HumanBodyBones.RightHand, _rightHand);
            _leftUpperLeg = Bone(a, HumanBodyBones.LeftUpperLeg, _leftUpperLeg);
            _leftLowerLeg = Bone(a, HumanBodyBones.LeftLowerLeg, _leftLowerLeg);
            _leftFoot = Bone(a, HumanBodyBones.LeftFoot, _leftFoot);
            _rightUpperLeg = Bone(a, HumanBodyBones.RightUpperLeg, _rightUpperLeg);
            _rightLowerLeg = Bone(a, HumanBodyBones.RightLowerLeg, _rightLowerLeg);
            _rightFoot = Bone(a, HumanBodyBones.RightFoot, _rightFoot);
            if (!createSockets) return;

            var root = transform;
            if (_head != null)
            {
                _headForwardLocal = AimChain.ClosestLocalAxis(_head, root.forward);
                if (_eyes == null) _eyes = FindOrCreate(_head, EyesName);
                if (_eyes.localPosition == Vector3.zero)
                {
                    // Eye midpoint: 9 cm above the head bone pivot and 9 cm forward (adult head; Biped head pivots at the skull base).
                    var s = _height / 1.78f;
                    _eyes.position = _head.position + root.up * (0.09f * s) + root.forward * (0.09f * s);
                    _eyes.rotation = root.rotation;
                }
            }
            if (_rightHand != null)
            {
                var basis = ComputeHand(a, false);
                _rightFingersLocal = Quaternion.Inverse(_rightHand.rotation) * basis.Fingers;
                _rightPalmLocal = Quaternion.Inverse(_rightHand.rotation) * basis.Palm;
                if (_rightGrip == null)
                {
                    _rightGrip = FindOrCreate(_rightHand, RightGripName);
                    basis.PrimaryGripPose(out var p, out var r);
                    _rightGrip.SetPositionAndRotation(p, r);
                }
            }
            if (_leftHand != null)
            {
                var basis = ComputeHand(a, true);
                _leftFingersLocal = Quaternion.Inverse(_leftHand.rotation) * basis.Fingers;
                _leftPalmLocal = Quaternion.Inverse(_leftHand.rotation) * basis.Palm;
                if (_leftGrip == null)
                {
                    _leftGrip = FindOrCreate(_leftHand, LeftGripName);
                    basis.SupportGripPose(out var p, out var r);
                    _leftGrip.SetPositionAndRotation(p, r);
                }
            }
            var backParent = TopSpine;
            if (_back == null && backParent != null)
            {
                _back = FindOrCreate(backParent, BackName);
                // Slung across the back, muzzle up-left, 16 cm behind the spine.
                _back.position = backParent.position - root.forward * 0.16f + root.up * 0.02f;
                _back.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(-35f, root.forward) * root.up, -root.forward);
            }
        }

        /// <summary>
        /// Hand rotation that puts the hand's fingers along <paramref name="fingersWorld"/> with the palm facing
        /// <paramref name="palmWorld"/> (procedural gestures, watch check).
        /// </summary>
        public Quaternion HandRotationFor(bool left, Vector3 fingersWorld, Vector3 palmWorld)
        {
            var fl = left ? _leftFingersLocal : _rightFingersLocal;
            var pl = left ? _leftPalmLocal : _rightPalmLocal;
            var local = Quaternion.LookRotation(fl, pl);
            var world = Quaternion.LookRotation(fingersWorld, palmWorld);
            return world * Quaternion.Inverse(local);
        }

        /// <summary>Palm normal of a hand in world space (current pose).</summary>
        public Vector3 PalmWorld(bool left)
        {
            var h = left ? _leftHand : _rightHand;
            return h != null ? h.rotation * (left ? _leftPalmLocal : _rightPalmLocal) : Vector3.down;
        }

        /// <summary>Finger direction of a hand in world space (current pose).</summary>
        public Vector3 FingersWorld(bool left)
        {
            var h = left ? _leftHand : _rightHand;
            return h != null ? h.rotation * (left ? _leftFingersLocal : _rightFingersLocal) : Vector3.forward;
        }

#if UNITY_EDITOR
        /// <summary>Builders: sets the measured standing height.</summary>
        public void EditorSetHeight(float height) => _height = height;

        /// <summary>Builders: sets the Animator (when it is not on this GameObject).</summary>
        public void EditorSetAnimator(Animator animator) => _animator = animator;

        /// <summary>Builders: explicit hand frames for rigs without finger bones (placeholder mannequin).</summary>
        public void EditorSetHandFrames(Vector3 leftFingersWorld, Vector3 leftPalmWorld, Vector3 rightFingersWorld, Vector3 rightPalmWorld)
        {
            if (_leftHand != null)
            {
                _leftFingersLocal = Quaternion.Inverse(_leftHand.rotation) * leftFingersWorld.normalized;
                _leftPalmLocal = Quaternion.Inverse(_leftHand.rotation) * leftPalmWorld.normalized;
            }
            if (_rightHand != null)
            {
                _rightFingersLocal = Quaternion.Inverse(_rightHand.rotation) * rightFingersWorld.normalized;
                _rightPalmLocal = Quaternion.Inverse(_rightHand.rotation) * rightPalmWorld.normalized;
            }
        }
#endif

        private HandBasis ComputeHand(Animator a, bool left)
        {
            var hand = left ? _leftHand : _rightHand;
            var index = a.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var middle = a.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var little = a.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var thumb = a.GetBoneTransform(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            var root = transform;
            if (index != null && little != null)
            {
                var mid = middle != null ? middle.position : (index.position + little.position) * 0.5f;
                return HandBasis.Compute(hand.position, index.position, mid, little.position,
                                         thumb != null ? thumb.position : (Vector3?)null, -root.up);
            }
            // No finger bones: fingers continue the forearm, palm down (T-pose convention).
            var lower = left ? _leftLowerArm : _rightLowerArm;
            var dir = lower != null ? (hand.position - lower.position).normalized : (left ? -root.right : root.right);
            return HandBasis.FromAxes(hand.position, dir, -root.up, 0.09f * (_height / 1.78f), left);
        }

        private static Transform Bone(Animator a, HumanBodyBones b, Transform current)
        {
            if (current != null) return current;
            return a.GetBoneTransform(b);
        }

        private static Transform FindOrCreate(Transform parent, string childName)
        {
            var t = parent.Find(childName);
            if (t != null) return t;
            t = new GameObject(childName).transform;
            t.SetParent(parent, false);
            return t;
        }
    }
}
