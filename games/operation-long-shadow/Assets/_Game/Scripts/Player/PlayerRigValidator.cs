using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Player.View;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Player
{
    /// <summary>
    /// Checks a PF_Player hierarchy against TECH_PLAN §3.6, §3.9, §7.3, D5 and D19 (used by the editor validator menu,
    /// the prefab builder and the tests): components, names, layers, capsule, both cameras, the camera stack, no
    /// AudioListener, view-model renderers that never cast shadows, and a shadow body that renders shadows only.
    /// </summary>
    public static class PlayerRigValidator
    {
        /// <summary>Appends every problem found to <paramref name="problems"/>; returns true when there are none.</summary>
        public static bool Validate(GameObject root, List<string> problems)
        {
            var before = problems.Count;
            if (root == null)
            {
                problems.Add("no root object");
                return false;
            }
            var t = root.transform;
            if (root.layer != LayerIds.Player) problems.Add("root layer is not Player (6)");
            if (!root.CompareTag(Tags.Player)) problems.Add("root tag is not Player");
            var rig = root.GetComponent<PlayerRig>();
            if (rig == null) problems.Add("PlayerRig component missing");

            var cc = root.GetComponent<CharacterController>();
            if (cc == null) problems.Add("CharacterController missing");
            else
            {
                if (Mathf.Abs(cc.height - 1.8f) > 0.01f) problems.Add("capsule height " + cc.height + " (GDD §12: 1.8 m)");
                if (Mathf.Abs(cc.center.y - cc.height * 0.5f) > 0.01f) problems.Add("capsule centre is not at half height (feet at the root)");
                if (Mathf.Abs(cc.stepOffset - 0.35f) > 0.01f) problems.Add("step offset " + cc.stepOffset + " (TECH_PLAN §7.3: 0.35)");
                if (Mathf.Abs(cc.slopeLimit - 45f) > 0.1f) problems.Add("slope limit " + cc.slopeLimit + " (TECH_PLAN §7.3: 45)");
            }

            var names = new[]
            {
                PlayerRigNames.Yaw, PlayerRigNames.Pitch, PlayerRigNames.EyeOffset, PlayerRigNames.Bob, PlayerRigNames.Shake,
                PlayerRigNames.WorldCamera, PlayerRigNames.ViewmodelCamera, PlayerRigNames.ViewmodelRoot,
                PlayerRigNames.FirstPersonBodySlot, PlayerRigNames.BinocularsSlot, PlayerRigNames.ShadowProxySlot, PlayerRigNames.AimPoint,
            };
            foreach (var n in names)
                if (FindDeep(t, n) == null) problems.Add("missing child '" + n + "'");

            var shake = FindDeep(t, PlayerRigNames.Shake);
            if (shake != null && !IsChainOf(t, shake, PlayerRigNames.Yaw, PlayerRigNames.Pitch, PlayerRigNames.EyeOffset, PlayerRigNames.Bob, PlayerRigNames.Shake))
                problems.Add("camera chain is not Root → Yaw → Pitch → EyeOffset → Bob → Shake");

            var worldT = FindDeep(t, PlayerRigNames.WorldCamera);
            var vmT = FindDeep(t, PlayerRigNames.ViewmodelCamera);
            var world = worldT != null ? worldT.GetComponent<Camera>() : null;
            var vm = vmT != null ? vmT.GetComponent<Camera>() : null;
            if (world == null) problems.Add("Cam_World has no Camera");
            if (vm == null) problems.Add("Cam_Viewmodel has no Camera");
            if (world != null)
            {
                if (!world.CompareTag(Tags.MainCamera)) problems.Add("Cam_World is not tagged MainCamera");
                if (world.cullingMask != PlayerLayers.WorldCameraCulling) problems.Add("Cam_World culling must exclude Viewmodel, Booth and Trigger");
                if (Mathf.Abs(world.nearClipPlane - 0.05f) > 1e-3f || Mathf.Abs(world.farClipPlane - 3000f) > 0.5f) problems.Add("Cam_World near/far must be 0.05/3000");
                if (world.TryGetComponent(out UniversalAdditionalCameraData d) && d.renderType != CameraRenderType.Base) problems.Add("Cam_World must be a Base camera");
            }
            if (vm != null)
            {
                if (vm.cullingMask != PlayerLayers.ViewmodelCameraCulling) problems.Add("Cam_Viewmodel must cull everything but Viewmodel");
                if (Mathf.Abs(vm.fieldOfView - 55f) > 0.01f) problems.Add("Cam_Viewmodel FOV must be 55 (TECH_PLAN D5)");
                if (Mathf.Abs(vm.nearClipPlane - 0.01f) > 1e-4f || Mathf.Abs(vm.farClipPlane - 5f) > 0.01f) problems.Add("Cam_Viewmodel near/far must be 0.01/5");
                if (vm.TryGetComponent(out UniversalAdditionalCameraData v))
                {
                    if (v.renderType != CameraRenderType.Overlay) problems.Add("Cam_Viewmodel must be an Overlay camera");
                    if (v.renderShadows) problems.Add("Cam_Viewmodel must not render shadows");
                }
                if (world != null && UniversalRenderPipeline.asset != null && !PlayerCameraSetup.IsStacked(world, vm))
                    problems.Add("Cam_Viewmodel is not in Cam_World's camera stack");
            }

            if (root.GetComponentsInChildren<AudioListener>(true).Length > 0) problems.Add("AudioListener on the rig (WP06 owns the only one)");

            var vmRoot = FindDeep(t, PlayerRigNames.ViewmodelRoot);
            if (vmRoot != null)
            {
                foreach (var tr in vmRoot.GetComponentsInChildren<Transform>(true))
                    if (tr.gameObject.layer != LayerIds.Viewmodel) { problems.Add("'" + tr.name + "' under Viewmodel is not on layer Viewmodel"); break; }
                foreach (var r in vmRoot.GetComponentsInChildren<Renderer>(true))
                    if (r.shadowCastingMode != ShadowCastingMode.Off) { problems.Add("view-model renderer '" + r.name + "' casts shadows (D5)"); break; }
            }
            var shadowSlot = FindDeep(t, PlayerRigNames.ShadowProxySlot);
            if (shadowSlot != null)
            {
                foreach (var tr in shadowSlot.GetComponentsInChildren<Transform>(true))
                    if (tr.gameObject.layer != LayerIds.Player) { problems.Add("'" + tr.name + "' under ShadowProxySlot is not on layer Player"); break; }
                foreach (var r in shadowSlot.GetComponentsInChildren<Renderer>(true))
                    if (r.shadowCastingMode != ShadowCastingMode.ShadowsOnly) { problems.Add("shadow-proxy renderer '" + r.name + "' is not ShadowsOnly (D19)"); break; }
            }
            return problems.Count == before;
        }

        private static bool IsChainOf(Transform root, Transform leaf, params string[] chain)
        {
            var t = leaf;
            for (var i = chain.Length - 1; i >= 0; i--)
            {
                if (t == null || t.name != chain[i]) return false;
                t = t.parent;
            }
            return t == root;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
                var d = FindDeep(c, name);
                if (d != null) return d;
            }
            return null;
        }
    }
}
