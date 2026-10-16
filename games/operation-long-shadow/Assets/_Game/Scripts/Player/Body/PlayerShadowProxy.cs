using LongShadow.Core;
using LongShadow.Player.Movement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Player.Body
{
    /// <summary>
    /// Drives the shadow-only Calder body (TECH_PLAN D19, GDD §12, §9.5): <c>PFB_Calder_Shadow</c> nested in the rig's
    /// <c>ShadowProxySlot</c> (layer Player, every renderer <see cref="ShadowCastingMode.ShadowsOnly"/>, colliders off),
    /// so the player casts a correct sun shadow (the MID-A "LONG SHADOW" image) while nothing of it ever renders in the
    /// view. The slot follows the rig root and its yaw through the hierarchy; this component feeds the 5-state
    /// <c>AC_ShadowProxy</c> (<see cref="AnimatorParams.Speed"/>, <see cref="AnimatorParams.Crouch"/>,
    /// <see cref="AnimatorParams.Aim"/>, <see cref="AnimatorParams.Airborne"/>; WP05 <c>ShadowProxyBody</c> adds the weapon
    /// and the head/aim IK). Hidden in the booth and cinematics (rig view inactive), while mounted (cabin), while dead,
    /// while the scripted posture is prone, and on <c>IPlayerRig.SetBodyVisibility</c>.
    /// <para>Until PFB_Calder_Shadow lands, a greybox body (capsule, head, rifle) is built at runtime and scaled to the
    /// stance height (stand 1.8, crouch 1.1, slide 0.9 m) so the shadow still matches the stance.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerShadowProxy : MonoBehaviour
    {
        /// <summary>Below this eye height (scripted prone) the proxy hides: AC_ShadowProxy has no prone state.</summary>
        public const float ProneEyeHeight = 0.7f;
        /// <summary>Manifest-free asset name of the shadow body prefab (WP05, Prefabs/Bodies).</summary>
        public const string BodyPrefabName = "PFB_Calder_Shadow";

        [SerializeField] private Transform _slot;
        [SerializeField, Tooltip("Build a greybox body at runtime when the slot is empty.")]
        private bool _createPlaceholder = true;
        [SerializeField, Tooltip("Animator speed parameter smoothing (1/s).")]
        private float _speedDamping = 12f;

        private Animator _animator;
        private Renderer[] _renderers = new Renderer[0];
        private Transform _placeholder;
        private bool _hasSpeed, _hasCrouch, _hasAim, _hasAirborne;
        private bool _visible = true;
        private bool _bound;
        private float _speed;
        private float _placeholderHeight = -1f;

        /// <summary>True while the proxy renders its shadow.</summary>
        public bool Visible => _visible;
        /// <summary>The body under the slot drives an animator.</summary>
        public bool HasAnimator => _animator != null;
        /// <summary>True when the runtime greybox stands in for PFB_Calder_Shadow.</summary>
        public bool UsingPlaceholder => _placeholder != null;
        /// <summary>The slot.</summary>
        public Transform Slot => _slot;
        /// <summary>Current greybox height in metres (stance), or -1 when the real body is used.</summary>
        public float PlaceholderHeight => _placeholder != null ? _placeholderHeight : -1f;

        /// <summary>Visibility rule (D19 and TECH_PLAN §7.3 "disabled in the booth, the cabin and cinematics that hide the player").</summary>
        public static bool ShouldShow(bool enabledByScript, bool viewActive, bool alive, bool mounted, float eyeHeightNow) =>
            enabledByScript && viewActive && alive && !mounted && eyeHeightNow >= ProneEyeHeight;

        /// <summary>Animator "Crouch" for a stance (slide uses the crouch pose).</summary>
        public static bool IsCrouchPose(Stance stance, bool crouchedCapsule) =>
            stance == Stance.Crouch || stance == Stance.Slide || (crouchedCapsule && stance != Stance.Sprint);

        /// <summary>Animator "Airborne" for a stance (jump, fall, vault/climb, ladder).</summary>
        public static bool IsAirbornePose(Stance stance) => stance == Stance.Airborne || stance == Stance.Mantle || stance == Stance.Ladder;

        /// <summary>Greybox body height for a stance (GDD §12 capsule 1.8, TECH_PLAN §7.3 crouch 1.1; slide 0.9).</summary>
        public static float GreyboxHeight(Stance stance, bool crouchedCapsule, MotorSettings s)
        {
            if (stance == Stance.Slide) return 0.9f;
            return IsCrouchPose(stance, crouchedCapsule) ? s.CrouchHeight : s.StandHeight;
        }

        /// <summary>Wires the slot (PlayerRigFactory).</summary>
        internal void Bind(Transform slot)
        {
            _slot = slot;
            _bound = false;
        }

        private void Awake() => Rebind();

        /// <summary>Re-reads the body under the slot (call after replacing it): layer, shadow mode, animator, colliders.</summary>
        public void Rebind()
        {
            _bound = true;
            if (_slot == null)
            {
                var t = transform.Find(PlayerRigNames.ShadowProxySlot);
                _slot = t;
            }
            if (_slot == null) return;
            if (_slot.GetComponentInChildren<Renderer>(true) == null && _createPlaceholder) BuildPlaceholder();

            SetLayer(_slot, LayerIds.Player);
            foreach (var c in _slot.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            _renderers = _slot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                r.receiveShadows = false;
                r.forceRenderingOff = !_visible;
            }

            _animator = _slot.GetComponentInChildren<Animator>(true);
            _hasSpeed = _hasCrouch = _hasAim = _hasAirborne = false;
            if (_animator != null)
            {
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // shadow casters are never "visible"
                if (_animator.runtimeAnimatorController != null)
                {
                    foreach (var p in _animator.parameters)
                    {
                        if (p.nameHash == AnimatorParams.SpeedId && p.type == AnimatorControllerParameterType.Float) _hasSpeed = true;
                        else if (p.nameHash == AnimatorParams.CrouchId && p.type == AnimatorControllerParameterType.Bool) _hasCrouch = true;
                        else if (p.nameHash == AnimatorParams.AimId && p.type == AnimatorControllerParameterType.Bool) _hasAim = true;
                        else if (p.nameHash == AnimatorParams.AirborneId && p.type == AnimatorControllerParameterType.Bool) _hasAirborne = true;
                    }
                }
                else
                {
                    LSLog.WarnOnce("player-shadow-noctrl", LogCategory.Player,
                        BodyPrefabName + " has no animator controller (AC_ShadowProxy, WP05): the shadow keeps its bind pose.", this);
                }
            }
        }

        /// <summary>Per-frame update (called by PlayerRig in LateUpdate).</summary>
        public void Tick(bool visible, Stance stance, bool crouchedCapsule, float planarSpeed, bool aiming, MotorSettings motorSettings, float dt)
        {
            if (!_bound) Rebind();
            if (visible != _visible) SetVisible(visible);
            if (!visible) return;

            _speed = dt > 0f ? MathX.Damp(_speed, planarSpeed, _speedDamping, dt) : planarSpeed;
            var crouch = IsCrouchPose(stance, crouchedCapsule);
            var airborne = IsAirbornePose(stance);

            if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
            {
                if (_hasSpeed) _animator.SetFloat(AnimatorParams.SpeedId, _speed);
                if (_hasCrouch) _animator.SetBool(AnimatorParams.CrouchId, crouch);
                if (_hasAim) _animator.SetBool(AnimatorParams.AimId, aiming);
                if (_hasAirborne) _animator.SetBool(AnimatorParams.AirborneId, airborne);
            }

            if (_placeholder != null && motorSettings != null)
            {
                var h = GreyboxHeight(stance, crouchedCapsule, motorSettings);
                if (Mathf.Abs(h - _placeholderHeight) > 1e-3f)
                {
                    _placeholderHeight = h;
                    _placeholder.localScale = new Vector3(1f, h / Mathf.Max(0.1f, motorSettings.StandHeight), 1f);
                }
            }
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            for (var i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].forceRenderingOff = !visible;
            if (_animator != null) _animator.enabled = visible;
        }

        private void BuildPlaceholder()
        {
            LSLog.MissingAsset(LogCategory.Player, BodyPrefabName, "Assets/_Game/Prefabs/Bodies/" + BodyPrefabName + ".prefab", this);
            var root = new GameObject(PlayerRigNames.ShadowPlaceholder).transform;
            root.SetParent(_slot, false);
            var grey = Placeholders.Greybox;
            var body = Placeholders.Create("CalderShadow_Body", PrimitiveType.Capsule, new Vector3(0.48f, 0.82f, 0.36f), grey, root);
            body.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            var head = Placeholders.Create("CalderShadow_Head", PrimitiveType.Sphere, new Vector3(0.24f, 0.27f, 0.26f), grey, root);
            head.transform.localPosition = new Vector3(0f, 1.66f, 0.02f);
            var rifle = Placeholders.Create("CalderShadow_Rifle", PrimitiveType.Cube, new Vector3(0.06f, 0.09f, 0.8f), grey, root);
            rifle.transform.localPosition = new Vector3(0.18f, 1.22f, 0.32f);
            rifle.transform.localRotation = Quaternion.Euler(12f, -8f, 0f);
            _placeholder = root;
            _placeholderHeight = -1f;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }
    }
}
