using System;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LongShadow.UI.Dev
{
    /// <summary>
    /// <see cref="IPlayerRig"/> stand-in for the UI test bed: a free camera (hold the right mouse button to look, WASD to
    /// move, Z toggles ADS, X toggles sprint) so markers, the compass, nameplates and the damage ring can be checked
    /// without WP02. Every other member is inert.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FakePlayerRig : MonoBehaviour, IPlayerRig
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _lookSpeed = 0.15f;
        [SerializeField] private bool _register = true;

        private float _pitch, _yaw;
        private bool _ads;
        private float _adsBlend;
        private bool _sprint;

        public Transform Root => transform;
        public Transform Eye => _camera != null ? _camera.transform : transform;
        public Camera WorldCamera => _camera;
        public Camera ViewmodelCamera => null;
        public Transform ViewmodelRoot => null;
        public Vector3 Velocity { get; private set; }
        public Stance Stance => _sprint ? Stance.Sprint : Stance.Stand;
        public bool IsGrounded => true;
        public bool IsADS => _ads;
        public float AdsBlend => _adsBlend;
        public float SpeedMultiplier { set { } }
        public float TimeSinceSprintEnd => 1f;
        public float BobPhase => 0f;
        public float BobAmount01 => 0f;
        public Ray ViewRay => new Ray(Eye.position, Eye.forward);
        public bool IsGlassing { get; set; }
        public float Pitch => _pitch;
        public float Yaw => _yaw;

#pragma warning disable 67 // part of the interface; the fake never lands
        public event Action<float> Landed;
#pragma warning restore 67

        /// <summary>Sets the camera (builder).</summary>
        public void SetCamera(Camera cam) => _camera = cam;

        /// <summary>Toggles ADS (test bed key Z).</summary>
        public void ToggleAds() => _ads = !_ads;

        /// <summary>Toggles sprint (test bed key X).</summary>
        public void ToggleSprint() => _sprint = !_sprint;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            if (_register) GameServices.Register<IPlayerRig>(this);
            var e = transform.eulerAngles;
            _yaw = e.y;
        }

        private void OnDestroy()
        {
            if (_register) GameServices.Unregister<IPlayerRig>(this);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _adsBlend = Mathf.MoveTowards(_adsBlend, _ads ? 1f : 0f, dt * 6f);
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                _yaw += d.x * _lookSpeed;
                _pitch = Mathf.Clamp(_pitch - d.y * _lookSpeed, -80f, 80f);
            }
            var move = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed) move.z -= 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
            }
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_camera != null) _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            Velocity = transform.TransformDirection(move.normalized) * _moveSpeed * (_sprint ? 1.6f : 1f);
            transform.position += Velocity * dt;
        }

        public void AddAimDelta(float pitch, float yaw)
        {
            _pitch += pitch;
            _yaw += yaw;
        }

        public void SetLookAngles(float pitch, float yaw)
        {
            _pitch = pitch;
            _yaw = yaw;
        }

        public void Teleport(Vector3 position, float yaw)
        {
            transform.position = position;
            _yaw = yaw;
        }

        public void SetAdsState(bool isAds, float blend01)
        {
            _ads = isAds;
            _adsBlend = blend01;
        }

        public void SetViewPunch(Quaternion punch) { }
        public void RequestFov(object owner, float vFov, float blendTime) { }
        public void ReleaseFov(object owner) { }
        public void Shake(float trauma) { }
        public void SetViewActive(bool active) { }
        public void SetForcedWalk(float? speed) { }
        public void SetLookLimits(Quaternion center, float yaw, float up, float down) { }
        public void ClearLookLimits() { }
        public void SetDamageModifiers(float scale, float floorHp) { }
        public void Mount(Transform seat) { }
        public void Dismount() { }
        public void SetBinocularsAvailable(bool available) { }
        public void SetBodyVisibility(bool viewmodel, bool shadowProxy) { }
        public void SetEyeHeightOverride(float? eyeHeight, float blendSeconds) { }
    }
}
