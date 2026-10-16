using UnityEngine;
using UnityEngine.InputSystem;

namespace FunUnity.WebGLTest
{
    /// <summary>
    /// Tiny smoke-test behaviour for the WebGL feasibility build.
    /// Rotates its transform, logs once on start, and (optionally) drives a camera
    /// with mouse delta while the pointer is locked, to verify Input System + pointer lock on Web.
    /// </summary>
    public class WebGLTestRotator : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 45f, 0f);
        [SerializeField] private Transform lookCamera;
        [SerializeField] private float mouseSensitivity = 0.1f;

        private float yaw;
        private float pitch;
        private int framesLogged;

        private void Start()
        {
            Debug.Log($"[WebGLTest] Start: Unity {Application.unityVersion}, platform {Application.platform}, " +
                      $"gfx {SystemInfo.graphicsDeviceType} / {SystemInfo.graphicsDeviceName}, " +
                      $"screen {Screen.width}x{Screen.height}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            if (lookCamera != null)
            {
                Vector3 e = lookCamera.eulerAngles;
                yaw = e.y;
                pitch = e.x > 180f ? e.x - 360f : e.x;
            }
        }

        private void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (lookCamera != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue();
                if (d.sqrMagnitude > 0f && framesLogged < 3)
                {
                    framesLogged++;
                    Debug.Log($"[WebGLTest] mouse delta {d}");
                }
                yaw += d.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * mouseSensitivity, -85f, 85f);
                lookCamera.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }
    }
}
