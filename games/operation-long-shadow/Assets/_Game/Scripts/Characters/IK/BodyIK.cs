using LongShadow.Characters.Animation;
using LongShadow.Characters.Body;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.IK
{
    /// <summary>
    /// Procedural post-animation pass of a third-person body (TECH_PLAN §7.8 "aim IK, left-hand IK to the weapon
    /// foregrip, look-at, hit reactions"), run in LateUpdate after the Animator:
    /// <list type="number">
    /// <item>cover lean (spine roll),</item>
    /// <item>weapon aim: the spine chain turns until the <em>muzzle</em> points at the aim target,</item>
    /// <item>recoil kick and hit flinch springs,</item>
    /// <item>head look-at (neck + head), following the sights while aiming,</item>
    /// <item>hands: the support hand onto the weapon's LeftGrip, or a procedural gesture (signals, handset, camera, reach).</item>
    /// </list>
    /// Solved on transforms (not Animation Rigging jobs) so the left hand follows the weapon after the spine aim in the
    /// same frame and gestures need no clips (see Docs/DECISIONS.md [WP05]). Turned off beyond 40 m (TECH_PLAN §7.16).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Squad + 20)]
    [DisallowMultipleComponent]
    public sealed class BodyIK : MonoBehaviour
    {
        [SerializeField] private BodyRig _rig;
        [Header("Aim (muzzle onto the target)")]
        [SerializeField, Tooltip("Fractions of the remaining aim error taken by Spine, Chest, UpperChest per pass (last = 1 converges).")]
        private float[] _spineWeights = { 0.25f, 0.35f, 1f };
        [SerializeField] private int _aimIterations = 2;
        [SerializeField, Tooltip("Largest spine correction (deg); beyond it the body turns its feet.")]
        private float _maxAimAngle = 70f;
        [SerializeField] private float _aimBlendSpeed = 9f;
        [Header("Look")]
        [SerializeField, Tooltip("Neck and head fractions (TECH_PLAN §7.8: head 0.8 of the aim).")]
        private float[] _lookWeights = { 0.4f, 1f };
        [SerializeField] private float _maxLookAngle = 75f;
        [SerializeField] private float _lookBlendSpeed = 5f;
        [SerializeField, Tooltip("Head weight while it follows the aim (TECH_PLAN §7.8 Head 0.8).")]
        private float _headAimWeight = 0.8f;
        [Header("Hands")]
        [SerializeField] private float _handBlendSpeed = 10f;
        [SerializeField, Tooltip("Elbow hint weight for the support arm (0 keeps the animated elbow).")]
        private float _elbowHintWeight = 0.45f;
        [Header("Springs")]
        [SerializeField, Tooltip("Upper-chest kick per shot (deg).")]
        private float _recoilDegrees = 3.2f;
        [SerializeField, Tooltip("Torso flinch per hit (deg); heads take 1.6x.")]
        private float _flinchDegrees = 9f;
        [Header("LOD")]
        [SerializeField, Tooltip("No IK beyond this camera distance (TECH_PLAN §7.16: rigs off beyond 40 m).")]
        private float _maxDistance = 40f;

        // inputs
        private Vector3? _aimTarget;
        private bool _aimWanted;
        private Transform _muzzle;
        private Transform _weaponLeftGrip;
        private bool _leftHandOnWeapon;
        private Transform _lookTransform;
        private Vector3? _lookPoint;
        private float _lookWeightTarget;
        private float _leanTarget;
        private int _forcedLod;
        private Transform _reachTarget;
        private float _headPitchDegrees;
        private float _headPitchWeightTarget;

        // smoothed state
        private float _aimWeight;
        private float _lookWeight;
        private float _leftWeaponWeight;
        private float _lean;
        private float _headPitchWeight;
        private SpringFloat _recoil = new SpringFloat(7f, 0.38f);
        private SpringVector3 _flinch = new SpringVector3(5.5f, 0.45f);
        private bool _flinchHead;

        // gesture
        private Gesture _gesture;
        private float _gestureTime;
        private float _gestureLength;
        private bool _gestureLoop;
        private float _gestureWeight;

        private readonly Transform[] _aimBones = new Transform[3];
        private readonly float[] _aimBoneWeights = new float[3];
        private int _aimBoneCount;
        private readonly Transform[] _lookBones = new Transform[2];
        private readonly float[] _lookBoneWeights = new float[2];
        private int _lookBoneCount;
        private float _nextLodCheck;
        private bool _distanceCulled;
        private bool _unscaled;
        private Camera _lodCamera;

        /// <summary>The rig solved.</summary>
        public BodyRig Rig => _rig;
        /// <summary>Current blended aim weight (0..1).</summary>
        public float AimWeight => _aimWeight;
        /// <summary>Current support-hand-on-weapon weight (0..1).</summary>
        public float LeftHandWeight => _leftWeaponWeight;
        /// <summary>The gesture playing (None when idle).</summary>
        public Gesture CurrentGesture => _gesture;
        /// <summary>Last solved distance between the support hand and the weapon's LeftGrip (metres; DoD &lt; 2 cm).</summary>
        public float LastLeftHandError { get; private set; }
        /// <summary>Remaining aim error after the last solve (degrees).</summary>
        public float LastAimError { get; private set; }
        /// <summary>Use unscaled time (booth bodies, R3).</summary>
        public bool UnscaledTime { get => _unscaled; set => _unscaled = value; }

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<BodyRig>();
            CacheChains();
        }

        private void OnEnable()
        {
            _recoil.Reset(0f);
            _flinch.Reset(Vector3.zero);
            _aimWeight = 0f;
            _leftWeaponWeight = 0f;
            _gesture = Gesture.None;
            _gestureWeight = 0f;
        }

        /// <summary>Re-reads the bone chains from the rig (after the rig resolves).</summary>
        public void CacheChains()
        {
            if (_rig == null) return;
            _rig.EnsureResolved();
            _aimBoneCount = 0;
            AddAimBone(_rig.Spine, _spineWeights.Length > 0 ? _spineWeights[0] : 0.25f);
            AddAimBone(_rig.Chest, _spineWeights.Length > 1 ? _spineWeights[1] : 0.35f);
            AddAimBone(_rig.UpperChest, _spineWeights.Length > 2 ? _spineWeights[2] : 1f);
            // The last bone that exists must finish the correction.
            if (_aimBoneCount > 0) _aimBoneWeights[_aimBoneCount - 1] = 1f;
            _lookBoneCount = 0;
            if (_rig.Neck != null)
            {
                _lookBones[_lookBoneCount] = _rig.Neck;
                _lookBoneWeights[_lookBoneCount++] = _lookWeights.Length > 0 ? _lookWeights[0] : 0.4f;
            }
            if (_rig.Head != null)
            {
                _lookBones[_lookBoneCount] = _rig.Head;
                _lookBoneWeights[_lookBoneCount++] = 1f;
            }
        }

        private void AddAimBone(Transform t, float w)
        {
            if (t == null) return;
            _aimBones[_aimBoneCount] = t;
            _aimBoneWeights[_aimBoneCount++] = w;
        }

        // ---------------------------------------------------------------- inputs

        /// <summary>Aim point (null lowers the weapon).</summary>
        public void SetAim(Vector3? worldTarget)
        {
            _aimTarget = worldTarget;
            _aimWanted = worldTarget.HasValue;
        }

        /// <summary>Weapon references from the holder (muzzle may be null when unarmed).</summary>
        public void SetWeapon(Transform muzzle, Transform leftGrip)
        {
            _muzzle = muzzle;
            _weaponLeftGrip = leftGrip;
        }

        /// <summary>Support hand on the weapon's LeftGrip (rifles, launchers, two-handed pistols).</summary>
        public bool LeftHandOnWeapon { get => _leftHandOnWeapon; set => _leftHandOnWeapon = value; }

        /// <summary>Look-at target and weight (null clears).</summary>
        public void SetLookAt(Transform target, float weight)
        {
            _lookTransform = target;
            _lookPoint = null;
            _lookWeightTarget = target != null ? Mathf.Clamp01(weight) : 0f;
        }

        /// <summary>Look-at a world point (null clears).</summary>
        public void SetLookPoint(Vector3? point, float weight)
        {
            _lookTransform = null;
            _lookPoint = point;
            _lookWeightTarget = point.HasValue ? Mathf.Clamp01(weight) : 0f;
        }

        /// <summary>Cover lean -1 (left) .. 1 (right).</summary>
        public void SetLean(float lean) => _leanTarget = Mathf.Clamp(lean, -1f, 1f);

        /// <summary>Forced animation LOD from the owner (0 full, 1+ no IK).</summary>
        public void SetForcedLod(int lod) => _forcedLod = Mathf.Max(0, lod);

        /// <summary>Right-hand reach target for <see cref="Gesture.Reach"/>.</summary>
        public void SetReachTarget(Transform target) => _reachTarget = target;

        /// <summary>Procedural head pitch (degrees, + = down) blended by weight (booth head lift / limp).</summary>
        public void SetHeadPitch(float degrees, float weight)
        {
            _headPitchDegrees = degrees;
            _headPitchWeightTarget = Mathf.Clamp01(weight);
        }

        /// <summary>Starts a gesture for <paramref name="seconds"/> (or until <see cref="StopGesture"/> when looping).</summary>
        public void PlayGesture(Gesture gesture, float seconds, bool loop)
        {
            _gesture = gesture;
            _gestureTime = 0f;
            _gestureLength = Mathf.Max(0.3f, seconds);
            _gestureLoop = loop;
        }

        /// <summary>Releases the gesture (blends out).</summary>
        public void StopGesture()
        {
            if (_gesture == Gesture.None) return;
            _gestureLoop = false;
            _gestureLength = Mathf.Min(_gestureLength, _gestureTime + 0.3f);
        }

        /// <summary>One shot: upper-body kick.</summary>
        public void AddRecoil(float scale = 1f) => _recoil.AddImpulse(-_recoilDegrees * 70f * scale); // impulse ≈ peak deg × ω / 0.6 (7 Hz, ζ 0.38)

        /// <summary>A hit: the torso (or head) flinches along the shot direction.</summary>
        public void AddFlinch(Vector3 shotDirection, HitZone zone, float scale = 1f)
        {
            var root = transform;
            var dir = new Vector3(shotDirection.x, 0f, shotDirection.z);
            if (dir.sqrMagnitude < 1e-6f) dir = -root.forward;
            dir.Normalize();
            // Rotation axis that bends the body away from the shooter.
            var axis = Vector3.Cross(root.up, dir);
            var deg = _flinchDegrees * scale * (zone == HitZone.Head ? 1.6f : zone == HitZone.Limb ? 0.5f : 1f);
            _flinchHead = zone == HitZone.Head;
            _flinch.AddImpulse(axis * (deg * 60f)); // impulse ≈ peak deg × ω / 0.55 (5.5 Hz, ζ 0.45)
        }

        // ---------------------------------------------------------------- solve

        private void LateUpdate()
        {
            if (_rig == null || !_rig.IsValid) return;
            var dt = _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            UpdateDistanceCull();
            Solve(dt, false, _forcedLod == 0 && !_distanceCulled);
        }

        /// <summary>
        /// Runs one solve now with every weight snapped to its target (validation tools and EditMode tests, where LateUpdate
        /// does not run). Call after posing the skeleton.
        /// </summary>
        public void SolveImmediate()
        {
            if (_rig == null) _rig = GetComponent<BodyRig>();
            if (_rig == null) return;
            if (_aimBoneCount == 0 && _lookBoneCount == 0) CacheChains();
            if (!_rig.IsValid) return;
            Solve(1f / 60f, true, true);
        }

        private void Solve(float dt, bool snap, bool active)
        {
            // Weights always advance so nothing pops when IK comes back.
            var aimTarget = _aimWanted && _muzzle != null ? 1f : 0f;
            var gestureUsesLeft = GestureUsesLeft(_gesture);
            var leftTarget = _leftHandOnWeapon && _weaponLeftGrip != null && !gestureUsesLeft ? 1f : 0f;
            _aimWeight = snap ? aimTarget : MathX.Damp(_aimWeight, aimTarget, _aimBlendSpeed, dt);
            _lookWeight = snap ? _lookWeightTarget : MathX.Damp(_lookWeight, _lookWeightTarget, _lookBlendSpeed, dt);
            _leftWeaponWeight = snap ? leftTarget : MathX.Damp(_leftWeaponWeight, leftTarget, _handBlendSpeed, dt);
            _lean = snap ? _leanTarget : MathX.Damp(_lean, _leanTarget, 6f, dt);
            _headPitchWeight = snap ? _headPitchWeightTarget : MathX.Damp(_headPitchWeight, _headPitchWeightTarget, 4f, dt);
            var recoil = _recoil.Step(dt);
            var flinch = _flinch.Step(dt);
            StepGesture(dt);
            if (!active) return;

            var root = transform;
            // 1) cover lean: roll the spine around the body forward.
            if (Mathf.Abs(_lean) > 0.001f)
            {
                var deg = -_lean * 16f; // GDD §14.3 lean left/right on cover nodes
                Rotate(_rig.Spine, root.forward, deg * 0.4f);
                Rotate(_rig.Chest, root.forward, deg * 0.6f);
            }

            // 2) weapon aim.
            if (_aimWeight > 0.001f && _muzzle != null && _aimTarget.HasValue && _aimBoneCount > 0)
                LastAimError = AimChain.Solve(_aimBones, _aimBoneWeights, _aimBoneCount, _muzzle, Vector3.forward,
                                              _aimTarget.Value, _aimWeight, _aimIterations, _maxAimAngle);

            // 3) springs.
            if (Mathf.Abs(recoil) > 0.01f) Rotate(_rig.TopSpine, root.right, recoil);
            if (flinch.sqrMagnitude > 0.0001f)
            {
                var target = _flinchHead && _rig.Head != null ? _rig.Head : (_rig.Chest != null ? _rig.Chest : _rig.Spine);
                if (target != null) target.rotation = Quaternion.AngleAxis(flinch.magnitude, flinch.normalized) * target.rotation;
            }

            // 4) head.
            SolveLook(root);

            // 5) hands.
            SolveHands(root);
        }

        private void SolveLook(Transform root)
        {
            if (_lookBoneCount == 0) return;
            var head = _rig.Head;
            Vector3? point = null;
            var w = 0f;
            if (_lookTransform != null)
            {
                point = _lookTransform.position;
                w = _lookWeight;
            }
            else if (_lookPoint.HasValue)
            {
                point = _lookPoint;
                w = _lookWeight;
            }
            if (_aimWeight > 0.001f && _aimTarget.HasValue && w < _aimWeight * _headAimWeight)
            {
                // While aiming the head follows the sights unless a stronger look-at overrides it.
                point = _aimTarget;
                w = _aimWeight * _headAimWeight;
            }
            if (point.HasValue && w > 0.001f)
                AimChain.Solve(_lookBones, _lookBoneWeights, _lookBoneCount, head, _rig.HeadForwardLocal, point.Value, w, 1,
                               _maxLookAngle, 0.3f);
            if (_headPitchWeight > 0.001f && head != null)
                Rotate(head, root.right, _headPitchDegrees * _headPitchWeight);
        }

        private void SolveHands(Transform root)
        {
            var leftUsed = false;
            var rightUsed = false;
            if (_gesture != Gesture.None && _gestureWeight > 0.001f)
                SolveGesture(root, ref leftUsed, ref rightUsed);

            if (!leftUsed && _leftWeaponWeight > 0.001f && _weaponLeftGrip != null && _rig.LeftGrip != null)
            {
                var socket = _rig.LeftGrip;
                TwoBoneIK.TipPoseForSocket(_weaponLeftGrip.position, _weaponLeftGrip.rotation, socket.localPosition,
                                           socket.localRotation, _rig.LeftHand.lossyScale, out var p, out var r);
                var hint = _rig.LeftLowerArm.position - root.up * 0.3f - root.right * 0.12f;
                TwoBoneIK.Solve(_rig.LeftUpperArm, _rig.LeftLowerArm, _rig.LeftHand, p, r, _leftWeaponWeight, _leftWeaponWeight,
                                hint, _elbowHintWeight);
                LastLeftHandError = (socket.position - _weaponLeftGrip.position).magnitude;
            }
        }

        // ---------------------------------------------------------------- gestures

        private void StepGesture(float dt)
        {
            if (_gesture == Gesture.None)
            {
                _gestureWeight = 0f;
                return;
            }
            _gestureTime += dt;
            var fadeIn = Mathf.Clamp01(_gestureTime / 0.3f);
            var fadeOut = _gestureLoop ? 1f : Mathf.Clamp01((_gestureLength - _gestureTime) / 0.3f);
            _gestureWeight = Smooth(Mathf.Min(fadeIn, fadeOut));
            if (!_gestureLoop && _gestureTime >= _gestureLength)
            {
                _gesture = Gesture.None;
                _gestureWeight = 0f;
            }
        }

        private static bool GestureUsesLeft(Gesture g) =>
            g == Gesture.FistHold || g == Gesture.HandSignal || g == Gesture.HandsetToEar || g == Gesture.RaiseCamera ||
            g == Gesture.CrackChemlight;

        private void SolveGesture(Transform root, ref bool leftUsed, ref bool rightUsed)
        {
            var anchor = _rig.TopSpine != null ? _rig.TopSpine.position : root.position + root.up * 1.35f;
            var s = _rig.Height / 1.78f;
            var t = _gestureLength > 0f ? Mathf.Clamp01(_gestureTime / _gestureLength) : 0f;
            var w = _gestureWeight;
            switch (_gesture)
            {
                case Gesture.FistHold:
                    // Fist raised beside the head, forearm vertical, palm forward (GDD §8.5 row 1).
                    Hand(root, true, anchor, new Vector3(-0.21f, 0.33f, 0.10f) * s, Vector3.up, Vector3.forward, w,
                         new Vector3(-0.5f, 0.05f, 0f));
                    leftUsed = true;
                    break;
                case Gesture.HandSignal:
                {
                    // Raise beside the face, then sweep forward ("move up").
                    var k = Smooth(Mathf.InverseLerp(0.3f, 0.75f, t));
                    var p = Vector3.Lerp(new Vector3(-0.14f, 0.34f, 0.16f), new Vector3(-0.10f, 0.14f, 0.55f), k) * s;
                    var fingers = Vector3.Slerp(Vector3.up, Vector3.forward, k);
                    var palm = Vector3.Slerp(Vector3.forward, Vector3.down, k);
                    Hand(root, true, anchor, p, fingers, palm, w, new Vector3(-0.45f, -0.1f, 0f));
                    leftUsed = true;
                    break;
                }
                case Gesture.HandsetToEar:
                    // PRC-77 handset at the left ear (insertion I-6), palm toward the head.
                    Hand(root, true, anchor, new Vector3(-0.09f, 0.42f, 0.0f) * s, new Vector3(0.1f, 0.85f, -0.3f), Vector3.right, w,
                         new Vector3(-0.45f, -0.15f, 0.1f));
                    leftUsed = true;
                    break;
                case Gesture.RaiseCamera:
                    // Both hands either side of a camera in front of the face (Sparrow's Minolta hides her face).
                    Hand(root, true, anchor, new Vector3(-0.07f, 0.37f, 0.19f) * s, new Vector3(0.2f, 0.5f, 0.85f), Vector3.right, w,
                         new Vector3(-0.4f, -0.2f, 0f));
                    Hand(root, false, anchor, new Vector3(0.07f, 0.37f, 0.19f) * s, new Vector3(-0.2f, 0.5f, 0.85f), Vector3.left, w,
                         new Vector3(0.4f, -0.2f, 0f));
                    leftUsed = rightUsed = true;
                    break;
                case Gesture.CrackChemlight:
                {
                    // Hands together in front of the chest; the bend at 40 % twists the wrists.
                    var bend = Mathf.Sin(Mathf.Clamp01((t - 0.3f) / 0.2f) * Mathf.PI) * 0.6f;
                    Hand(root, true, anchor, new Vector3(-0.05f, 0.02f, 0.30f) * s, Vector3.forward,
                         Vector3.Slerp(Vector3.right, Vector3.up, bend), w, new Vector3(-0.35f, -0.3f, 0f));
                    Hand(root, false, anchor, new Vector3(0.05f, 0.02f, 0.30f) * s, Vector3.forward,
                         Vector3.Slerp(Vector3.left, Vector3.up, bend), w, new Vector3(0.35f, -0.3f, 0f));
                    leftUsed = rightUsed = true;
                    break;
                }
                case Gesture.Reach:
                    if (_reachTarget != null)
                    {
                        // Reach out, touch at mid-gesture, return (booth toggle, phone).
                        var reach = Mathf.Sin(t * Mathf.PI);
                        var local = root.InverseTransformPoint(_reachTarget.position) - root.InverseTransformPoint(anchor);
                        var fingers = local.sqrMagnitude > 1e-4f ? local.normalized : Vector3.forward;
                        Hand(root, false, anchor, local, fingers, Vector3.down, w * reach, new Vector3(0.4f, -0.3f, -0.1f));
                        rightUsed = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Puts a hand at a point given in the body frame (x right, y up, z forward, relative to <paramref name="anchor"/>)
        /// with fingers/palm directions in the body frame; <paramref name="elbowLocal"/> is the elbow hint offset.
        /// </summary>
        private void Hand(Transform root, bool left, Vector3 anchor, Vector3 localOffset, Vector3 fingersLocal, Vector3 palmLocal,
                          float weight, Vector3 elbowLocal)
        {
            var upper = left ? _rig.LeftUpperArm : _rig.RightUpperArm;
            var lower = left ? _rig.LeftLowerArm : _rig.RightLowerArm;
            var hand = left ? _rig.LeftHand : _rig.RightHand;
            if (upper == null || lower == null || hand == null) return;
            var rot = root.rotation;
            var pos = anchor + rot * localOffset;
            var fingers = rot * fingersLocal.normalized;
            var palm = rot * palmLocal.normalized;
            palm = (palm - fingers * Vector3.Dot(palm, fingers)).normalized;
            if (palm.sqrMagnitude < 1e-6f) palm = rot * Vector3.down;
            var handRot = _rig.HandRotationFor(left, fingers, palm);
            var hint = upper.position + rot * elbowLocal;
            TwoBoneIK.Solve(upper, lower, hand, pos, handRot, weight, weight, hint, 0.6f);
        }

        // ---------------------------------------------------------------- helpers

        private void UpdateDistanceCull()
        {
            var now = Time.unscaledTime;
            if (now < _nextLodCheck) return;
            _nextLodCheck = now + 0.25f + Random.value * 0.1f;
            if (_lodCamera == null || !_lodCamera.isActiveAndEnabled) _lodCamera = Camera.main;
            if (_lodCamera == null)
            {
                _distanceCulled = false;
                return;
            }
            var d2 = (_lodCamera.transform.position - transform.position).sqrMagnitude;
            _distanceCulled = d2 > _maxDistance * _maxDistance;
        }

        private static void Rotate(Transform bone, Vector3 worldAxis, float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) < 1e-4f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, worldAxis) * bone.rotation;
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);

#if UNITY_EDITOR
        /// <summary>Builders: assigns the rig.</summary>
        public void EditorSetRig(BodyRig rig) => _rig = rig;

        /// <summary>Builders: overrides the LOD distance (shadow proxy and FP never cull).</summary>
        public void EditorSetMaxDistance(float metres) => _maxDistance = metres;
#endif
    }
}
