using LongShadow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LongShadow.Input
{
    /// <summary>
    /// <see cref="IInputReader"/> over the project-wide actions (TECH_PLAN §7.2). Resolves actions by name once, samples
    /// them every frame before gameplay (execution order <see cref="ExecOrder.Input"/>), applies <see cref="IInputLock"/>
    /// (neutral values for locked channels), divides the mouse delta by <see cref="IDisplayInfo.DevicePixelRatio"/> and
    /// drops one-frame spikes (<see cref="SpikeFilter"/>). Registers itself as IInputReader in Awake. Lives in S_Boot (WP01).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Input)]
    [DisallowMultipleComponent]
    public sealed class InputReader : MonoBehaviour, IInputReader
    {
        [SerializeField, Tooltip("Optional; empty = InputSystem.actions (project-wide).")]
        private InputActionAsset _actionsOverride;
        [SerializeField, Tooltip("Spike factor over the running median (TECH_PLAN §7.2: 8).")]
        private float _spikeFactor = 8f;
        [SerializeField] private int _spikeWindow = 15;
        [SerializeField, Tooltip("Minimum spike size in CSS px.")]
        private float _minSpike = 40f;
        [SerializeField] private bool _registerService = true;

        private InputActionAsset _asset;
        private InputAction _move, _look, _fire, _aim, _reload, _sprint, _jump, _crouch, _frag, _melee, _interact,
                            _weapon1, _weapon2, _scroll, _objectives, _pause, _skip;
        private SpikeFilter _spike;
        private ServiceRef<IInputLock> _lock;
        private ServiceRef<IDisplayInfo> _display;
        private float _skipHeld;

        /// <summary>The actions asset in use.</summary>
        public InputActionAsset Actions => _asset;
        /// <summary>Frames dropped by the spike filter (diagnostics).</summary>
        public int SpikesDropped => _spike?.Dropped ?? 0;

        /// <inheritdoc/>
        public Vector2 Move { get; private set; }
        /// <inheritdoc/>
        public Vector2 Look { get; private set; }
        /// <inheritdoc/>
        public bool Fire { get; private set; }
        /// <inheritdoc/>
        public bool FireDown { get; private set; }
        /// <inheritdoc/>
        public bool Aim { get; private set; }
        /// <inheritdoc/>
        public bool AimDown { get; private set; }
        /// <inheritdoc/>
        public bool ReloadDown { get; private set; }
        /// <inheritdoc/>
        public bool Sprint { get; private set; }
        /// <inheritdoc/>
        public bool SprintDown { get; private set; }
        /// <inheritdoc/>
        public bool JumpDown { get; private set; }
        /// <inheritdoc/>
        public bool CrouchDown { get; private set; }
        /// <inheritdoc/>
        public bool Frag { get; private set; }
        /// <inheritdoc/>
        public bool FragDown { get; private set; }
        /// <inheritdoc/>
        public bool FragUp { get; private set; }
        /// <inheritdoc/>
        public bool MeleeDown { get; private set; }
        /// <inheritdoc/>
        public bool Interact { get; private set; }
        /// <inheritdoc/>
        public bool InteractDown { get; private set; }
        /// <inheritdoc/>
        public bool ObjectivesHeld { get; private set; }
        /// <inheritdoc/>
        public bool ObjectivesDown { get; private set; }
        /// <inheritdoc/>
        public bool PauseDown { get; private set; }
        /// <inheritdoc/>
        public int WeaponSlotDown { get; private set; }
        /// <inheritdoc/>
        public float Scroll { get; private set; }
        /// <inheritdoc/>
        public float SkipHeld01 { get; private set; }

        private void Awake()
        {
            _spike = new SpikeFilter(_spikeWindow, _spikeFactor, _minSpike);
            Resolve();
            if (_registerService) GameServices.Register<IInputReader>(this);
        }

        private void OnEnable()
        {
            if (_asset == null) return;
            var player = _asset.FindActionMap(InputIds.PlayerMap);
            var ui = _asset.FindActionMap(InputIds.UIMap);
            if (player != null && !player.enabled) player.Enable();
            if (ui != null && !ui.enabled) ui.Enable();
        }

        private void OnDestroy()
        {
            if (_registerService) GameServices.Unregister<IInputReader>(this);
        }

        /// <summary>Re-resolves actions (after the asset changes).</summary>
        public void Resolve()
        {
            _asset = _actionsOverride != null ? _actionsOverride : InputSystem.actions;
            if (_asset == null)
            {
                LSLog.Error(LogCategory.Input, "No input actions: assign project-wide actions (LongShadow/Setup/Apply Project Settings)", this);
                return;
            }
            _move = Find(InputIds.Move);
            _look = Find(InputIds.Look);
            _fire = Find(InputIds.Fire);
            _aim = Find(InputIds.Aim);
            _reload = Find(InputIds.Reload);
            _sprint = Find(InputIds.Sprint);
            _jump = Find(InputIds.Jump);
            _crouch = Find(InputIds.Crouch);
            _frag = Find(InputIds.Frag);
            _melee = Find(InputIds.Melee);
            _interact = Find(InputIds.Interact);
            _weapon1 = Find(InputIds.Weapon1);
            _weapon2 = Find(InputIds.Weapon2);
            _scroll = Find(InputIds.WeaponScroll);
            _objectives = Find(InputIds.Objectives);
            _pause = Find(InputIds.Pause);
            _skip = Find(InputIds.Skip);
        }

        private InputAction Find(string action)
        {
            var a = _asset.FindAction(InputIds.PlayerMap + "/" + action);
            if (a == null) LSLog.ErrorOnce("input-missing:" + action, LogCategory.Input, "Input action Player/" + action + " not found", this);
            return a;
        }

        /// <summary>Clears the spike history (call after the pointer is re-locked).</summary>
        public void ResetLookFilter() => _spike?.Reset();

        private void Update()
        {
            if (_asset == null) return;
            var locks = _lock.Value;
            var moveOk = locks == null || !locks.IsLocked(InputLockFlags.Move);
            var lookOk = locks == null || !locks.IsLocked(InputLockFlags.Look);
            var fireOk = locks == null || !locks.IsLocked(InputLockFlags.Fire);
            var weaponsOk = locks == null || !locks.IsLocked(InputLockFlags.Weapons);
            var interactOk = locks == null || !locks.IsLocked(InputLockFlags.Interact);
            var jumpOk = locks == null || !locks.IsLocked(InputLockFlags.Jump);

            var mv = moveOk ? ReadV2(_move) : Vector2.zero;
            Move = mv.sqrMagnitude > 1f ? mv.normalized : mv;
            Sprint = moveOk && Held(_sprint);
            SprintDown = moveOk && Pressed(_sprint);
            CrouchDown = moveOk && Pressed(_crouch);

            var raw = ReadV2(_look);
            var dpr = _display.Value != null ? Mathf.Max(0.5f, _display.Value.DevicePixelRatio) : 1f;
            var css = raw / dpr;
            var filtered = _spike.Filter(css);
            Look = lookOk ? filtered : Vector2.zero;

            Fire = fireOk && Held(_fire);
            FireDown = fireOk && Pressed(_fire);
            Aim = fireOk && Held(_aim);
            AimDown = fireOk && Pressed(_aim);

            ReloadDown = weaponsOk && Pressed(_reload);
            Frag = weaponsOk && Held(_frag);
            FragDown = weaponsOk && Pressed(_frag);
            FragUp = weaponsOk && Released(_frag);
            MeleeDown = weaponsOk && Pressed(_melee);
            WeaponSlotDown = !weaponsOk ? 0 : Pressed(_weapon1) ? 1 : Pressed(_weapon2) ? 2 : 0;
            var s = weaponsOk && _scroll != null ? _scroll.ReadValue<float>() : 0f;
            Scroll = Mathf.Abs(s) > 0.01f ? Mathf.Sign(s) : 0f;

            Interact = interactOk && Held(_interact);
            InteractDown = interactOk && Pressed(_interact);
            JumpDown = jumpOk && Pressed(_jump);

            ObjectivesHeld = Held(_objectives);
            ObjectivesDown = Pressed(_objectives);
            PauseDown = Pressed(_pause);

            // Skip: hold Space 1 s, unscaled (works while time scale is 0 in cinematics, R3).
            _skipHeld = Held(_skip) ? _skipHeld + Time.unscaledDeltaTime : 0f;
            SkipHeld01 = Mathf.Clamp01(_skipHeld / InputIds.SkipHoldSeconds);
        }

        private static Vector2 ReadV2(InputAction a) => a != null ? a.ReadValue<Vector2>() : Vector2.zero;
        private static bool Held(InputAction a) => a != null && a.IsPressed();
        private static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();
        private static bool Released(InputAction a) => a != null && a.WasReleasedThisFrame();
    }
}
