using System;
using LongShadow.Characters.Animation;
using LongShadow.Characters.Body;
using LongShadow.Characters.IK;
using LongShadow.Characters.Weapons;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.FirstPerson
{
    /// <summary>
    /// First-person Calder (<c>PFB_Calder_FP</c>, TECH_PLAN D19, GDD §12): arms and upper chest on the Viewmodel layer
    /// with head, neck and both thighs scaled to 0; both arms two-bone IK onto the current view model's grips
    /// (<see cref="IWeaponGrips"/>, WP03), the left hand following the magazine during reloads through
    /// <c>LeftGrip</c>/<c>LeftHandWeight</c>; finger grip poses on the Hands layer; MID-A watch check (GDD §8.5 row 3,
    /// 1.2 s), climax pistol rise (GDD §8.7 row 3, 3 s) and outro fists. Runs in LateUpdate after the view model
    /// (<see cref="ExecOrder.Weapons"/> + 50) so the hands never lag the gun.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Weapons + 50)]
    [DisallowMultipleComponent]
    public sealed class FPArmsIK : MonoBehaviour, IFistsTarget
    {
        [SerializeField] private BodyRig _rig;
        [SerializeField] private Animator _animator;
        [SerializeField, Tooltip("Bones scaled to 0 every frame (head, neck, thighs; D19).")]
        private Transform[] _hiddenBones = new Transform[0];
        [SerializeField, Tooltip("Camera position relative to the head pivot in the bind pose, body space (eyes).")]
        private Vector3 _eyeFromHead = new Vector3(0f, 0.09f, 0.09f);
        [SerializeField, Tooltip("Extra camera offset in camera space (pull the shoulders slightly back/down).")]
        private Vector3 _cameraOffset = new Vector3(0f, 0.015f, 0.02f);
        [SerializeField, Tooltip("Rotate the body with the camera (yaw and pitch) so the shoulders stay behind the gun at any pitch.")]
        private bool _followCameraRotation = true;
        [SerializeField] private float _gripBlendSpeed = 12f;
        [SerializeField, Tooltip("IK reach softness (fraction of arm length).")]
        private float _softness = 0.015f;

        private ServiceRef<IWeaponGrips> _grips;
        private ServiceRef<IPlayerRig> _rigService;
        private ServiceRef<IWeaponService> _weapons;
        private float _rightWeight;
        private float _leftWeight;
        private HandPose _handPose = HandPose.Relaxed;
        private bool _fistsOverride;
        private bool _fistsClenched;

        // scripted actions
        private float _watchTime = -1f, _watchLength;
        private Action _watchDone;
        private float _riseTime = -1f, _riseLength;
        private Action _riseDone;
        private Transform _riseTarget;
        private Vector3 _riseBasePos, _riseWrittenPos;
        private Quaternion _riseBaseRot, _riseWrittenRot;
        private bool _riseWritten;

        /// <summary>True while the watch check or the pistol rise plays.</summary>
        public bool IsPlaying => _watchTime >= 0f || _riseTime >= 0f;
        /// <summary>Last right-hand error to the grip (m).</summary>
        public float LastRightError { get; private set; }
        /// <summary>Last left-hand error to the grip (m).</summary>
        public float LastLeftError { get; private set; }

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<BodyRig>();
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_rig != null) _rig.EnsureResolved();
        }

        private void OnEnable() => FirstPersonBodyHub.Instance.AttachArms(this);

        private void OnDisable()
        {
            FirstPersonBodyHub.Instance.DetachArms(this);
            CompleteWatch();
            CompleteRise(true);
        }

        // ------------------------------------------------------------------ IFirstPersonBody (via the hub)

        /// <summary>Watch check: the left wrist turns up into view and back (MID-A 0:12, 1.2 s).</summary>
        public void PlayWatchCheck(float seconds, Action onDone)
        {
            CompleteWatch();
            _watchLength = Mathf.Max(0.4f, seconds);
            _watchTime = 0f;
            _watchDone = onDone;
        }

        /// <summary>Pistol rise: the view model and the right arm come up from below over <paramref name="seconds"/>.</summary>
        public void PlayPistolRise(float seconds, Action onDone)
        {
            CompleteRise(true);
            _riseLength = Mathf.Max(0.3f, seconds);
            _riseTime = 0f;
            _riseDone = onDone;
            _riseWritten = false;
        }

        /// <inheritdoc/>
        public void SetFists(bool clenched)
        {
            _fistsOverride = true;
            _fistsClenched = clenched;
        }

        /// <summary>Clears the fists override (back to the weapon's grip pose).</summary>
        public void ClearFists() => _fistsOverride = false;

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            if (_rig == null || !_rig.IsValid) return;
            var dt = Time.deltaTime;
            foreach (var b in _hiddenBones)
                if (b != null && b.localScale != Vector3.zero) b.localScale = Vector3.zero;

            var cam = CameraTransform();
            if (cam != null) AlignToCamera(cam);

            var grips = _grips.Value;
            var weaponVisible = grips != null && grips.RightGrip != null && grips.RightGrip.gameObject.activeInHierarchy;
            UpdateHandPose(weaponVisible);

            // Pistol rise moves the whole view model before the hands are solved.
            if (_riseTime >= 0f) StepRise(dt, grips, cam);

            _rightWeight = MathX.Damp(_rightWeight, weaponVisible ? 1f : 0f, _gripBlendSpeed, dt);
            var leftTarget = weaponVisible ? Mathf.Clamp01(grips.LeftHandWeight) : 0f;
            _leftWeight = MathX.Damp(_leftWeight, leftTarget, _gripBlendSpeed, dt);

            if (_rightWeight > 0.001f && grips != null && grips.RightGrip != null && _rig.RightGrip != null)
            {
                var s = _rig.RightGrip;
                TwoBoneIK.TipPoseForSocket(grips.RightGrip.position, grips.RightGrip.rotation, s.localPosition, s.localRotation,
                                           _rig.RightHand.lossyScale, out var p, out var r);
                var hint = _rig.RightLowerArm.position + Down(cam) * 0.25f + Right(cam) * 0.1f;
                TwoBoneIK.Solve(_rig.RightUpperArm, _rig.RightLowerArm, _rig.RightHand, p, r, _rightWeight, _rightWeight, hint, 0.4f,
                                _softness);
                LastRightError = (s.position - grips.RightGrip.position).magnitude;
            }

            SolveLeft(dt, grips, cam);
            StepWatch(dt);
        }

        private void SolveLeft(float dt, IWeaponGrips grips, Transform cam)
        {
            var hasGrip = grips != null && grips.LeftGrip != null && _rig.LeftGrip != null;
            var watch = WatchEnvelope();
            var weight = Mathf.Max(_leftWeight, watch);
            if (weight <= 0.001f) return;

            Vector3 p;
            Quaternion r;
            if (hasGrip)
            {
                var s = _rig.LeftGrip;
                TwoBoneIK.TipPoseForSocket(grips.LeftGrip.position, grips.LeftGrip.rotation, s.localPosition, s.localRotation,
                                           _rig.LeftHand.lossyScale, out p, out r);
            }
            else
            {
                p = _rig.LeftHand.position;
                r = _rig.LeftHand.rotation;
            }
            if (watch > 0f && cam != null)
            {
                // Wrist turned toward the eyes, watch face (back of the wrist) to camera (GDD §8.5: "the Seiko reads 18:40").
                var cr = cam.rotation;
                var wp = cam.position + cr * new Vector3(-0.07f, -0.12f, 0.30f);
                var fingers = cr * new Vector3(1f, 0.28f, 0.12f).normalized;
                var palm = cr * new Vector3(0f, -0.3f, 1f).normalized;
                palm = (palm - fingers * Vector3.Dot(palm, fingers)).normalized;
                var wr = _rig.HandRotationFor(true, fingers, palm);
                var blend = hasGrip ? watch : 1f;
                p = Vector3.Lerp(p, wp, blend);
                r = Quaternion.Slerp(r, wr, blend);
            }
            var hint = grips != null && grips.SupportHint != null && watch < 0.5f
                ? grips.SupportHint.position
                : _rig.LeftLowerArm.position + Down(cam) * 0.3f - Right(cam) * 0.15f;
            TwoBoneIK.Solve(_rig.LeftUpperArm, _rig.LeftLowerArm, _rig.LeftHand, p, r, weight, weight, hint, 0.5f, _softness);
            if (hasGrip && watch <= 0f) LastLeftError = (_rig.LeftGrip.position - grips.LeftGrip.position).magnitude;
        }

        private void UpdateHandPose(bool weaponVisible)
        {
            HandPose pose;
            if (_fistsOverride) pose = _fistsClenched ? HandPose.Fist : HandPose.Flat;
            else if (!weaponVisible) pose = HandPose.Relaxed;
            else
            {
                var id = _weapons.Value != null ? _weapons.Value.CurrentWeaponId : null;
                WeaponHoldTable.TryGet(id, out var row);
                pose = row.HandPose == HandPose.Relaxed ? HandPose.RifleGrip : row.HandPose;
            }
            if (pose == _handPose && _animator != null) return;
            _handPose = pose;
            if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
                _animator.SetInteger(CharacterAnimIds.HandPoseId, (int)pose);
        }

        private void AlignToCamera(Transform cam)
        {
            var head = _rig.Head;
            if (head == null) return;
            // The camera sits at the eyes: turn the body with the view, then move it so the head pivot is where the eyes imply.
            var root = transform;
            if (_followCameraRotation) root.rotation = cam.rotation;
            var desiredEye = cam.position + cam.rotation * _cameraOffset;
            var eye = head.position + root.rotation * _eyeFromHead;
            root.position += desiredEye - eye;
        }

        private Transform CameraTransform()
        {
            var r = _rigService.Value;
            if (r != null)
            {
                if (r.ViewmodelCamera != null) return r.ViewmodelCamera.transform;
                if (r.Eye != null) return r.Eye;
            }
            var main = Camera.main;
            return main != null ? main.transform : null;
        }

        private static Vector3 Down(Transform cam) => cam != null ? -cam.up : Vector3.down;
        private static Vector3 Right(Transform cam) => cam != null ? cam.right : Vector3.right;

        // ------------------------------------------------------------------ watch check

        private float WatchEnvelope()
        {
            if (_watchTime < 0f) return 0f;
            var t = _watchTime / _watchLength;
            // Up in the first 30 %, held, down in the last 25 %.
            var up = Mathf.Clamp01(t / 0.3f);
            var down = Mathf.Clamp01((1f - t) / 0.25f);
            var x = Mathf.Min(up, down);
            return x * x * (3f - 2f * x);
        }

        private void StepWatch(float dt)
        {
            if (_watchTime < 0f) return;
            _watchTime += dt;
            if (_watchTime >= _watchLength) CompleteWatch();
        }

        private void CompleteWatch()
        {
            if (_watchTime < 0f && _watchDone == null) return;
            _watchTime = -1f;
            var cb = _watchDone;
            _watchDone = null;
            SafeInvoke(cb);
        }

        // ------------------------------------------------------------------ pistol rise

        private void StepRise(float dt, IWeaponGrips grips, Transform cam)
        {
            _riseTime += dt;
            var target = FindViewmodelRoot(grips);
            if (target != _riseTarget)
            {
                RestoreRiseTarget();
                _riseTarget = target;
                _riseWritten = false;
            }
            if (_riseTarget != null && cam != null)
            {
                // Start from what WP03 wrote this frame (or our previous base if it did not write).
                var pos = _riseTarget.position;
                var rot = _riseTarget.rotation;
                if (_riseWritten && pos == _riseWrittenPos && rot == _riseWrittenRot)
                {
                    pos = _riseBasePos;
                    rot = _riseBaseRot;
                }
                _riseBasePos = pos;
                _riseBaseRot = rot;
                var t = Mathf.Clamp01(_riseTime / _riseLength);
                var lowered = 1f - t * t * (3f - 2f * t); // slow, steady (GDD §8.7: over 3 s)
                var pivot = cam.position;
                var q = Quaternion.AngleAxis(48f * lowered, cam.right);
                var offset = (-cam.up * 0.18f - cam.forward * 0.04f) * lowered;
                var newPos = pivot + q * (pos - pivot) + offset;
                var newRot = q * rot;
                _riseTarget.SetPositionAndRotation(newPos, newRot);
                _riseWrittenPos = newPos;
                _riseWrittenRot = newRot;
                _riseWritten = true;
            }
            if (_riseTime >= _riseLength) CompleteRise(false);
        }

        private Transform FindViewmodelRoot(IWeaponGrips grips)
        {
            if (grips == null || grips.RightGrip == null) return null;
            var stop = _rigService.Value != null ? _rigService.Value.ViewmodelRoot : null;
            var t = grips.RightGrip;
            while (t.parent != null && t.parent != stop && t.parent != transform.parent) t = t.parent;
            return t;
        }

        private void RestoreRiseTarget()
        {
            if (_riseTarget != null && _riseWritten && _riseTarget.position == _riseWrittenPos)
                _riseTarget.SetPositionAndRotation(_riseBasePos, _riseBaseRot);
            _riseWritten = false;
        }

        private void CompleteRise(bool restore)
        {
            if (_riseTime < 0f && _riseDone == null) return;
            if (restore) RestoreRiseTarget();
            _riseTime = -1f;
            _riseTarget = null;
            _riseWritten = false;
            var cb = _riseDone;
            _riseDone = null;
            SafeInvoke(cb);
        }

        private void SafeInvoke(Action cb)
        {
            if (cb == null) return;
            try
            {
                cb();
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Characters, e, this);
            }
        }

#if UNITY_EDITOR
        /// <summary>Builders: wiring and the bind-pose eye offset.</summary>
        public void EditorSetup(BodyRig rig, Animator animator, Transform[] hiddenBones, Vector3 eyeFromHead)
        {
            _rig = rig;
            _animator = animator;
            _hiddenBones = hiddenBones ?? new Transform[0];
            _eyeFromHead = eyeFromHead;
        }
#endif
    }
}
