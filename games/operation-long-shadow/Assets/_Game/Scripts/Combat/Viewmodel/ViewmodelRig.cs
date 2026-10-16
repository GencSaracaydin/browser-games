using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// Root component of a first-person weapon prefab (<c>PF_VM_&lt;Weapon&gt;</c>, built by WeaponPrefabBuilder; layer
    /// Viewmodel): references to the model, the separately animated magazine (pivot at the magazine well), the sockets
    /// exported by the art pipeline (<c>Socket_Muzzle</c>, <c>Socket_SightFront</c>, <c>Socket_SightRear</c>,
    /// <c>Socket_Grip</c>, <c>Socket_LeftHand</c>, <c>Socket_Eject</c>, <c>Socket_MagWell</c>) plus the derived IK points
    /// (magazine grip, charging handle, elbow hint), and the pose values (copied from WeaponTable's ViewmodelSpec at build
    /// time so an editor holder can fine-tune each gun in the prefab). Weapon space: barrel along +Z, up +Y, origin at
    /// the pistol grip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewmodelRig : MonoBehaviour
    {
        [SerializeField] private string _weaponId;
        [SerializeField, Tooltip("True when built from the real model (false = greybox).")]
        private bool _hasArt;

        [Header("Parts")]
        [SerializeField] private Transform _model;
        [SerializeField, Tooltip("Magazine pivot (origin at the magazine well entry).")]
        private Transform _magazine;

        [Header("Sockets (weapon space)")]
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _sightFront;
        [SerializeField] private Transform _sightRear;
        [SerializeField] private Transform _grip;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _eject;
        [SerializeField] private Transform _magWell;
        [SerializeField, Tooltip("Where the left hand holds the magazine (child of the magazine).")]
        private Transform _magGrip;
        [SerializeField, Tooltip("Charging handle / bolt / slide rear: the left hand target of the charge.")]
        private Transform _chargeHandle;
        [SerializeField, Tooltip("Left elbow hint.")]
        private Transform _supportHint;

        [Header("Pose (defaults from WeaponTable ViewmodelSpec)")]
        [SerializeField] private Vector3 _hipPosition;
        [SerializeField] private Vector3 _hipEuler;
        [SerializeField, Tooltip("Eye to rear sight distance at full ADS (metres).")]
        private float _eyeRelief = 0.2f;
        [SerializeField] private Vector3 _adsOffset;
        [SerializeField] private Vector3 _sprintPosition;
        [SerializeField] private Vector3 _sprintEuler;
        [SerializeField] private Vector3 _crouchPosition;
        [SerializeField] private Vector3 _crouchEuler;
        [SerializeField] private Vector3 _slidePosition;
        [SerializeField] private Vector3 _slideEuler;
        [SerializeField] private Vector3 _loweredPosition;
        [SerializeField] private Vector3 _loweredEuler;
        [SerializeField] private float _adsViewmodelFov = 45f;
        [SerializeField] private float _swayScale = 1f;
        [SerializeField] private float _bobScale = 1f;
        [SerializeField, Range(0f, 1f)] private float _heft = 0.3f;
        [SerializeField] private float _muzzleFlashScale = 1f;
        [SerializeField] private string _casingId;
        [SerializeField, Tooltip("Left hand stays on the weapon while sprinting (false: pistols run one-handed).")]
        private bool _twoHandedSprint = true;

        private Renderer[] _magRenderers;
        private Vector3 _magRestPosition;
        private Quaternion _magRestRotation = Quaternion.identity;
        private Vector3 _localSightRear;
        private Vector3 _localSightFront;
        private bool _initialised;
        private bool _magVisible = true;

        /// <summary>Weapon id.</summary>
        public string WeaponId => _weaponId;
        /// <summary>True when built from the real model.</summary>
        public bool HasArt => _hasArt;
        /// <summary>Model root.</summary>
        public Transform Model => _model;
        /// <summary>Magazine pivot (may be null for the door gun).</summary>
        public Transform Magazine => _magazine;
        /// <summary>Muzzle socket.</summary>
        public Transform Muzzle => _muzzle;
        /// <summary>Right-hand grip socket.</summary>
        public Transform Grip => _grip;
        /// <summary>Left-hand handguard socket.</summary>
        public Transform LeftHand => _leftHand;
        /// <summary>Ejection port socket.</summary>
        public Transform Eject => _eject;
        /// <summary>Magazine grip (left hand during reloads).</summary>
        public Transform MagGrip => _magGrip != null ? _magGrip : _magazine;
        /// <summary>Charging handle point.</summary>
        public Transform ChargeHandle => _chargeHandle;
        /// <summary>Left elbow hint.</summary>
        public Transform SupportHint => _supportHint;
        /// <summary>View-model camera vertical FOV at full ADS.</summary>
        public float AdsViewmodelFov => _adsViewmodelFov;
        /// <summary>Weight feel 0..1.</summary>
        public float Heft => _heft;
        /// <summary>Muzzle flash scale (0 = suppressed).</summary>
        public float MuzzleFlashScale => _muzzleFlashScale;
        /// <summary>Casing id ejected per shot.</summary>
        public string CasingId => _casingId;
        /// <summary>Left hand stays on during sprint.</summary>
        public bool TwoHandedSprint => _twoHandedSprint;

        /// <summary>
        /// Editor/runtime setup: assigns parts and sockets and copies the pose values from the weapon spec. Missing sockets
        /// are created from WeaponTable's defaults.
        /// </summary>
        public void Configure(string weaponId, bool hasArt, Transform model, Transform magazine, WeaponSpec spec)
        {
            _weaponId = weaponId;
            _hasArt = hasArt;
            _model = model != null ? model : transform;
            _magazine = magazine;
            var sockets = spec?.Viewmodel != null ? spec.Viewmodel.Sockets : WeaponGreybox.SocketsFor(weaponId);
            _muzzle = Socket("Muzzle", sockets.Muzzle);
            _sightFront = Socket("SightFront", sockets.SightFront);
            _sightRear = Socket("SightRear", sockets.SightRear);
            _grip = Socket("Grip", sockets.Grip);
            _leftHand = Socket("LeftHand", sockets.LeftHand);
            _eject = Socket("Eject", sockets.Eject);
            _magWell = Socket("MagWell", sockets.MagWell);
            var kind = spec?.Kind ?? WeaponKind.Rifle;
            _chargeHandle = Derived("IK_ChargeHandle", ChargePoint(kind, sockets));
            _supportHint = Derived("IK_SupportHint", sockets.LeftHand + new Vector3(-0.16f, -0.22f, -0.12f));
            if (_magazine != null)
            {
                var mg = _magazine.Find("IK_MagGrip");
                if (mg == null)
                {
                    mg = new GameObject("IK_MagGrip").transform;
                    mg.SetParent(_magazine, false);
                    mg.localPosition = kind == WeaponKind.Pistol ? new Vector3(-0.012f, -0.07f, 0.01f) :
                        kind == WeaponKind.Lmg ? new Vector3(-0.07f, -0.08f, 0.02f) : new Vector3(-0.018f, -0.085f, 0.04f);
                    mg.localRotation = Quaternion.Euler(0f, 0f, -25f);
                }
                _magGrip = mg;
            }
            if (spec != null) ApplySpec(spec);
            _initialised = false;
        }

        /// <summary>Copies the pose values of a weapon spec (builder; "Reset Pose From WeaponTable" in the inspector).</summary>
        public void ApplySpec(WeaponSpec spec)
        {
            var v = spec?.Viewmodel;
            if (v == null) return;
            _hipPosition = v.HipPosition;
            _hipEuler = v.HipEuler;
            _eyeRelief = v.EyeRelief;
            _adsOffset = v.AdsOffset;
            _sprintPosition = v.SprintPosition;
            _sprintEuler = v.SprintEuler;
            _crouchPosition = v.CrouchPosition;
            _crouchEuler = v.CrouchEuler;
            _slidePosition = v.SlidePosition;
            _slideEuler = v.SlideEuler;
            _loweredPosition = v.LoweredPosition;
            _loweredEuler = v.LoweredEuler;
            _adsViewmodelFov = v.AdsViewmodelFov;
            _swayScale = v.SwayScale;
            _bobScale = v.BobScale;
            _heft = v.Heft;
            _muzzleFlashScale = v.MuzzleFlashScale;
            _casingId = v.CasingId;
            _twoHandedSprint = spec.Kind != WeaponKind.Pistol;
        }

        /// <summary>The pose values in the form ViewmodelMotion consumes.</summary>
        public ViewmodelMotion.PoseSet Pose(float sprintOutTime)
        {
            Init();
            return new ViewmodelMotion.PoseSet
            {
                HipPosition = _hipPosition, HipEuler = _hipEuler, EyeRelief = _eyeRelief, AdsOffset = _adsOffset,
                SightRear = _localSightRear, SightFront = _localSightFront,
                SprintPosition = _sprintPosition, SprintEuler = _sprintEuler, SprintOutTime = Mathf.Max(0.05f, sprintOutTime),
                CrouchPosition = _crouchPosition, CrouchEuler = _crouchEuler, SlidePosition = _slidePosition, SlideEuler = _slideEuler,
                LoweredPosition = _loweredPosition, LoweredEuler = _loweredEuler, SwayScale = _swayScale, BobScale = _bobScale,
            };
        }

        /// <summary>Caches rest poses and renderer lists (idempotent).</summary>
        public void Init()
        {
            if (_initialised) return;
            _initialised = true;
            if (_magazine != null)
            {
                _magRestPosition = _magazine.localPosition;
                _magRestRotation = _magazine.localRotation;
                _magRenderers = _magazine.GetComponentsInChildren<Renderer>(true);
            }
            _localSightRear = _sightRear != null ? transform.InverseTransformPoint(_sightRear.position) : new Vector3(0f, 0.1f, 0f);
            _localSightFront = _sightFront != null ? transform.InverseTransformPoint(_sightFront.position) : _localSightRear + Vector3.forward * 0.3f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off; // TECH_PLAN D5: receives, never casts
                r.receiveShadows = true;
            }
        }

        /// <summary>Applies the reload pose to the magazine (offset in weapon space from its seat).</summary>
        public void PoseMagazine(Vector3 offset, Vector3 euler, bool visible)
        {
            if (_magazine == null) return;
            Init();
            _magazine.localPosition = _magRestPosition + offset;
            _magazine.localRotation = Quaternion.Euler(euler) * _magRestRotation;
            if (visible == _magVisible || _magRenderers == null) return;
            _magVisible = visible;
            for (var i = 0; i < _magRenderers.Length; i++)
                if (_magRenderers[i] != null) _magRenderers[i].enabled = visible;
        }

        /// <summary>Puts the magazine back in its seat, visible.</summary>
        public void ResetMagazine() => PoseMagazine(Vector3.zero, Vector3.zero, true);

        private Transform Socket(string name, Vector3 fallback)
        {
            var root = _model != null ? _model : transform;
            var t = CombatUtil.FindAny(root, "Socket_" + name, name);
            if (t != null) return t;
            t = new GameObject("Socket_" + name).transform;
            t.SetParent(root, false);
            t.localPosition = fallback;
            return t;
        }

        private Transform Derived(string name, Vector3 position)
        {
            var t = transform.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(transform, false);
            }
            t.localPosition = position;
            t.localRotation = Quaternion.identity;
            return t;
        }

        /// <summary>The left hand's charge point for a weapon kind from its sockets.</summary>
        public static Vector3 ChargePoint(WeaponKind kind, WeaponSockets s)
        {
            switch (kind)
            {
                case WeaponKind.Pistol: return new Vector3(-0.005f, s.SightRear.y - 0.02f, s.SightRear.z + 0.02f); // slide serrations
                case WeaponKind.Lmg:
                case WeaponKind.Rifle:
                    // AK pattern: the bolt handle is on the right of the receiver above the magazine; the COMMANDO's
                    // charging handle sits behind the carry handle.
                    return s.SightRear.y > 0.12f ? new Vector3(0f, s.SightRear.y - 0.035f, s.SightRear.z - 0.05f)
                                                 : new Vector3(0.035f, s.Eject.y + 0.005f, s.Eject.z + 0.04f);
                default: return s.Eject;
            }
        }
    }
}
