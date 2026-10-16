using System.Collections.Generic;
using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Effects;
using LongShadow.Combat.Service;
using LongShadow.Combat.Viewmodel;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// The player's hands (TECH_PLAN §7.5 PlayerWeapon, <see cref="IWeaponHudState"/>): reads <see cref="IInputReader"/>
    /// and drives the held weapon's <see cref="WeaponRuntime"/> (fire modes and RPM, sprint-out lock, spread, recoil,
    /// ADS, tactical/empty reloads), switching with raise/lower on 1/2/wheel, holster requests, the frag throw with
    /// cooking (<see cref="GrenadeThrower"/>), the knife (<see cref="Knife"/>) and the Beat 11 door gun
    /// (<see cref="DoorGun"/>). Each shot becomes a hitscan bullet (<see cref="HitscanShooter"/>), recoil on the rig
    /// (aim kick + 30 % camera punch, GDD §13.1), the view-model kick, a muzzle flash and 50 ms light, a casing, a tracer
    /// every Nth round, the shot sound, and the ShotFired/Noise/HitConfirmed/TracerPath events. Lives on the player rig
    /// with <see cref="WeaponInventory"/>; the view model is a <see cref="ViewmodelController"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Weapons)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponInventory))]
    public sealed class PlayerWeapon : MonoBehaviour, IWeaponHudState
    {
        private enum Hands { Holstered, Raising, Ready, Lowering, Mounted }

        /// <summary>Seconds without a shot after which a burst of 3+ rounds leaves a smoke puff at the muzzle.</summary>
        public const float MuzzleSmokeDelay = 0.12f;
        /// <summary>Magazine fill below which the low-ammo tick plays once (GDD §11: flash below 25 %).</summary>
        public const float LowAmmoFraction = 0.25f;

        [SerializeField, Tooltip("View model driver (found or added on this object when empty).")]
        private ViewmodelController _viewmodel;
        [SerializeField, Tooltip("Muzzle light colour (warm powder flash).")]
        private Color _flashColor = new Color(1f, 0.74f, 0.42f);
        [SerializeField, Tooltip("Muzzle light intensity at flash scale 1.")]
        private float _flashIntensity = 2.8f;
        [SerializeField, Tooltip("Muzzle light range (TECH_PLAN §7.5: 4 m).")]
        private float _flashRange = 4f;
        [SerializeField, Tooltip("Muzzle light duration (TECH_PLAN §7.5: 50 ms).")]
        private float _flashSeconds = 0.05f;

        private readonly Dictionary<string, WeaponRuntime> _runtimes = new Dictionary<string, WeaponRuntime>(6);
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<IInputReader> _input;
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<ISettingsService> _settings;
        private ServiceRef<IHud> _hud;
        private ServiceRef<ITargetRegistry> _targets;
        private WeaponInventory _inventory;
        private GrenadeThrower _grenade;
        private Knife _knife;
        private DoorGun _doorGun;
        private WeaponRuntime _doorRuntime;
        private WeaponRuntime _rt;
        private WeaponSpec _spec;
        private string _equipped;
        private Hands _hands = Hands.Holstered;
        private float _handsTime;
        private float _handsDuration;
        private float _lowerFrom;
        private float _lowered = 1f;
        private int _pendingSlot;
        private bool _forceReequip;
        private bool _adsToggled;
        private bool _fovHeld;
        private int _seenVersion = -1;
        private int _lastMag = int.MinValue;
        private int _lastReserve = int.MinValue;
        private int _lastFrags = int.MinValue;
        private bool _lowAmmo;
        private int _burstRounds;
        private bool _registered;
        private bool _started;

        /// <summary>Weapon in the hands (door gun while mounted), or null.</summary>
        public string EquippedId => IsMounted ? WeaponIds.DoorGun : _equipped;
        /// <summary>True while mounted on the door gun.</summary>
        public bool IsMounted => _doorGun != null && _doorGun.IsMounted;
        /// <summary>The held weapon's rules (null when nothing is equipped).</summary>
        public WeaponRuntime Runtime => _rt;
        /// <summary>The view model driver.</summary>
        public ViewmodelController Viewmodel => _viewmodel;
        /// <summary>The frag thrower.</summary>
        public GrenadeThrower Grenade => _grenade;
        /// <summary>True when the weapon is up and nothing else holds the hands.</summary>
        public bool IsReady => _hands == Hands.Ready && !_grenade.Active && !_knife.Active;

        // ---- IWeaponHudState ----------------------------------------------------------------------------------------
        /// <inheritdoc/>
        public float CrosshairSpread01 => _rt != null ? SpreadModel.Crosshair01(_spec.Spread, _rt.CurrentSpread) : 0f;
        /// <inheritdoc/>
        public bool CrosshairVisible
        {
            get
            {
                if (_rt == null || IsMounted || _hands != Hands.Ready || _grenade.Active || _knife.Active) return false;
                if (_rt.AdsBlend >= 0.5f) return false; // GDD §11: hidden while ADS
                var rig = _rig.Value;
                return rig == null || rig.Stance != Stance.Sprint; // GDD §11: hidden while sprinting
            }
        }
        /// <inheritdoc/>
        public float Heat01 => IsMounted && _doorRuntime != null ? _doorRuntime.Heat01 : 0f;
        /// <inheritdoc/>
        public bool Overheated => IsMounted && _doorRuntime != null && _doorRuntime.Overheated;
        /// <inheritdoc/>
        public float CookTime01 => _grenade != null ? _grenade.Cook01 : 0f;
        /// <inheritdoc/>
        public string DisplayName => _spec != null ? _spec.DisplayName : string.Empty;
        /// <inheritdoc/>
        public int Magazine => _rt?.Ammo?.Magazine ?? 0;
        /// <inheritdoc/>
        public int MagazineSize => _rt?.Ammo?.MagazineSize ?? 0;
        /// <inheritdoc/>
        public int Reserve => _rt?.Ammo?.Reserve ?? 0;
        /// <inheritdoc/>
        public int Frags => _inventory != null ? _inventory.Frags : 0;
        /// <inheritdoc/>
        public FireMode FireMode => _spec != null ? _spec.FireMode : FireMode.Auto;
        /// <inheritdoc/>
        public bool IsReloading => _rt != null && _rt.Reload.IsReloading;

        private void Awake()
        {
            _inventory = GetComponent<WeaponInventory>();
            _grenade = new GrenadeThrower(WeaponTable.Frag);
            _knife = new Knife(WeaponTable.Knife);
            _doorGun = new DoorGun(WeaponTable.DoorGunMount);
            if (_viewmodel == null) _viewmodel = GetComponentInChildren<ViewmodelController>(true);
            if (_viewmodel == null) _viewmodel = gameObject.AddComponent<ViewmodelController>();
            GameServices.Register<IWeaponHudState>(this);
            _registered = true;
        }

        private void Start()
        {
            _started = true;
            EquipImmediate();
        }

        private void OnDisable() => ReleaseFov();

        private void OnDestroy()
        {
            if (_registered) GameServices.Unregister<IWeaponHudState>(this);
        }

        // ---- requests (WeaponInventory) -----------------------------------------------------------------------------

        /// <summary>Requests a switch to <paramref name="slot"/>; <paramref name="force"/> re-draws even the same slot.</summary>
        public void RequestSlot(int slot, bool force = false)
        {
            if (_inventory.Loadout.InSlot(slot) == null) return;
            _pendingSlot = slot;
            _forceReequip |= force;
        }

        /// <summary>The loadout changed (give, swap, resupply, restore): re-draw if the weapon in hand is no longer carried.</summary>
        public void OnLoadoutChanged()
        {
            if (IsMounted || _equipped == null) return;
            if (_inventory.Loadout.CurrentId != _equipped)
            {
                _pendingSlot = _inventory.Loadout.CurrentSlot;
                _forceReequip = true;
            }
        }

        /// <summary>Draws the loadout's current weapon instantly (start, checkpoint restore, loadout reset).</summary>
        public void EquipImmediate()
        {
            if (!_started) return;
            CancelActions();
            if (IsMounted) return;
            _pendingSlot = 0;
            _forceReequip = false;
            foreach (var kv in _runtimes) kv.Value.Reset();
            var id = _inventory.Loadout.CurrentId;
            Equip(id);
            _viewmodel.ResetMotion();
            if (_inventory.IsHolstered)
            {
                _hands = Hands.Holstered;
                _lowered = 1f;
            }
            else
            {
                _hands = Hands.Ready;
                _lowered = 0f;
            }
        }

        /// <summary>Beat 11: mounts the door gun at <paramref name="pivot"/> (IWeaponService.MountDoorGun).</summary>
        public void MountDoorGun(Transform pivot)
        {
            var rig = _rig.Value;
            if (rig == null || pivot == null)
            {
                LSLog.Warn(LogCategory.Combat, "MountDoorGun needs the player rig and a pivot (HELI.DoorGunPivot).", this);
                return;
            }
            if (IsMounted && _doorGun.Pivot == pivot) return;
            CancelActions();
            ReleaseFov();
            if (!_doorGun.Mount(pivot, rig, _hud.Value)) return;
            if (_doorRuntime == null) _doorRuntime = new WeaponRuntime(WeaponTable.DoorGun, null, WeaponTable.DoorGunMount);
            _doorRuntime.Reset();
            _rt = _doorRuntime;
            _spec = WeaponTable.DoorGun;
            _viewmodel.Equip(WeaponIds.DoorGun);
            _viewmodel.ResetMotion();
            _hands = Hands.Mounted;
            _lowered = 0f;
            _audio.Value?.Play2D(CombatCues.Raise, 0.8f);
            GameEvents.RaiseWeaponChanged(WeaponIds.DoorGun);
        }

        /// <summary>Leaves the door gun and raises the carried weapon (IWeaponService.DismountDoorGun).</summary>
        public void DismountDoorGun()
        {
            if (!IsMounted) return;
            _doorGun.Dismount(_rig.Value, _hud.Value);
            _equipped = null;
            Equip(_inventory.Loadout.CurrentId);
            BeginRaise(1f);
        }

        // ---- frame ----------------------------------------------------------------------------------------------------

        private void Update()
        {
            var dt = Time.deltaTime;
            var rig = _rig.Value;
            if (rig == null || _inventory == null)
            {
                PushViewmodel(false);
                return;
            }
            var input = _input.Value;
            if (_inventory.Version != _seenVersion)
            {
                _seenVersion = _inventory.Version;
                if (_rt != null && !IsMounted && _equipped != null) _rt.SetAmmo(_inventory.Loadout.AmmoOf(_equipped));
            }

            if (IsMounted)
            {
                TickMounted(dt, rig, input);
                PushViewmodel(true);
                ReportAmmo();
                return;
            }

            var holster = _inventory.IsHolstered;
            TickHands(dt, holster);
            if (_hands == Hands.Ready || _hands == Hands.Raising) ReadSwitchInput(input);
            if (_hands == Hands.Ready && !holster) ReadActionInput(rig, input);
            TickGrenade(dt, rig, input);
            TickKnife(dt, rig);

            if (_rt != null)
            {
                var blocked = _hands != Hands.Ready || holster || _grenade.Active || _knife.Active;
                TickWeapon(dt, rig, input, blocked);
            }
            else
            {
                rig.SetAdsState(false, 0f);
            }
            PushViewmodel(_hands != Hands.Holstered && _rt != null);
            ReportAmmo();
        }

        private void TickHands(float dt, bool holster)
        {
            var wantSwitch = (_pendingSlot != 0 && _inventory.Loadout.InSlot(_pendingSlot) != null &&
                              (_inventory.Loadout.InSlot(_pendingSlot) != _equipped || _forceReequip));
            switch (_hands)
            {
                case Hands.Holstered:
                    _lowered = 1f;
                    if (!holster)
                    {
                        if (wantSwitch) CompleteSwitch();
                        else if (_equipped == null) Equip(_inventory.Loadout.CurrentId);
                        if (_equipped != null) BeginRaise(1f);
                    }
                    break;
                case Hands.Raising:
                    _handsTime += dt;
                    _lowered = 1f - CombatUtil.Smooth01(_handsTime / Mathf.Max(0.01f, _handsDuration));
                    if (holster || wantSwitch) BeginLower();
                    else if (_handsTime >= _handsDuration)
                    {
                        _hands = Hands.Ready;
                        _lowered = 0f;
                    }
                    break;
                case Hands.Ready:
                    _lowered = 0f;
                    if (holster || wantSwitch) BeginLower();
                    else if (_pendingSlot != 0)
                    {
                        // Asked for the weapon already in hand: nothing to do.
                        _pendingSlot = 0;
                        _forceReequip = false;
                    }
                    break;
                case Hands.Lowering:
                    _handsTime += dt;
                    _lowered = Mathf.Lerp(_lowerFrom, 1f, CombatUtil.Smooth01(_handsTime / Mathf.Max(0.01f, _handsDuration)));
                    if (_handsTime >= _handsDuration)
                    {
                        _lowered = 1f;
                        if (holster)
                        {
                            _hands = Hands.Holstered;
                        }
                        else
                        {
                            if (wantSwitch) CompleteSwitch();
                            BeginRaise(1f);
                        }
                    }
                    break;
            }
        }

        private void BeginLower()
        {
            CancelActions();
            _rt?.CancelReload();
            _lowerFrom = _lowered;
            _hands = Hands.Lowering;
            _handsTime = 0f;
            _handsDuration = (_spec != null ? _spec.LowerTime : 0.3f) * (1f - _lowerFrom);
            if (_handsDuration <= 0f) _handsDuration = 0.01f;
        }

        private void BeginRaise(float from)
        {
            if (_spec == null) return;
            _hands = Hands.Raising;
            _handsTime = (1f - Mathf.Clamp01(from)) * _spec.RaiseTime;
            _handsDuration = _spec.RaiseTime;
            _lowered = from;
            _audio.Value?.Play2D(CombatCues.Raise, 0.7f);
        }

        private void CompleteSwitch()
        {
            var slot = _pendingSlot;
            _pendingSlot = 0;
            _forceReequip = false;
            if (slot != 0) _inventory.CommitSlot(slot);
            _equipped = null;
            Equip(_inventory.Loadout.CurrentId);
            _audio.Value?.Play2D(CombatCues.Switch, 0.7f);
        }

        private void Equip(string id)
        {
            var spec = WeaponTable.Get(id);
            if (spec == null)
            {
                _equipped = null;
                _spec = null;
                _rt = null;
                return;
            }
            if (!_runtimes.TryGetValue(id, out var rt))
            {
                rt = new WeaponRuntime(spec, _inventory.Loadout.AmmoOf(id));
                _runtimes[id] = rt;
            }
            rt.SetAmmo(_inventory.Loadout.AmmoOf(id));
            rt.Reset();
            ReleaseFov();
            _rt = rt;
            _spec = spec;
            _equipped = id;
            _adsToggled = false;
            _viewmodel.Equip(id);
            _viewmodel.Prewarm(_inventory.Loadout.InSlot(1), _inventory.Loadout.InSlot(2));
            var rig = _rig.Value;
            if (rig != null) rig.SpeedMultiplier = spec.MoveMultiplier; // GDD §13.1 move speed
            GameEvents.RaiseWeaponChanged(id);
        }

        private void ReadSwitchInput(IInputReader input)
        {
            if (input == null || _grenade.Active || _knife.Active) return;
            var slot = input.WeaponSlotDown;
            if (slot == 0 && Mathf.Abs(input.Scroll) > 0.01f) slot = _inventory.Loadout.OtherSlot();
            if (slot != 0 && _inventory.Loadout.InSlot(slot) != null && _inventory.Loadout.InSlot(slot) != _equipped) RequestSlot(slot);
        }

        private void ReadActionInput(IPlayerRig rig, IInputReader input)
        {
            if (input == null || _grenade.Active || _knife.Active) return;
            if (input.MeleeDown)
            {
                _rt?.CancelReload();
                _knife.Begin(rig.ViewRay, _targets.Value);
                _audio.Value?.Play2D(CombatCues.KnifeSwing, 0.9f);
                return;
            }
            if (input.FragDown && _inventory.TryUseFrag())
            {
                _rt?.CancelReload();
                _grenade.Begin();
                _audio.Value?.Play2D(CombatCues.GrenadePin, 0.9f);
            }
        }

        private void TickGrenade(float dt, IPlayerRig rig, IInputReader input)
        {
            if (!_grenade.Active) return;
            var held = input != null && input.Frag;
            var released = input != null && input.FragUp;
            var ev = _grenade.Tick(dt, held, released);
            switch (ev)
            {
                case GrenadeThrower.Event.Tick:
                    _audio.Value?.Play2D(CombatCues.FuseTick, 0.5f);
                    break;
                case GrenadeThrower.Event.Throw:
                {
                    var ray = rig.ViewRay;
                    var pos = SafeHandPoint(ray.origin);
                    var vel = _grenade.ThrowVelocity(Quaternion.LookRotation(ray.direction), rig.Velocity);
                    var svc = CombatService.Current;
                    if (svc != null) svc.ThrowGrenade(pos, vel, _grenade.ThrownFuse, Team.Player, Instigator(rig));
                    else LSLog.WarnOnce("combat-no-service-grenade", LogCategory.Combat, "No CombatService: the thrown frag was dropped.");
                    break;
                }
                case GrenadeThrower.Event.ExplodedInHand:
                {
                    var pos = SafeHandPoint(rig.ViewRay.origin);
                    var s = WeaponTable.Frag;
                    CombatResolver.Explode(pos, s.Damage, s.LethalRadius, s.MaxRadius, Instigator(rig), Team.Player, WeaponIds.Frag,
                        FxIds.ExplosionFrag, CombatCues.ExplosionFrag, s.NoiseRadius, null);
                    GameEvents.RaiseGrenadeExploded(pos, s.MaxRadius);
                    break;
                }
            }
        }

        private void TickKnife(float dt, IPlayerRig rig)
        {
            if (!_knife.Active) return;
            var strike = _knife.Tick(dt, rig.ViewRay, Instigator(rig), Team.Player);
            if (!strike.Happened || !strike.Hit) return;
            var fx = CombatFx.Instance;
            var audio = _audio.Value;
            if (strike.HitActor)
            {
                audio?.Play(CombatCues.KnifeHit, strike.Point);
                fx?.Impacts.Play(SurfaceType.Flesh, strike.Point, strike.Normal, strike.Collider, false, null, strike.Point);
                GameEvents.RaiseHitConfirmed(strike.Kill, false);
            }
            else
            {
                fx?.Impacts.Play(strike.Surface, strike.Point, strike.Normal, strike.Collider, false, audio, strike.Point, 0.6f);
            }
            _rig.Value?.Shake(0.08f);
        }

        private void TickWeapon(float dt, IPlayerRig rig, IInputReader input, bool blocked)
        {
            var settings = _settings.Value?.Current;
            var aimHeld = ResolveAim(input, settings, blocked);
            var stance = rig.Stance;
            var sprinting = stance == Stance.Sprint;
            var planar = rig.Velocity;
            planar.y = 0f;
            var wi = new WeaponInput
            {
                TriggerHeld = input != null && input.Fire,
                TriggerDown = input != null && input.FireDown,
                AimHeld = aimHeld,
                ReloadDown = input != null && input.ReloadDown,
                Sprinting = sprinting,
                SinceSprintEnd = sprinting ? 0f : rig.TimeSinceSprintEnd,
                Move01 = planar.magnitude / 4.4f, // GDD §12 walk 4.4 m/s
                Grounded = rig.IsGrounded,
                Crouched = stance == Stance.Crouch,
                Blocked = blocked,
            };
            if (input != null) ReportLook(input, settings);
            var frame = _rt.Tick(dt, wi);
            ApplyFrame(rig, frame);
        }

        private void TickMounted(float dt, IPlayerRig rig, IInputReader input)
        {
            var wi = new WeaponInput
            {
                TriggerHeld = input != null && input.Fire,
                TriggerDown = input != null && input.FireDown,
                SinceSprintEnd = 10f,
                Grounded = true,
            };
            var frame = _rt.Tick(dt, wi);
            if (frame.Overheated || (frame.Shots == 0 && _rt.Overheated && wi.TriggerDown)) _audio.Value?.Play2D(CombatCues.DryFire, 0.6f);
            ApplyFrame(rig, frame);
        }

        private void ApplyFrame(IPlayerRig rig, in WeaponFrame frame)
        {
            if (frame.Recovery.sqrMagnitude > 1e-10f) rig.AddAimDelta(frame.Recovery.x, frame.Recovery.y);
            if (frame.Reload != ReloadEvents.None) OnReload(frame.Reload);
            for (var i = 0; i < frame.Shots; i++)
            {
                var shot = _rt.TakeShot(Random.value, Random.value, Random.value, Random.value);
                FireOne(rig, shot);
                if (shot.Overheated)
                {
                    _audio.Value?.Play2D(CombatCues.Overheat, 0.8f);
                    break;
                }
            }
            if (frame.Shots > 0) _burstRounds += frame.Shots;
            else if (_burstRounds > 0 && _rt.SinceShot > MuzzleSmokeDelay)
            {
                if (_burstRounds >= 3) MuzzleSmoke();
                _burstRounds = 0;
            }
            if (frame.DryFire) _audio.Value?.Play2D(CombatCues.DryFire, 0.8f);
            if (frame.AdsStarted)
            {
                RequestAdsFov(rig);
                _audio.Value?.Play2D(CombatCues.AdsIn, 0.5f);
            }
            if (frame.AdsEnded)
            {
                ReleaseFov();
                _audio.Value?.Play2D(CombatCues.AdsOut, 0.4f);
            }
            rig.SetAdsState(_rt.AdsIntent, _rt.AdsBlend);
        }

        private void FireOne(IPlayerRig rig, in ShotSolution shot)
        {
            var ray = rig.ViewRay;
            if (ray.direction.sqrMagnitude < 1e-6f && rig.WorldCamera != null)
                ray = new Ray(rig.WorldCamera.transform.position, rig.WorldCamera.transform.forward);
            var aim = Quaternion.LookRotation(ray.direction);
            var dir = SpreadModel.Deviate(aim, shot.SpreadDegrees, shot.SpreadRandom.x, shot.SpreadRandom.y);
            var instigator = Instigator(rig);
            var res = HitscanShooter.Fire(_spec, ray.origin, dir, instigator, Team.Player);
            if (res.HitEnemy) GameEvents.RaiseHitConfirmed(res.Kill, res.Headshot);

            // Recoil: the aim moves by the kick, the camera takes a 30 % visual punch on top (GDD §13.1).
            rig.AddAimDelta(shot.Kick.x, shot.Kick.y);
            var share = _spec.Recoil.CameraShare;
            rig.SetViewPunch(Quaternion.Euler(-shot.Kick.x * share, shot.Kick.y * share, (Random.value * 2f - 1f) * shot.Kick.x * share * 0.6f));
            _viewmodel.Kick(shot);

            // Effects at the view-model muzzle, mapped into the world camera's space.
            var fx = CombatFx.Instance;
            var vmMuzzle = _viewmodel.Muzzle;
            var muzzleWorld = vmMuzzle != null && _viewmodel.IsVisible ? _viewmodel.ToWorld(vmMuzzle.position) : ray.origin + ray.direction * 0.6f;
            var flashScale = _viewmodel.Current != null ? _viewmodel.Current.MuzzleFlashScale : 1f;
            if (fx != null)
            {
                if (flashScale > 0f)
                {
                    if (vmMuzzle != null && _viewmodel.IsVisible) fx.Muzzle.FlashAttached(vmMuzzle, flashScale, _viewmodel.Current.transform);
                    fx.Lights.Flash(muzzleWorld + dir * 0.12f, _flashColor, _flashIntensity * flashScale, _flashRange, _flashSeconds);
                }
                EjectCasing(fx, rig);
                if (shot.Tracer) fx.Tracers.Fire(muzzleWorld, res.End, Team.Player);
            }
            if (shot.Tracer) GameEvents.RaiseTracerPath(muzzleWorld, res.End, Team.Player);
            _audio.Value?.PlayWeaponShot(_spec.Id, ray.origin, true);
            GameEvents.RaiseShotFired(new ShotEvent
            {
                Origin = ray.origin, Direction = dir, Team = Team.Player, WeaponId = _spec.Id, NoiseRadius = _spec.NoiseRadius,
                Hit = res.HitDamageable, IsPlayer = true,
            });
            GameEvents.RaiseNoise(new NoiseEvent(ray.origin, _spec.NoiseRadius, Team.Player, _spec.NoiseKind)); // GDD §14.2
        }

        private void EjectCasing(CombatFx fx, IPlayerRig rig)
        {
            var vm = _viewmodel.Current;
            var eject = _viewmodel.Eject;
            if (vm == null || eject == null || string.IsNullOrEmpty(vm.CasingId) || !_viewmodel.IsVisible) return;
            var cam = rig.WorldCamera != null ? rig.WorldCamera.transform : eject;
            var pos = _viewmodel.ToWorld(eject.position);
            var vel = rig.Velocity + cam.right * Random.Range(1.4f, 2.2f) + cam.up * Random.Range(0.9f, 1.6f) - cam.forward * Random.Range(0.1f, 0.5f);
            var ang = new Vector3(Random.Range(-25f, 25f), Random.Range(-40f, 40f), Random.Range(-10f, 10f));
            fx.Shells.Eject(vm.CasingId, pos, vm.transform.rotation * Quaternion.Euler(0f, 90f, 0f), vel, ang);
        }

        private void MuzzleSmoke()
        {
            var fx = CombatFx.Instance;
            var muzzle = _viewmodel.Muzzle;
            if (fx == null || fx.MuzzleSmoke == null || muzzle == null || !_viewmodel.IsVisible) return;
            var p = _viewmodel.ToWorld(muzzle.position);
            var fwd = _viewmodel.Current != null ? _viewmodel.Current.transform.forward : muzzle.forward;
            fx.MuzzleSmoke.Emit(p, Quaternion.LookRotation(fwd + Vector3.up * 0.6f));
        }

        private void OnReload(ReloadEvents e)
        {
            var audio = _audio.Value;
            var id = _spec.Id;
            var sliced = audio != null && audio.HasCue(CombatCues.ReloadMagOut(id));
            if ((e & ReloadEvents.Started) != 0)
            {
                ReleaseFov();
                if (audio != null && !sliced) audio.Play2D(AudioCues.WeaponReload(id), 0.9f);
            }
            if (sliced)
            {
                if ((e & ReloadEvents.MagOut) != 0) audio.Play2D(CombatCues.ReloadMagOut(id), 0.9f);
                if ((e & ReloadEvents.MagIn) != 0) audio.Play2D(CombatCues.ReloadMagIn(id), 1f);
                if ((e & ReloadEvents.ChargeStart) != 0) audio.Play2D(CombatCues.ReloadCharge(id), 1f);
            }
            _viewmodel.OnReloadEvents(e, _rt.Reload.IsEmptyReload);
            if ((e & ReloadEvents.MagIn) != 0) _lowAmmo = false;
        }

        private bool ResolveAim(IInputReader input, GameSettings settings, bool blocked)
        {
            if (input == null) return false;
            if (settings != null && settings.AdsToggle)
            {
                if (input.AimDown) _adsToggled = !_adsToggled;
                if (blocked || (_rt != null && _rt.Reload.IsReloading)) _adsToggled = false;
                return _adsToggled;
            }
            return input.Aim;
        }

        private void ReportLook(IInputReader input, GameSettings settings)
        {
            var look = input.Look;
            if (look.sqrMagnitude < 1e-8f) return;
            var sens = settings != null ? settings.MouseSensitivity : 3f;
            var deg = look * (sens * GameSettings.DegreesPerPixelPerSens); // GDD §11 mouse formula
            if (_rt.AdsBlend > 0.5f)
            {
                var adsSens = settings != null ? settings.AdsSensitivity : 0.85f;
                deg *= adsSens / Mathf.Max(1f, _spec.AdsZoom);
            }
            var pitchUp = settings != null && settings.InvertY ? -deg.y : deg.y;
            _rt.OnPlayerLook(pitchUp, deg.x);
        }

        private void RequestAdsFov(IPlayerRig rig)
        {
            if (_spec == null || !_spec.CanAds) return;
            rig.RequestFov(this, AdsVerticalFov(_spec, HipVerticalFov(rig)), _spec.AdsTime);
            _fovHeld = true;
        }

        private void ReleaseFov()
        {
            if (!_fovHeld) return;
            _fovHeld = false;
            _rig.Value?.ReleaseFov(this);
        }

        /// <summary>The player's hip vertical FOV from the settings (GDD §11 FOV option) and the camera aspect.</summary>
        public float HipVerticalFov(IPlayerRig rig)
        {
            var h = _settings.Value?.Current?.FovHorizontal ?? 90f;
            var aspect = rig?.WorldCamera != null && rig.WorldCamera.aspect > 0.1f ? rig.WorldCamera.aspect : 16f / 9f;
            return MathX.HorizontalToVerticalFov(h, aspect);
        }

        /// <summary>
        /// ADS vertical FOV for a weapon, keeping its magnification (GDD §13: "ADS FOVs are given in vertical degrees at the
        /// default FOV and scale with the player's FOV setting"): the zoom is the tangent ratio at the default 58.7°, so the
        /// default setting gives exactly the GDD value (Commando 47°) and wider settings keep the same magnification.
        /// </summary>
        public static float AdsVerticalFov(WeaponSpec w, float hipVerticalFov)
        {
            if (w == null || !w.CanAds) return hipVerticalFov;
            var zoom = Mathf.Tan(WeaponTable.DefaultVerticalFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(w.AdsFovVertical * 0.5f * Mathf.Deg2Rad);
            zoom = Mathf.Max(1f, zoom);
            return 2f * Mathf.Atan(Mathf.Tan(hipVerticalFov * 0.5f * Mathf.Deg2Rad) / zoom) * Mathf.Rad2Deg;
        }

        private void PushViewmodel(bool visible)
        {
            if (_viewmodel == null) return;
            var s = new ViewmodelState
            {
                Visible = visible,
                AdsBlend = _rt != null ? _rt.AdsBlend : 0f,
                Lowered01 = _lowered,
                Mounted = IsMounted,
                Grenade = _grenade.Phase,
                GrenadePhaseTime = _grenade.PhaseTime,
                Knife = _knife.Active,
                KnifeTime = _knife.Time,
                KnifeLunge = _knife.Lunge,
            };
            if (_rt != null && _rt.Reload.IsReloading)
            {
                s.Reloading = true;
                s.ReloadElapsed = _rt.Reload.Elapsed;
                s.Reload = _rt.Reload.Spec;
                s.EmptyReload = _rt.Reload.IsEmptyReload;
            }
            _viewmodel.SetState(s);
        }

        private void ReportAmmo()
        {
            var mag = Magazine;
            var res = Reserve;
            var frags = Frags;
            if (mag == _lastMag && res == _lastReserve && frags == _lastFrags) return;
            var size = MagazineSize;
            if (size > 0 && !IsMounted && mag < _lastMag && mag > 0 && mag < size * LowAmmoFraction && !_lowAmmo)
            {
                _lowAmmo = true;
                _audio.Value?.Play2D(CombatCues.LowAmmo, 0.6f);
            }
            if (size > 0 && mag >= size * LowAmmoFraction) _lowAmmo = false;
            _lastMag = mag;
            _lastReserve = res;
            _lastFrags = frags;
            GameEvents.RaiseAmmoChanged(mag, res, frags);
        }

        private Vector3 SafeHandPoint(Vector3 eye)
        {
            var hand = _viewmodel.ActionHand != null ? _viewmodel.ToWorld(_viewmodel.ActionHand.position) : eye;
            var d = hand - eye;
            var len = d.magnitude;
            if (len < 0.01f) return eye;
            return Physics.Raycast(eye, d / len, out var hit, len, LayerIds.Ground, QueryTriggerInteraction.Ignore)
                ? eye + d / len * Mathf.Max(0f, hit.distance - 0.1f)
                : hand;
        }

        private GameObject Instigator(IPlayerRig rig) => rig != null && rig.Root != null ? rig.Root.gameObject : gameObject;

        private void CancelActions()
        {
            _grenade?.Cancel();
            _knife?.Cancel();
        }
    }
}
