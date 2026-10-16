using LongShadow.Characters.Animation;
using LongShadow.Characters.Body;
using LongShadow.Characters.FirstPerson;
using LongShadow.Characters.IK;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Booth
{
    /// <summary>
    /// Booth bodies in <c>S0_Booth</c> (GDD §8.0, §8.2, §8.6, §8.8): <c>PFB_Calder_Booth</c> seated and cuffed in the
    /// chair, and <c>PFB_Interrogator</c>. Runs on unscaled time (R3: inserts freeze gameplay time).
    /// <list type="bullet">
    /// <item>Calder: wrists IK'd to the cuff points on the armrests, fists clench/unclench through
    /// <see cref="IFirstPersonBody.SetFists"/> (outro rows 2 and 5), head hidden while a camera sits in it (C_POV), the
    /// "Session 11" shaved-head variant for MID-B shots 4-6.</item>
    /// <item>Interrogator: stands by default; <c>ReachToggle</c> reaches the right hand to <c>BOOTH.Scrambler</c> (outro
    /// row 7), <c>PhoneTalk</c> brings the handset to the ear (row 8).</item>
    /// </list>
    /// SendMessage targets (no assembly reference needed): <c>SetSession11(bool)</c>, <c>SetHeadVisible(bool)</c>,
    /// <c>SetCuffed(bool)</c>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Squad + 30)]
    [DisallowMultipleComponent]
    public sealed class BoothBody : MonoBehaviour, IFistsTarget
    {
        /// <summary>Marker the interrogator reaches for (Appendix A).</summary>
        public const string ScramblerMarker = "BOOTH.Scrambler";
        /// <summary>Desk phone marker (Appendix A).</summary>
        public const string PhoneMarker = "BOOTH.Phone";

        [SerializeField] private BodyRig _rig;
        [SerializeField] private HumanoidBody _body;
        [SerializeField] private Animator _animator;
        [SerializeField, Tooltip("Seated, cuffed Calder (true) or the interrogator (false).")]
        private bool _isCalder = true;
        [Header("Cuffs (body-local, chair origin = body root, GDD §8.0)")]
        [SerializeField] private bool _cuffed = true;
        [SerializeField] private Vector3 _leftCuffLocal = new Vector3(-0.235f, 0.64f, 0.17f);
        [SerializeField] private Vector3 _rightCuffLocal = new Vector3(0.235f, 0.64f, 0.17f);
        [Header("Session 11 head (MID-B)")]
        [SerializeField] private Renderer _headRenderer;
        [SerializeField] private int _headMaterialIndex;
        [SerializeField] private Material _session12Head;
        [SerializeField] private Material _session11Head;
        [SerializeField, Tooltip("Hair renderers hidden in the shaved Session 11 variant.")]
        private Renderer[] _hair = new Renderer[0];
        [Header("POV")]
        [SerializeField, Tooltip("Hide the head while any enabled camera is this close to the eyes (C_POV at 0, 1.18, 0.05).")]
        private float _povHideDistance = 0.3f;

        private ServiceRef<IMarkerRegistry> _markers;
        private bool _session11;
        private bool _headForcedVisible = true;
        private bool _headHidden;
        private Vector3 _headScale = Vector3.one;
        private Camera[] _cameras = new Camera[8];
        private bool _reachBound;

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<BodyRig>();
            if (_body == null) _body = GetComponent<HumanoidBody>();
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator != null) _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (_rig != null && _rig.Head != null) _headScale = _rig.Head.localScale;
        }

        private void OnEnable()
        {
            if (_isCalder) FirstPersonBodyHub.Instance.AttachFists(this);
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetInteger(CharacterAnimIds.IdleSetId, _isCalder ? 1 : 0);
            _reachBound = false;
        }

        private void OnDisable()
        {
            if (_isCalder) FirstPersonBodyHub.Instance.DetachFists(this);
        }

        private void Start()
        {
            if (_isCalder && _body != null) _body.PlayAction(ExtraActionIds.SitIdle);
        }

        // ------------------------------------------------------------------ API

        /// <inheritdoc/>
        public void SetFists(bool clenched)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetInteger(CharacterAnimIds.HandPoseId, (int)(clenched ? HandPose.Fist : HandPose.Flat));
        }

        /// <summary>Shaved, gaunt "Session 11" head (MID-B shots 4-6) or back. SendMessage target.</summary>
        public void SetSession11(bool on)
        {
            _session11 = on;
            if (_headRenderer != null)
            {
                var mat = on ? _session11Head : _session12Head;
                if (mat != null)
                {
                    var mats = _headRenderer.sharedMaterials;
                    if (_headMaterialIndex >= 0 && _headMaterialIndex < mats.Length)
                    {
                        mats[_headMaterialIndex] = mat;
                        _headRenderer.sharedMaterials = mats;
                    }
                }
                else if (on)
                    LSLog.MissingAsset(LogCategory.Characters, "M_CalderReel_Head", "Assets/_Game/Art/Materials/Characters/Calder/", this);
            }
            foreach (var h in _hair)
                if (h != null) h.enabled = !on;
        }

        /// <summary>
        /// false hides the head (scale 0); true returns to the default: Calder's head hides itself while a camera sits in
        /// it (C_POV), the interrogator's stays visible. SendMessage target.
        /// </summary>
        public void SetHeadVisible(bool visible) => _headForcedVisible = visible;

        /// <summary>Wrists pinned to the armrest cuffs (Calder). SendMessage target.</summary>
        public void SetCuffed(bool cuffed) => _cuffed = cuffed;

        /// <summary>True in the Session 11 state.</summary>
        public bool IsSession11 => _session11;

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            if (_rig == null || !_rig.IsValid) return;
            BindReachTarget();
            UpdateHeadVisibility();
            if (_isCalder && _cuffed) SolveCuffs();
        }

        private void BindReachTarget()
        {
            if (_reachBound || _isCalder || _body == null) return;
            var reg = _markers.Value;
            if (reg == null || !reg.Has(ScramblerMarker)) return;
            _body.SetReachTarget(reg.Get(ScramblerMarker));
            _reachBound = true;
        }

        private void UpdateHeadVisibility()
        {
            var head = _rig.Head;
            if (head == null) return;
            var hide = false;
            if (!_headForcedVisible || _isCalder)
            {
                var eyes = _rig.Eyes != null ? _rig.Eyes.position : head.position;
                var n = Camera.allCamerasCount;
                if (n > _cameras.Length) _cameras = new Camera[n + 4];
                n = Camera.GetAllCameras(_cameras);
                var limit = _povHideDistance * _povHideDistance;
                for (var i = 0; i < n; i++)
                {
                    var c = _cameras[i];
                    if (c != null && c.isActiveAndEnabled && (c.transform.position - eyes).sqrMagnitude < limit)
                    {
                        hide = true;
                        break;
                    }
                }
                if (!_headForcedVisible) hide = true;
            }
            if (hide == _headHidden && head.localScale == (hide ? Vector3.zero : _headScale)) return;
            _headHidden = hide;
            head.localScale = hide ? Vector3.zero : _headScale;
        }

        private void SolveCuffs()
        {
            var root = transform;
            Pin(true, root.TransformPoint(_leftCuffLocal), root);
            Pin(false, root.TransformPoint(_rightCuffLocal), root);
        }

        private void Pin(bool left, Vector3 wrist, Transform root)
        {
            var upper = left ? _rig.LeftUpperArm : _rig.RightUpperArm;
            var lower = left ? _rig.LeftLowerArm : _rig.RightLowerArm;
            var hand = left ? _rig.LeftHand : _rig.RightHand;
            if (upper == null || lower == null || hand == null) return;
            // Forearms along the armrests, palms down; elbows out and back.
            var fingers = root.forward;
            var palm = -root.up;
            var rot = _rig.HandRotationFor(left, fingers, palm);
            var hint = lower.position + root.right * (left ? -0.25f : 0.25f) - root.forward * 0.2f;
            TwoBoneIK.Solve(upper, lower, hand, wrist, rot, 1f, 0.85f, hint, 0.5f);
        }

#if UNITY_EDITOR
        /// <summary>Builders: wiring.</summary>
        public void EditorSetup(BodyRig rig, HumanoidBody body, Animator animator, bool isCalder, Renderer headRenderer,
                                int headMaterialIndex, Material session12Head, Material session11Head, Renderer[] hair)
        {
            _rig = rig;
            _body = body;
            _animator = animator;
            _isCalder = isCalder;
            _cuffed = isCalder;
            _headRenderer = headRenderer;
            _headMaterialIndex = headMaterialIndex;
            _session12Head = session12Head;
            _session11Head = session11Head;
            _hair = hair ?? new Renderer[0];
        }
#endif
    }
}
