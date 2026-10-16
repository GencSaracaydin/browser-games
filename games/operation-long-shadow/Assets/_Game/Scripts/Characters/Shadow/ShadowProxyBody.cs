using LongShadow.Characters.Animation;
using LongShadow.Characters.Body;
using LongShadow.Characters.IK;
using LongShadow.Characters.Weapons;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Characters.Shadow
{
    /// <summary>
    /// <c>PFB_Calder_Shadow</c> (TECH_PLAN D19): the full Calder body that renders <see cref="ShadowCastingMode.ShadowsOnly"/>
    /// on layer Player so the player casts a correct sun shadow (the MID-A "LONG SHADOW" image, GDD §8.5 row 1).
    /// <para><b>Player mode</b> (default): WP02's <c>PlayerShadowProxy</c> moves the root and drives the 5-state
    /// <c>AC_ShadowProxy</c> (Speed, Crouch, Aim, Airborne). This component carries the current weapon (so the gun casts
    /// a shadow too), aims it along the view while ADS and tilts the head with the view. With <c>_selfDrive</c> on it
    /// also follows <see cref="IPlayerRig"/> itself (useful before WP02 lands).</para>
    /// <para><b>Cinematic mode</b> (insertion cold open, CINEMATIC_DIRECTION §2): visible, driven as an
    /// <see cref="ICharacterBody"/> through its <see cref="HumanoidBody"/> with <c>AC_Soldier</c>/<c>AOC_Squad</c>.
    /// Entered automatically when anything calls the body's <see cref="ICharacterBody"/> API, or with
    /// <c>SendMessage("SetCinematicMode", true)</c>; <c>SendMessage("SetRenderVisible", bool)</c> only toggles visibility.</para>
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Player + 10)]
    [DisallowMultipleComponent]
    public sealed class ShadowProxyBody : MonoBehaviour
    {
        [SerializeField] private BodyRig _rig;
        [SerializeField] private BodyIK _ik;
        [SerializeField] private BodyWeaponHolder _weapon;
        [SerializeField] private Animator _animator;
        [SerializeField] private HumanoidBody _cinematicBody;
        [SerializeField, Tooltip("AC_ShadowProxy (player mode).")]
        private RuntimeAnimatorController _shadowController;
        [SerializeField, Tooltip("AOC_Squad over AC_Soldier (cinematic mode).")]
        private RuntimeAnimatorController _cinematicController;
        [SerializeField, Tooltip("Follow IPlayerRig and drive the animator here (only when WP02's PlayerShadowProxy is absent).")]
        private bool _selfDrive;
        [SerializeField, Tooltip("Render visibly (cameras see it) instead of shadows only.")]
        private bool _visible;

        private ServiceRef<IPlayerRig> _player;
        private ServiceRef<IWeaponService> _weapons;
        private bool _cinematic;
        private string _shownWeapon;

        /// <summary>True in cinematic mode.</summary>
        public bool IsCinematic => _cinematic;

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<BodyRig>();
            if (_ik == null) _ik = GetComponent<BodyIK>();
            if (_weapon == null) _weapon = GetComponent<BodyWeaponHolder>();
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_cinematicBody == null) _cinematicBody = GetComponent<HumanoidBody>();
        }

        private void OnEnable()
        {
            GameEvents.WeaponChanged += OnWeaponChanged;
            if (!_cinematic && _cinematicBody != null) _cinematicBody.enabled = false;
            ApplyRenderMode();
            SyncWeapon();
        }

        private void OnDisable() => GameEvents.WeaponChanged -= OnWeaponChanged;

        /// <summary>Visible (true) or shadows only (false). SendMessage target.</summary>
        public void SetRenderVisible(bool visible)
        {
            _visible = visible;
            ApplyRenderMode();
        }

        /// <summary>Switches to the insertion's third-person mode (visible, ICharacterBody-driven) or back. SendMessage target.</summary>
        public void SetCinematicMode(bool on)
        {
            if (_cinematic == on) return;
            _cinematic = on;
            _visible = on;
            if (_animator != null)
            {
                var c = on ? _cinematicController : _shadowController;
                if (c != null) _animator.runtimeAnimatorController = c;
                else LSLog.WarnOnce("shadow-noctrl:" + on, LogCategory.Characters,
                    name + ": no " + (on ? "cinematic" : "shadow") + " controller assigned; keeping the current one.", this);
            }
            if (_cinematicBody != null) _cinematicBody.enabled = on;
            if (_ik != null)
            {
                _ik.SetAim(null);
                _ik.SetLookPoint(null, 0f);
            }
            ApplyRenderMode();
        }

        private void ApplyRenderMode()
        {
            var mode = _visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = mode;
            if (_weapon != null) _weapon.RefreshRenderSettings();
        }

        private void OnWeaponChanged(string weaponId)
        {
            if (!_cinematic) SyncWeapon();
        }

        private void SyncWeapon()
        {
            if (_weapon == null || _cinematic) return;
            var svc = _weapons.Value;
            var id = svc != null && !svc.IsHolstered ? svc.CurrentWeaponId : null;
            if (svc == null) id = _shownWeapon ?? WeaponIds.Commando; // before WP03 lands: the starting primary (GDD §13.1)
            if (id == _shownWeapon && (_weapon.Instance != null || id == null)) return;
            _shownWeapon = id;
            _weapon.SetWeapon(id);
            if (_ik != null)
            {
                _ik.SetWeapon(_weapon.IsHolding ? _weapon.Muzzle : null, _weapon.LeftGrip);
                _ik.LeftHandOnWeapon = _weapon.IsHolding;
            }
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetInteger(CharacterAnimIds.HoldId, (int)_weapon.Hold);
                _animator.SetInteger(CharacterAnimIds.HandPoseId, (int)_weapon.HandPose);
                // AC_ShadowProxy's Arms layer carries the rifle hold; unarmed (holstered, cinematics) the locomotion arms swing.
                var arms = _animator.GetLayerIndex(CharacterAnimIds.LayerArms);
                if (arms >= 0) _animator.SetLayerWeight(arms, _weapon.IsHolding ? 1f : 0f);
            }
            ApplyRenderMode();
        }

        private void Update()
        {
            if (_cinematic) return;
            var rig = _player.Value;
            if (rig == null) return;
            var svc = _weapons.Value;
            if (svc != null)
            {
                var want = svc.IsHolstered ? null : svc.CurrentWeaponId;
                if (want != _shownWeapon) SyncWeapon();
            }
            if (_selfDrive) SelfDrive(rig);
            if (_ik == null || rig.Eye == null) return;
            var eye = rig.Eye;
            var viewPoint = eye.position + eye.forward * 25f;
            // ADS: the weapon follows the view; otherwise only the head tilts with it (low ready).
            _ik.SetAim(rig.IsADS ? viewPoint : (Vector3?)null);
            _ik.SetLookPoint(viewPoint, 0.75f);
        }

        private void SelfDrive(IPlayerRig rig)
        {
            var root = rig.Root;
            if (root != null)
                transform.SetPositionAndRotation(root.position, Quaternion.Euler(0f, rig.Yaw, 0f));
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            var v = rig.Velocity;
            v.y = 0f;
            _animator.SetFloat(AnimatorParams.SpeedId, v.magnitude);
            _animator.SetBool(AnimatorParams.CrouchId, rig.Stance == Stance.Crouch || rig.Stance == Stance.Slide);
            _animator.SetBool(AnimatorParams.AimId, rig.IsADS);
            _animator.SetBool(AnimatorParams.AirborneId, !rig.IsGrounded);
        }

#if UNITY_EDITOR
        /// <summary>Builders: wiring and controllers.</summary>
        public void EditorSetup(BodyRig rig, BodyIK ik, BodyWeaponHolder weapon, Animator animator, HumanoidBody cinematicBody,
                                RuntimeAnimatorController shadowController, RuntimeAnimatorController cinematicController)
        {
            _rig = rig;
            _ik = ik;
            _weapon = weapon;
            _animator = animator;
            _cinematicBody = cinematicBody;
            _shadowController = shadowController;
            _cinematicController = cinematicController;
        }
#endif
    }
}
