using LongShadow.Core;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Every asset path the WP10 Look builders write or read (TECH_PLAN §4.1, §4.2 naming; ownership §11.1: WP10 owns
    /// <c>Settings/{Volumes,Lighting}</c>, <c>Art/Shaders</c>, <c>Art/HDRI</c>, <c>Art/VFX</c>, <c>Data/Lighting</c>,
    /// <c>Prefabs/{Props,FX}</c>).
    /// </summary>
    public static class LookPaths
    {
        // ---- folders
        public const string VolumesFolder = "Assets/_Game/Settings/Volumes";
        public const string LightingSettingsFolder = "Assets/_Game/Settings/Lighting";
        public const string LightingDataFolder = "Assets/_Game/Data/Lighting";
        public const string ShaderFolder = "Assets/_Game/Art/Shaders/Look";
        public const string SkyMaterialFolder = "Assets/_Game/Art/Materials/Sky";
        public const string FxMaterialFolder = "Assets/_Game/Art/Materials/FX/Look";
        public const string FxTextureFolder = "Assets/_Game/Art/Textures/FX";
        public const string FxPrefabFolder = "Assets/_Game/Prefabs/FX";
        public const string LampPrefabFolder = "Assets/_Game/Prefabs/Props/Lamps";
        public const string VfxFolder = "Assets/_Game/Art/VFX";
        public const string HdriRuntimeFolder = "Assets/_Game/Art/HDRI/Runtime";
        public const string HdriEditorOnlyFolder = "Assets/_Game/Art/HDRI/EditorOnly";

        // ---- shaders (names)
        public const string ShaderPanoBlend = "Sky/PanoBlend";
        public const string ShaderSunDisc = "LongShadow/World/SunDisc";
        public const string ShaderBackdrop = "LongShadow/World/Backdrop";
        public const string ShaderLightShaft = "LongShadow/World/LightShaft";
        public const string ShaderDustMotes = "LongShadow/World/DustMotes";
        public const string ShaderLampGlow = "LongShadow/World/LampGlow";
        public const string ShaderFoliageWind = "LongShadow/World/FoliageWind";
        public const string ShaderCubeToEquirect = "Hidden/LongShadow/CubeToEquirect";
        public const string ShaderLit = "Universal Render Pipeline/Lit";
        public const string ShaderBakedLit = "Universal Render Pipeline/Baked Lit";
        public const string ShaderParticlesSimpleLit = "Universal Render Pipeline/Particles/Simple Lit";
        public const string ShaderParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";

        // ---- volume profiles (TECH_PLAN §3.7)
        public const string VPBase = VolumesFolder + "/VP_Base.asset";
        public const string VPGolden = VolumesFolder + "/VP_Golden.asset";
        public const string VPSunset = VolumesFolder + "/VP_Sunset.asset";
        public const string VPAfterglow = VolumesFolder + "/VP_Afterglow.asset";
        public const string VPBlueHour = VolumesFolder + "/VP_BlueHour.asset";
        public const string VPInteriorCinema = VolumesFolder + "/VP_Interior_Cinema.asset";
        public const string VPInteriorHall = VolumesFolder + "/VP_Interior_Hall.asset";
        public const string VPInteriorScreening = VolumesFolder + "/VP_Interior_Screening.asset";
        public const string VPInteriorBasement = VolumesFolder + "/VP_Interior_Basement.asset";
        public const string VPBooth = VolumesFolder + "/VP_Booth.asset";
        public const string VPCinematic = VolumesFolder + "/VP_Cinematic.asset";

        // ---- materials
        public const string MatSky = SkyMaterialFolder + "/M_Sky_PanoBlend.mat";
        public const string MatSkyBake = "Assets/_Game/Art/HDRI/EditorOnly/M_Sky_BakeEnvironment.mat";
        public const string MatSunDisc = SkyMaterialFolder + "/M_SunDisc.mat";
        public const string MatBackdropRidge = SkyMaterialFolder + "/M_Backdrop_Ridge.mat";
        public const string MatBackdropHills = SkyMaterialFolder + "/M_Backdrop_FarHills.mat";
        public const string MatLightShaft = FxMaterialFolder + "/M_FX_LightShaft.mat";
        public const string MatProjectorBeam = FxMaterialFolder + "/M_FX_ProjectorBeam.mat";
        public const string MatDustMotes = FxMaterialFolder + "/M_FX_DustMotes.mat";
        public const string MatLampGlow = FxMaterialFolder + "/M_FX_LampGlow.mat";
        public const string MatLampGlass = FxMaterialFolder + "/M_FX_SodiumGlass.mat";
        public const string MatAviationLens = FxMaterialFolder + "/M_FX_AviationLens.mat";
        public const string MatFloodLens = FxMaterialFolder + "/M_FX_FloodLens.mat";
        public const string MatFlareEmissive = FxMaterialFolder + "/M_FX_FlareBurn.mat";
        public const string MatEmergencyLens = FxMaterialFolder + "/M_FX_EmergencyLens.mat";
        public const string MatTungstenBulb = FxMaterialFolder + "/M_FX_TungstenBulb.mat";
        public const string MatDustParticles = FxMaterialFolder + "/M_FX_DustParticles.mat";
        public const string MatSmokeParticles = FxMaterialFolder + "/M_FX_SmokeParticles.mat";
        public const string MatFireParticles = FxMaterialFolder + "/M_FX_FireParticles.mat";
        public const string MatLampHousing = FxMaterialFolder + "/M_FX_LampHousing.mat";

        // ---- textures generated by the builders
        public const string TexShaftNoise = FxTextureFolder + "/T_FX_ShaftNoise_BC.png";

        // ---- data
        public const string TuningAsset = LightingDataFolder + "/LST_Default.asset";
        public const string AmbientSHAsset = LightingDataFolder + "/AmbientSHSet.asset";
        public const string LensFlareSun = VfxFolder + "/LF_Sun.asset";

        // ---- prefabs
        public const string LookRigPrefab = FxPrefabFolder + "/Look/PF_LookRig.prefab";
        public const string SodiumLampHeadPrefab = LampPrefabFolder + "/PF_SodiumLamp_Head.prefab";
        public const string SodiumLampSpritePrefab = LampPrefabFolder + "/PF_SodiumLamp_Sprite.prefab";
        public const string AviationLightsPrefab = LampPrefabFolder + "/PF_AviationLights.prefab";
        public const string FloodlightPrefab = LampPrefabFolder + "/PF_Floodlight.prefab";
        public const string EmergencyLightPrefab = LampPrefabFolder + "/PF_EmergencyLight.prefab";
        public const string TungstenFillPrefab = LampPrefabFolder + "/PF_TungstenFill.prefab";
        public const string ParachuteFlarePrefab = FxPrefabFolder + "/PF_FX_ParachuteFlare.prefab";
        public const string FireTechnicalPrefab = FxPrefabFolder + "/PF_FX_FireTechnical.prefab";
        public const string LightShaftPrefab = FxPrefabFolder + "/PF_FX_LightShaft.prefab";
        public const string ProjectorBeamPrefab = FxPrefabFolder + "/PF_FX_ProjectorBeam.prefab";
        public const string DustMotesPrefab = FxPrefabFolder + "/PF_FX_DustMotes.prefab";
        public const string RotorDownwashPrefab = FxPrefabFolder + "/PF_FX_RotorDownwash.prefab";
        public const string RotorShadowPrefab = FxPrefabFolder + "/PF_FX_RotorShadow.prefab";

        // ---- manifest ids (Docs/ASSET_MANIFEST.json)
        public const string HdriSunset = "HDRI_KIARA_8_SUNSET";
        public const string HdriDusk = "HDRI_KIARA_9_DUSK";
        public const string TexLensDirt = "TEX_LENS_DIRT";
        public const string PropSecurityLight = "PH_SECURITY_LIGHT";
        public const string VfxSmoke = "TEX_VFX_SMOKE_FLIPBOOK";
        public const string VfxFire = "TEX_VFX_FIRE_FLIPBOOK";
        public const string VfxDust = "TEX_VFX_DUST_IMPACT";
        public const string VehicleHeli = "VEH_HELICOPTER";

        /// <summary>LightingSettings asset of a section (TECH_PLAN §3.8: <c>LS_&lt;Section&gt;.lighting</c>).</summary>
        public static string LightingSettingsPath(SectionId id) => LightingSettingsFolder + "/LS_" + Sections.SceneName(id) + ".lighting";

        /// <summary>Folder of a section's lightmap sets.</summary>
        public static string LightmapSetFolder(SectionId id) => LightingDataFolder + "/" + Sections.SceneName(id);
    }
}
