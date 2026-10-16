namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Asset locations and names owned by WP05 (TECH_PLAN §4.1, §4.2, §11.1: Art/Models/Characters, Art/Textures/Characters,
    /// Art/Materials/Characters, Art/Animation/**, Prefabs/Bodies). Builders and importers use only these constants.
    /// </summary>
    public static class CharacterPaths
    {
        public const string ModelsRoot = "Assets/_Game/Art/Models/Characters";
        public const string PropsModelsRoot = "Assets/_Game/Art/Models/Characters/Props";
        public const string TexturesRoot = "Assets/_Game/Art/Textures/Characters";
        public const string MaterialsRoot = "Assets/_Game/Art/Materials/Characters";
        public const string AnimationRoot = "Assets/_Game/Art/Animation";
        public const string ClipsRoot = "Assets/_Game/Art/Animation/Clips";
        public const string GeneratedClipsFolder = "Assets/_Game/Art/Animation/Clips/Generated";
        public const string ControllersFolder = "Assets/_Game/Art/Animation/Controllers";
        public const string MasksFolder = "Assets/_Game/Art/Animation/Masks";
        public const string PosesFolder = "Assets/_Game/Art/Animation/Poses";
        public const string MannequinFolder = "Assets/_Game/Art/Models/Characters/_Mannequin";
        public const string BodiesFolder = "Assets/_Game/Prefabs/Bodies";
        public const string WeaponPrefabsFolder = "Assets/_Game/Prefabs/Weapons";
        public const string ReportPath = "Logs/characters_report.txt";

        /// <summary>The reference body: every clip baked on the Rocketbox skeleton copies this model's avatar.</summary>
        public const string ReferenceModel = "Assets/_Game/Art/Models/Characters/Calder/SK_Calder.fbx";

        /// <summary>Unity SAC avatar source: every A_SAC_* clip copies this rig's avatar (content WP05_ART §2).</summary>
        public const string SacRigModel = ClipsRoot + "/SAC/SK_SAC_DefaultMaleRig.fbx";

        // ---- controllers (TECH_PLAN §7.8)
        public const string SoldierController = ControllersFolder + "/AC_Soldier.controller";
        public const string ShadowController = ControllersFolder + "/AC_ShadowProxy.controller";
        public const string FPArmsController = ControllersFolder + "/AC_FPArms.controller";
        public const string LegionOverride = ControllersFolder + "/AOC_Legion.overrideController";
        public const string GreycoatOverride = ControllersFolder + "/AOC_Greycoat.overrideController";
        public const string SquadOverride = ControllersFolder + "/AOC_Squad.overrideController";
        public const string CivilianOverride = ControllersFolder + "/AOC_Civilian.overrideController";

        // ---- avatar masks
        public const string UpperBodyMask = MasksFolder + "/AM_UpperBody.mask";
        public const string ArmsMask = MasksFolder + "/AM_Arms.mask";
        public const string HandsMask = MasksFolder + "/AM_Hands.mask";

        /// <summary>Prefab path of a body (PFB_*).</summary>
        public static string BodyPrefab(string prefabName) => BodiesFolder + "/" + prefabName + ".prefab";
    }
}
