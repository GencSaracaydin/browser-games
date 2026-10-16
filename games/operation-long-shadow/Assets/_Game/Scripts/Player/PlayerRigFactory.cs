using LongShadow.Core;
using LongShadow.Player.Body;
using LongShadow.Player.Bounds;
using LongShadow.Player.Footsteps;
using LongShadow.Player.Interaction;
using LongShadow.Player.Movement;
using LongShadow.Player.View;
using LongShadow.Player.Vitals;
using UnityEngine;

namespace LongShadow.Player
{
    /// <summary>
    /// Builds the PF_Player hierarchy in code (TECH_PLAN §7.3), so the editor <c>PlayerPrefabBuilder</c>, the EditMode
    /// tests and dev scenes all get exactly the same rig:
    /// <code>
    /// PF_Player (layer Player, tag Player): CharacterController, PlayerMotor, PlayerLook, PlayerCameraRig, PlayerHealth,
    ///   PlayerInteractor, Binoculars, FootstepEmitter, OutOfBoundsWatcher, PlayerShadowProxy, PlayerMount, PlayerRig
    ///   AimPoint (1.3 m)
    ///   ShadowProxySlot (layer Player; PFB_Calder_Shadow, D19)
    ///   Yaw (neck height) → Pitch → EyeOffset (= IPlayerRig.Eye) → Bob → Shake
    ///       Cam_World (Base, MainCamera) → Cam_Viewmodel (Overlay)
    ///       Viewmodel (layer Viewmodel; IPlayerRig.ViewmodelRoot) → FPBodySlot (PFB_Calder_FP), BinocularsSlot
    /// </code>
    /// No AudioListener (WP06 owns the only one). The root is built inactive and activated at the end (when
    /// <c>activate</c>) so every Awake sees a fully wired rig.
    /// </summary>
    public static class PlayerRigFactory
    {
        /// <summary>Vertical FOV of the default 90° horizontal at 16:9 (GDD §11), the camera's starting value.</summary>
        public const float DefaultVerticalFov = 58.72f;
        /// <summary>Chest height of the AI aim point (m).</summary>
        public const float AimPointHeight = 1.3f;

        /// <summary>Builds a complete rig at a position and compass heading.</summary>
        public static PlayerRig Build(Vector3 position, float yaw, Transform parent = null, bool activate = true)
        {
            var root = new GameObject(PlayerRigNames.Root);
            root.SetActive(false);
            root.tag = Tags.Player;
            root.layer = LayerIds.Player;
            var t = root.transform;
            if (parent != null) t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var aim = Child(t, PlayerRigNames.AimPoint, LayerIds.Player);
            aim.localPosition = new Vector3(0f, AimPointHeight, 0f);
            var shadowSlot = Child(t, PlayerRigNames.ShadowProxySlot, LayerIds.Player);

            var yawNode = Child(t, PlayerRigNames.Yaw, LayerIds.Player);
            var pitch = Child(yawNode, PlayerRigNames.Pitch, LayerIds.Player);
            var eye = Child(pitch, PlayerRigNames.EyeOffset, LayerIds.Player);
            var bob = Child(eye, PlayerRigNames.Bob, LayerIds.Player);
            var shake = Child(bob, PlayerRigNames.Shake, LayerIds.Player);

            var worldGo = new GameObject(PlayerRigNames.WorldCamera, typeof(Camera));
            worldGo.transform.SetParent(shake, false);
            var world = worldGo.GetComponent<Camera>();
            world.fieldOfView = DefaultVerticalFov;
            var vmGo = new GameObject(PlayerRigNames.ViewmodelCamera, typeof(Camera));
            vmGo.transform.SetParent(worldGo.transform, false);
            var vm = vmGo.GetComponent<Camera>();

            var vmRoot = Child(shake, PlayerRigNames.ViewmodelRoot, LayerIds.Viewmodel);
            var fpSlot = Child(vmRoot, PlayerRigNames.FirstPersonBodySlot, LayerIds.Viewmodel);
            var binocularsSlot = Child(vmRoot, PlayerRigNames.BinocularsSlot, LayerIds.Viewmodel);
            binocularsSlot.gameObject.SetActive(false);

            var cc = root.AddComponent<CharacterController>();
            var motor = root.AddComponent<PlayerMotor>();
            var look = root.AddComponent<PlayerLook>();
            var cameraRig = root.AddComponent<PlayerCameraRig>();
            var health = root.AddComponent<PlayerHealth>();
            var interactor = root.AddComponent<PlayerInteractor>();
            var binoculars = root.AddComponent<Binoculars>();
            var footsteps = root.AddComponent<FootstepEmitter>();
            var bounds = root.AddComponent<OutOfBoundsWatcher>();
            var shadow = root.AddComponent<PlayerShadowProxy>();
            var mount = root.AddComponent<PlayerMount>();
            var rig = root.AddComponent<PlayerRig>();

            motor.Bind(cc);
            look.Bind(yawNode, pitch);
            cameraRig.Bind(yawNode, eye, bob, shake, world, vm);
            health.Bind(aim);
            binoculars.Bind(binocularsSlot);
            footsteps.Bind(motor);
            bounds.Bind(cc, motor);
            shadow.Bind(shadowSlot);
            mount.Bind(motor, look);
            rig.Bind(cc, motor, look, cameraRig, health, interactor, binoculars, footsteps, bounds, shadow, mount, vmRoot, fpSlot);

            // Values the prefab must carry even before any Awake runs (edit-mode builds).
            PlayerCameraSetup.Configure(world, vm, cameraRig.Settings);
            var ms = motor.Settings;
            eye.localPosition = cameraRig.Settings.EyeOffsetFromNeck;
            yawNode.localPosition = new Vector3(0f, ms.StandEyeHeight - cameraRig.Settings.EyeOffsetFromNeck.y, 0f);

            if (activate) root.SetActive(true);
            return rig;
        }

        private static Transform Child(Transform parent, string name, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
