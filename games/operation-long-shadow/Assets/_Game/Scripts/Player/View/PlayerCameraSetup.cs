using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Player.View
{
    /// <summary>
    /// Applies the TECH_PLAN §3.6 camera settings to Cam_World and Cam_Viewmodel ("values set by code"). Used by the
    /// rig at Awake and by the editor PlayerPrefabBuilder, so the prefab and the runtime always agree. Idempotent.
    /// <list type="bullet">
    /// <item>Cam_World: Base, renderer 0, near 0.05, far 3000, culling everything but Viewmodel/Booth/Trigger, shadows,
    /// depth texture on, opaque texture off, volume mask Default, tag MainCamera.</item>
    /// <item>Cam_Viewmodel: Overlay stacked on Cam_World, culling Viewmodel only, FOV 55° vertical, near 0.01, far 5,
    /// clears depth, renders no shadows (TECH_PLAN D5).</item>
    /// <item>Post-processing (and with it SMAA High) runs on exactly one camera of the stack (<see cref="ViewmodelPostMode"/>):
    /// URP applies an overlay camera's post pass to the whole stack's colour buffer, so grading on both would grade the
    /// world twice. Hide the view model with Cam_Viewmodel's culling mask, never by disabling it.</item>
    /// </list>
    /// No AudioListener is ever added: the only listener belongs to WP06's AudioService (WP00 handoff).
    /// </summary>
    public static class PlayerCameraSetup
    {
        /// <summary>Configures both cameras and the stack.</summary>
        public static void Configure(Camera world, Camera viewmodel, CameraSettings s)
        {
            if (s == null) s = new CameraSettings();
            var postOnOverlay = s.PostMode == ViewmodelPostMode.GradeOnceOnOverlay && viewmodel != null;

            if (world != null)
            {
                world.nearClipPlane = s.WorldNear;
                world.farClipPlane = s.WorldFar;
                world.cullingMask = PlayerLayers.WorldCameraCulling;
                world.clearFlags = CameraClearFlags.Skybox;
                world.allowHDR = true;
                world.allowMSAA = false;
                world.usePhysicalProperties = false;
                world.depth = 0f;
                if (!world.CompareTag(Tags.MainCamera)) world.tag = Tags.MainCamera;
                var d = world.GetUniversalAdditionalCameraData();
                d.renderType = CameraRenderType.Base;
                d.SetRenderer(0);
                d.renderShadows = true;
                d.requiresDepthOption = CameraOverrideOption.On;
                d.requiresColorOption = CameraOverrideOption.Off;
                d.volumeLayerMask = 1 << LayerIds.Default;
                d.renderPostProcessing = !postOnOverlay;
                // SMAA High on both cameras: URP runs anti-aliasing inside the post pass of whichever camera grades the
                // stack, and overlay cameras may inherit the base camera's mode, so both carry it (it only runs once).
                d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                d.antialiasingQuality = AntialiasingQuality.High;
                d.dithering = !postOnOverlay;
                d.stopNaN = false;
            }

            if (viewmodel != null)
            {
                viewmodel.fieldOfView = s.ViewmodelFov;
                viewmodel.nearClipPlane = s.ViewmodelNear;
                viewmodel.farClipPlane = s.ViewmodelFar;
                viewmodel.cullingMask = PlayerLayers.ViewmodelCameraCulling;
                viewmodel.clearFlags = CameraClearFlags.Depth;
                viewmodel.allowHDR = true;
                viewmodel.allowMSAA = false;
                viewmodel.usePhysicalProperties = false;
                viewmodel.depth = 1f;
                var v = viewmodel.GetUniversalAdditionalCameraData();
                v.renderType = CameraRenderType.Overlay;
                v.SetRenderer(0);
                v.renderShadows = false;
                v.requiresDepthOption = CameraOverrideOption.Off;
                v.requiresColorOption = CameraOverrideOption.Off;
                v.volumeLayerMask = 1 << LayerIds.Default;
                v.renderPostProcessing = postOnOverlay;
                v.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                v.antialiasingQuality = AntialiasingQuality.High;
                v.dithering = postOnOverlay;
                v.stopNaN = false;
                if (world != null) AddToStack(world, viewmodel);
            }
        }

        /// <summary>True if <paramref name="overlay"/> is in the base camera's stack.</summary>
        public static bool IsStacked(Camera world, Camera overlay)
        {
            if (world == null || overlay == null || UniversalRenderPipeline.asset == null) return false;
            if (!world.TryGetComponent(out UniversalAdditionalCameraData d) || d.renderType != CameraRenderType.Base) return false;
            var stack = d.cameraStack;
            return stack != null && stack.Contains(overlay);
        }

        private static void AddToStack(Camera world, Camera overlay)
        {
            // cameraStack needs the URP renderer; without an active URP asset (bare test runners) skip quietly.
            if (UniversalRenderPipeline.asset == null)
            {
                LSLog.WarnOnce("player-cam-nourp", LogCategory.Player, "No URP asset active: Cam_Viewmodel was not added to the camera stack.");
                return;
            }
            var d = world.GetUniversalAdditionalCameraData();
            if (d.renderType != CameraRenderType.Base) return;
            var stack = d.cameraStack;
            if (stack == null) return;
            stack.RemoveAll(c => c == null);
            if (!stack.Contains(overlay)) stack.Add(overlay);
        }
    }
}
