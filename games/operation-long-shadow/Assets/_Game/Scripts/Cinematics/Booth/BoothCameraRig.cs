using UnityEngine;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// Cam_Booth in S0_Booth (TECH_PLAN §3.6: base camera, renderer 0, post on, SMAA, volume mask Booth, culling Booth),
    /// enabled only while a booth shot or insert owns the screen. <c>CinematicCameras</c> places it in the booth frame
    /// each frame (shot pose × limited look × breathing sway).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BoothCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        /// <summary>The booth camera.</summary>
        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            _camera.enabled = false;
        }

        /// <summary>Turns the camera on or off.</summary>
        public void SetActive(bool active)
        {
            var c = Camera;
            if (c != null && c.enabled != active) c.enabled = active;
        }

        /// <summary>Places the camera in the booth frame (local to the booth root) with a vertical FOV.</summary>
        public void SetLocalPose(Transform boothRoot, Vector3 localPosition, Quaternion localRotation, float verticalFov)
        {
            if (boothRoot != null)
                transform.SetPositionAndRotation(boothRoot.TransformPoint(localPosition), boothRoot.rotation * localRotation);
            else
                transform.SetPositionAndRotation(localPosition, localRotation);
            var c = Camera;
            if (c != null) c.fieldOfView = Mathf.Clamp(verticalFov, 1f, 120f);
        }
    }
}
