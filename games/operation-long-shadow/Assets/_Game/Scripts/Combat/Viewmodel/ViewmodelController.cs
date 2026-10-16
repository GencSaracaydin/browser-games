using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// The first-person weapon (TECH_PLAN §7.5 ViewmodelController, <see cref="IWeaponGrips"/>): keeps one instance of each
    /// view-model prefab under <c>IPlayerRig.ViewmodelRoot</c> (layer Viewmodel), drives it with
    /// <see cref="ViewmodelMotion"/> (sway, bob, ADS, sprint/crouch/slide poses, kick, landing), plays the reload
    /// choreography (<see cref="ReloadChoreography"/>; the magazine object moves and an empty reload drops a physical
    /// magazine) and the left-hand actions (<see cref="HandActions"/>), sets the view-model camera FOV for ADS, and exposes
    /// the IK grips the WP05 FPArmsIK solves the arms onto (the left grip follows the magazine, the charging handle, the
    /// grenade or the knife). Runs in LateUpdate after the camera rig and before the arms (ExecOrder.Weapons).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Weapons)]
    [DisallowMultipleComponent]
    public sealed class ViewmodelController : MonoBehaviour, IWeaponGrips
    {
        [SerializeField] private ViewmodelTuning _tuning = new ViewmodelTuning();
        [SerializeField, Tooltip("Parent of the weapon; defaults to IPlayerRig.ViewmodelRoot.")]
        private Transform _root;
        [SerializeField, Tooltip("Grenade held in the left hand (defaults to LIB_WeaponVisuals M67, else a greybox).")]
        private GameObject _grenadeItemPrefab;
        [SerializeField, Tooltip("Knife held in the left hand (defaults to LIB_WeaponVisuals KNIFE, else a greybox).")]
        private GameObject _knifeItemPrefab;

        private readonly Dictionary<string, ViewmodelRig> _instances = new Dictionary<string, ViewmodelRig>(6);
        private ServiceRef<IPlayerRig> _rig;
        private IPlayerRig _boundRig;
        private ViewmodelMotion _motion;
        private Transform _pivot;
        private Transform _rightGrip;
        private Transform _leftGrip;
        private Transform _support;
        private Transform _actionHand;
        private GameObject _grenadeItem;
        private GameObject _knifeItem;
        private ViewmodelRig _current;
        private WeaponSpec _spec;
        private ViewmodelState _state;
        private ReloadPose _reloadPose = ReloadPose.Rest;
        private float _prevPitch;
        private float _prevYaw;
        private bool _hasLook;
        private float _leftWeight = 1f;
        private float _time;
        private MagazineDrops _drops;
        private bool _registered;

        /// <summary>The equipped view model, or null.</summary>
        public ViewmodelRig Current => _current;
        /// <summary>Weapon id of the equipped view model.</summary>
        public string CurrentId => _current != null ? _current.WeaponId : null;
        /// <summary>The procedural motion (springs).</summary>
        public ViewmodelMotion Motion => _motion;
        /// <summary>Feel constants.</summary>
        public ViewmodelTuning Tuning => _tuning;
        /// <summary>Muzzle socket of the equipped weapon (Viewmodel layer).</summary>
        public Transform Muzzle => _current != null ? _current.Muzzle : null;
        /// <summary>Ejection port socket of the equipped weapon.</summary>
        public Transform Eject => _current != null ? _current.Eject : null;
        /// <summary>Left-hand action anchor (grenade release point).</summary>
        public Transform ActionHand => _actionHand;
        /// <summary>True when the view model renders.</summary>
        public bool IsVisible => _current != null && _current.gameObject.activeSelf;

        // ---- IWeaponGrips -------------------------------------------------------------------------------------------
        /// <inheritdoc/>
        public Transform RightGrip => IsVisible ? _rightGrip : null;
        /// <inheritdoc/>
        public Transform LeftGrip => IsVisible ? _leftGrip : null;
        /// <inheritdoc/>
        public Transform SupportHint => IsVisible ? _support : null;
        /// <inheritdoc/>
        public float LeftHandWeight => IsVisible ? _leftWeight : 0f;

        private void Awake()
        {
            _motion = new ViewmodelMotion(_tuning);
            GameServices.Register<IWeaponGrips>(this);
            _registered = true;
        }

        private void OnDestroy()
        {
            if (_registered) GameServices.Unregister<IWeaponGrips>(this);
            if (_boundRig != null) _boundRig.Landed -= OnLanded;
            if (_drops != null) _drops.Clear();
        }

        /// <summary>Finds the rig's view-model root and creates the pivot and IK proxies. False until the rig exists.</summary>
        public bool EnsureBound()
        {
            if (_pivot != null) return true;
            var rig = _rig.Value;
            var root = _root;
            if (root == null && rig != null)
            {
                root = rig.ViewmodelRoot;
                if (root == null && rig.ViewmodelCamera != null) root = rig.ViewmodelCamera.transform;
                if (root == null && rig.WorldCamera != null) root = rig.WorldCamera.transform;
            }
            if (root == null) return false;
            _root = root;
            _pivot = NewChild(root, "WeaponPivot");
            _rightGrip = NewChild(root, "IK_RightGrip");
            _leftGrip = NewChild(root, "IK_LeftGrip");
            _support = NewChild(root, "IK_SupportHint");
            _actionHand = NewChild(root, "IK_ActionHand");
            _grenadeItem = CreateItem(WeaponIds.Frag, _grenadeItemPrefab, true);
            _knifeItem = CreateItem(WeaponIds.Knife, _knifeItemPrefab, false);
            _drops = new MagazineDrops(new GameObject("MagazineDrops").transform); // world objects, not under the camera
            if (rig != null)
            {
                _boundRig = rig;
                rig.Landed += OnLanded;
            }
            return true;
        }

        /// <summary>Shows the view model of <paramref name="weaponId"/> (creates it once; prefab from LIB_WeaponVisuals or a greybox).</summary>
        public ViewmodelRig Equip(string weaponId)
        {
            if (!EnsureBound() || string.IsNullOrEmpty(weaponId)) return null;
            if (_current != null && _current.WeaponId == weaponId) return _current;
            if (_current != null) _current.gameObject.SetActive(false);
            _current = Instance(weaponId);
            _spec = WeaponTable.Get(weaponId);
            if (_current != null)
            {
                _current.ResetMagazine();
                _current.gameObject.SetActive(_state.Visible);
            }
            _hasLook = false;
            return _current;
        }

        /// <summary>Creates the instances of <paramref name="weaponIds"/> now (no hitch on the first switch).</summary>
        public void Prewarm(params string[] weaponIds)
        {
            if (!EnsureBound() || weaponIds == null) return;
            foreach (var id in weaponIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var vm = Instance(id);
                if (vm != null && vm != _current) vm.gameObject.SetActive(false);
            }
        }

        /// <summary>Per-frame state from PlayerWeapon.</summary>
        public void SetState(in ViewmodelState state) => _state = state;

        /// <summary>Visual kick of one shot.</summary>
        public void Kick(in ShotSolution shot)
        {
            if (_spec == null) return;
            _motion.AddKick(_spec.Recoil, shot.Kick, _state.AdsBlend, Random.value, Random.value);
        }

        /// <summary>Reload keys from the weapon: an empty reload drops the magazine physically at MagOut.</summary>
        public void OnReloadEvents(ReloadEvents e, bool empty)
        {
            if ((e & ReloadEvents.MagOut) == 0 || !empty || _current == null || _current.Magazine == null || _drops == null) return;
            var mag = _current.Magazine;
            var rig = _rig.Value;
            var pos = ToWorld(mag.position);
            var vel = (rig != null ? rig.Velocity : Vector3.zero) + Vector3.down * 1.2f;
            if (rig != null && rig.WorldCamera != null) vel += rig.WorldCamera.transform.right * -0.25f + rig.WorldCamera.transform.forward * 0.2f;
            _drops.Drop(_current.WeaponId, pos, mag.rotation, vel);
        }

        /// <summary>World-camera point that looks like <paramref name="viewmodelPoint"/> on screen (tracers, lights, casings).</summary>
        public Vector3 ToWorld(Vector3 viewmodelPoint)
        {
            var rig = _rig.Value;
            return rig != null ? ViewmodelSpace.ToWorld(viewmodelPoint, rig.ViewmodelCamera, rig.WorldCamera) : viewmodelPoint;
        }

        /// <summary>Hides everything and forgets transient motion (checkpoint, teleport).</summary>
        public void ResetMotion()
        {
            _motion?.Reset();
            _hasLook = false;
            _drops?.Clear();
            if (_current != null) _current.ResetMagazine();
        }

        private void OnLanded(float speed) => _motion?.AddLanding(speed);

        private void LateUpdate()
        {
            if (!EnsureBound()) return;
            var dt = Time.deltaTime;
            _drops?.Tick(dt);
            var visible = _state.Visible && _current != null;
            if (_current != null && _current.gameObject.activeSelf != visible) _current.gameObject.SetActive(visible);
            if (!visible)
            {
                _leftWeight = 0f;
                SetItems(false, false);
                return;
            }
            _time += dt;
            var rig = _rig.Value;

            // Look delta (degrees) from the rig's angles.
            var look = Vector2.zero;
            if (rig != null)
            {
                if (_hasLook) look = new Vector2(rig.Pitch - _prevPitch, CombatUtil.WrapAngle(rig.Yaw - _prevYaw));
                _prevPitch = rig.Pitch;
                _prevYaw = rig.Yaw;
                _hasLook = true;
            }

            // Poses of the running reload / hand action.
            _reloadPose = _state.Reloading && _spec != null
                ? ReloadChoreography.Evaluate(_spec.Kind, _state.Reload, _state.ReloadElapsed, _state.EmptyReload)
                : ReloadPose.Rest;
            var action = default(ActionPose);
            if (_state.Grenade != GrenadePhase.None)
                action = HandActions.Grenade(_state.Grenade, _state.GrenadePhaseTime, WeaponTable.Frag, _time);
            else if (_state.Knife)
                action = HandActions.Knife(_state.KnifeTime, WeaponTable.Knife, _state.KnifeLunge);

            var stance = rig != null ? rig.Stance : Stance.Stand;
            var input = new ViewmodelInput
            {
                Dt = dt,
                Time = _time,
                AdsBlend = _state.AdsBlend,
                Sprinting = stance == Stance.Sprint && !_state.Mounted,
                Crouched = stance == Stance.Crouch,
                Sliding = stance == Stance.Slide,
                Airborne = rig != null && !rig.IsGrounded && stance != Stance.Mounted && stance != Stance.Ladder,
                Lowered01 = _state.Lowered01,
                LookDelta = look,
                LocalVelocity = rig != null && _root != null ? _root.InverseTransformDirection(rig.Velocity) : Vector3.zero,
                WalkSpeed = 4.4f, // GDD §12 walk speed
                BobPhase = rig != null ? rig.BobPhase : 0f,
                BobAmount01 = rig != null && !_state.Mounted ? rig.BobAmount01 : 0f,
                Reload = _reloadPose,
                Action = action,
            };
            var pose = _current.Pose(_spec != null ? _spec.SprintOutTime : 0.2f);
            _motion.Step(input, pose, _current.Heft, out var pos, out var rot);
            _pivot.localPosition = pos;
            _pivot.localRotation = rot;
            _current.PoseMagazine(_reloadPose.MagOffset, _reloadPose.MagEuler, _reloadPose.MagVisible);

            // Hand action item.
            if (action.Active)
            {
                _actionHand.localPosition = action.HandPosition;
                _actionHand.localRotation = Quaternion.Euler(action.HandEuler);
            }
            SetItems(action.Active && action.ItemVisible && _state.Grenade != GrenadePhase.None,
                     action.Active && action.ItemVisible && _state.Knife);

            // View-model camera FOV (TECH_PLAN §3.6: 55° at the hip, per weapon in ADS).
            if (rig != null && rig.ViewmodelCamera != null)
                rig.ViewmodelCamera.fieldOfView = Mathf.Lerp(_tuning.HipViewmodelFov, _current.AdsViewmodelFov, CombatUtil.Smooth01(_state.AdsBlend));

            // Left grip: handguard → magazine → charging handle → action hand.
            var basePos = _current.LeftHand != null ? _current.LeftHand.position : _pivot.position;
            // Grip frames follow the weapon (forward = barrel, up = top, IWeaponGrips), whatever rotation the art's empties carry.
            var weaponRot = _current.transform.rotation;
            if (_current.Grip != null) _rightGrip.SetPositionAndRotation(_current.Grip.position, weaponRot);
            else _rightGrip.SetPositionAndRotation(_pivot.position, weaponRot);
            var baseRot = weaponRot;
            var magT = _current.MagGrip;
            if (magT != null && _reloadPose.LeftOnMag > 0f)
            {
                basePos = Vector3.Lerp(basePos, magT.position, _reloadPose.LeftOnMag);
                baseRot = Quaternion.Slerp(baseRot, magT.rotation, _reloadPose.LeftOnMag);
            }
            var charge = _current.ChargeHandle;
            if (charge != null && _reloadPose.LeftOnCharge > 0f)
            {
                var c = charge.position - _pivot.rotation * Vector3.forward * (0.045f * _reloadPose.ChargeTravel);
                basePos = Vector3.Lerp(basePos, c, _reloadPose.LeftOnCharge);
                baseRot = Quaternion.Slerp(baseRot, weaponRot, _reloadPose.LeftOnCharge);
            }
            if (action.Active && action.HandWeight > 0f)
            {
                basePos = Vector3.Lerp(basePos, _actionHand.position, action.HandWeight);
                baseRot = Quaternion.Slerp(baseRot, _actionHand.rotation, action.HandWeight);
            }
            _leftGrip.SetPositionAndRotation(basePos, baseRot);
            if (_current.SupportHint != null) _support.SetPositionAndRotation(_current.SupportHint.position, weaponRot);

            // One-handed sprint for pistols.
            var targetWeight = _current.TwoHandedSprint || action.Active ? 1f : 1f - _motion.Sprint01;
            _leftWeight = Mathf.MoveTowards(_leftWeight, targetWeight, dt * 6f);
        }

        private ViewmodelRig Instance(string weaponId)
        {
            if (_instances.TryGetValue(weaponId, out var vm) && vm != null) return vm;
            GameObject go = null;
            if (Libraries.TryGet(out WeaponVisualsLibrary lib) && lib.TryGet(weaponId, out var e) && e.Viewmodel != null)
                go = Instantiate(e.Viewmodel, _pivot, false);
            if (go == null)
            {
                LSLog.MissingAsset(LogCategory.Combat, "LIB_WeaponVisuals/" + weaponId + " (view model)", WeaponVisualsLibrary.AssetPath);
                go = WeaponGreybox.Build(weaponId, Placeholders.Material);
                go.transform.SetParent(_pivot, false);
            }
            go.name = "VM_" + weaponId;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            CombatUtil.SetLayerRecursively(go, LayerIds.Viewmodel);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.renderingLayerMask = RenderingLayerIds.ViewmodelReceives;
            }
            vm = go.GetComponent<ViewmodelRig>();
            if (vm == null)
            {
                vm = go.AddComponent<ViewmodelRig>();
                vm.Configure(weaponId, false, go.transform, CombatUtil.FindAny(go.transform, "Mag", "Magazine"), WeaponTable.Get(weaponId));
            }
            vm.Init();
            go.SetActive(false);
            _instances[weaponId] = vm;
            return vm;
        }

        private GameObject CreateItem(string weaponId, GameObject prefab, bool grenade)
        {
            if (prefab == null && Libraries.TryGet(out WeaponVisualsLibrary lib) && lib.TryGet(weaponId, out var e)) prefab = e.Viewmodel;
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, _actionHand, false);
            else
            {
                go = new GameObject("Item_" + weaponId);
                go.transform.SetParent(_actionHand, false);
                if (grenade)
                {
                    Placeholders.Create("M67", PrimitiveType.Sphere, new Vector3(0.064f, 0.064f, 0.064f), new Color(0.24f, 0.27f, 0.18f), go.transform)
                        .transform.localPosition = new Vector3(0f, 0.01f, 0.035f);
                }
                else
                {
                    var blade = Placeholders.Create("KnifeBlade", PrimitiveType.Cube, new Vector3(0.004f, 0.03f, 0.17f), new Color(0.3f, 0.3f, 0.32f), go.transform);
                    blade.transform.localPosition = new Vector3(0f, 0f, 0.12f);
                    var handle = Placeholders.Create("KnifeHandle", PrimitiveType.Cube, new Vector3(0.022f, 0.026f, 0.11f), new Color(0.25f, 0.17f, 0.1f), go.transform);
                    handle.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                }
            }
            go.name = "Item_" + weaponId;
            CombatUtil.SetLayerRecursively(go, LayerIds.Viewmodel);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.renderingLayerMask = RenderingLayerIds.ViewmodelReceives;
            }
            go.SetActive(false);
            return go;
        }

        private void SetItems(bool grenade, bool knife)
        {
            if (_grenadeItem != null && _grenadeItem.activeSelf != grenade) _grenadeItem.SetActive(grenade);
            if (_knifeItem != null && _knifeItem.activeSelf != knife) _knifeItem.SetActive(knife);
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.gameObject.layer = LayerIds.Viewmodel;
            return t;
        }
    }
}
