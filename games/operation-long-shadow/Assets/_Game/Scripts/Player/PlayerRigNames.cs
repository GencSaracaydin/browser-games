namespace LongShadow.Player
{
    /// <summary>
    /// Object names of the PF_Player hierarchy (TECH_PLAN §7.3: Root → Yaw → Pitch → EyeOffset → Bob → Shake →
    /// Cam_World + Cam_Viewmodel). Shared by <see cref="PlayerRigFactory"/>, the editor PlayerPrefabBuilder, the
    /// validator and the tests so nobody hard-codes them twice.
    /// </summary>
    public static class PlayerRigNames
    {
        /// <summary>Prefab and root object name.</summary>
        public const string Root = "PF_Player";
        /// <summary>Yaw node at neck height (yaw while mounted, crouch/landing dips).</summary>
        public const string Yaw = "Yaw";
        /// <summary>Pitch node (the neck pivot).</summary>
        public const string Pitch = "Pitch";
        /// <summary>Eye offset from the neck pivot; this is <c>IPlayerRig.Eye</c>.</summary>
        public const string EyeOffset = "EyeOffset";
        /// <summary>Head bob and roll.</summary>
        public const string Bob = "Bob";
        /// <summary>Trauma shake, view punch, breathing sway.</summary>
        public const string Shake = "Shake";
        /// <summary>Base camera (TECH_PLAN §3.6).</summary>
        public const string WorldCamera = "Cam_World";
        /// <summary>View-model overlay camera (TECH_PLAN D5, §3.6).</summary>
        public const string ViewmodelCamera = "Cam_Viewmodel";
        /// <summary>Parent of the first-person weapon (WP03) and body (WP05), layer Viewmodel.</summary>
        public const string ViewmodelRoot = "Viewmodel";
        /// <summary>Slot for PFB_Calder_FP (TECH_PLAN D19), under <see cref="ViewmodelRoot"/>.</summary>
        public const string FirstPersonBodySlot = "FPBodySlot";
        /// <summary>Slot for the binocular model (Beat 1), under <see cref="ViewmodelRoot"/>.</summary>
        public const string BinocularsSlot = "BinocularsSlot";
        /// <summary>Slot for PFB_Calder_Shadow (TECH_PLAN D19), under the root, layer Player.</summary>
        public const string ShadowProxySlot = "ShadowProxySlot";
        /// <summary>Name of the greybox shadow body created while PFB_Calder_Shadow has not landed.</summary>
        public const string ShadowPlaceholder = "PH_CalderShadow";
        /// <summary>Name of the greybox binoculars created while PH_BINOCULARS has not landed.</summary>
        public const string BinocularsPlaceholder = "PH_Binoculars";
        /// <summary>AI aim point (chest), child of the root.</summary>
        public const string AimPoint = "AimPoint";
    }
}
