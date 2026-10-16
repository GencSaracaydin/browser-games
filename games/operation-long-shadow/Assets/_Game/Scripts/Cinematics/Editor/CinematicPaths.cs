namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Asset locations written or read by the WP09 builders (TECH_PLAN §4.1 layout; ownership §11.1: Timelines/**,
    /// Prefabs/Booth/**, Scenes/Sections/S0_Booth, Art/Shaders/SG_CRT*|SG_Film*|SG_Gun*|SG_Glitch*). Content paths are the
    /// WP09_ART import index paths (file names per TECH_PLAN §4.2, which differ from the manifest's lower-case names).
    /// </summary>
    public static class CinematicPaths
    {
        public const string Timelines = "Assets/_Game/Timelines";
        public const string BoothPrefabs = "Assets/_Game/Prefabs/Booth";
        public const string BoothMeshes = "Assets/_Game/Prefabs/Booth/Meshes";
        public const string BoothMaterials = "Assets/_Game/Prefabs/Booth/Materials";
        public const string SystemsPrefab = "Assets/_Game/Prefabs/Booth/PF_CinematicSystems.prefab";
        public const string FxMaterials = "Assets/_Game/Art/Materials/FX/Cinematics";
        public const string Shaders = "Assets/_Game/Art/Shaders";
        public const string FxProfiles = "Assets/_Game/Settings/Volumes";
        public const string BoothScene = "Assets/_Game/Scenes/Sections/S0_Booth.unity";
        public const string BoothLighting = "Assets/_Game/Scenes/Sections/S0_Booth/LS_S0_Booth.lighting";
        public const string Report = "Logs/cinematics_report.txt";

        // ---- WP09_ART models (RawAssets/wp09/ready/import_index.json)
        public const string ModelChair = "Assets/_Game/Art/Models/Props/interrogation_chair/SM_InterrogationChair.fbx";
        public const string ModelCartLong = "Assets/_Game/Art/Models/Props/av_cart/SM_AVCartLong.fbx";
        public const string ModelCart = "Assets/_Game/Art/Models/Props/av_cart/SM_AVCart.fbx";
        public const string ModelScrambler = "Assets/_Game/Art/Models/Props/scrambler_box/SM_ScramblerBox.fbx";
        public const string ModelReel = "Assets/_Game/Art/Models/Props/reel_to_reel/SM_ReelToReel.fbx";
        public const string ModelPhone = "Assets/_Game/Art/Models/Props/rotary_phone/SM_RotaryPhone.fbx";
        public const string ModelAshtray = "Assets/_Game/Art/Models/Props/ashtray/SM_Ashtray.fbx";
        public const string ModelFilmCamera = "Assets/_Game/Art/Models/Props/film_camera/SM_FilmCamera16mm.fbx";
        public const string ModelTv1 = "Assets/_Game/Art/Models/Props/Television_01/SM_Television01.fbx";
        public const string ModelTv2 = "Assets/_Game/Art/Models/Props/television_02/SM_Television02.fbx";
        public const string ModelClock = "Assets/_Game/Art/Models/Props/wall_clock/SM_WallClock.fbx";
        public const string ModelLighter = "Assets/_Game/Art/Models/Props/vintage_lighter/SM_SteelLighter.fbx";
        public const string ModelBatten = "Assets/_Game/Art/Models/Props/mounted_fluorescent_lights/SM_FluorescentBatten.fbx";
        public const string ModelPen = "Assets/_Game/Art/Models/Props/booth_smalls/SM_BoothPen.fbx";
        public const string ModelElectrode = "Assets/_Game/Art/Models/Props/booth_smalls/SM_Electrode.fbx";
        public const string ModelElectrodeWire = "Assets/_Game/Art/Models/Props/booth_smalls/SM_ElectrodeWire.fbx";
        public const string ModelCigarette = "Assets/_Game/Art/Models/Props/booth_smalls/SM_Cigarette.fbx";

        // ---- materials (MaterialFactory outputs; WP09_ART materials.json and WP10 materials_cc0.json)
        public const string MatPlaster = "Assets/_Game/Art/Materials/Env/M_PaintedPlaster012.mat";
        public const string MatAcoustic = "Assets/_Game/Art/Materials/Env/M_AcousticTile.mat";
        public const string MatFloor = "Assets/_Game/Art/Materials/Env/M_ConcreteFloorWorn001.mat";
        public const string MatMirror = "Assets/_Game/Art/Materials/Env/M_OneWayMirror.mat";
        public const string MatDrainDecal = "Assets/_Game/Art/Materials/Env/M_BoothDrain_Decal.mat";
        public const string MatClockGlassCracked = "Assets/_Game/Art/Materials/Props/M_WallClock_Glass_Cracked.mat";
        public const string MatBoothParts = "Assets/_Game/Art/Materials/Props/M_BoothParts.mat";
        /// <summary>WP09_ART tile sizes ("tiling_m" in materials.json, which MaterialFactory does not read; DECISIONS [WP09]).</summary>
        public const float PlasterTileMetres = 2.0f;
        public const float AcousticTileMetres = 0.6096f;

        // ---- images (Art/Textures/Images)
        public const string ImgMap = "Assets/_Game/Art/Textures/Images/T_MapSardahn_BC.png";
        public const string ImgRecon = "Assets/_Game/Art/Textures/Images/T_ReconStation_BC.png";
        public const string ImgHarrou = "Assets/_Game/Art/Textures/Images/T_HarrouStill_BC.png";
        public const string ImgSurveil = "Assets/_Game/Art/Textures/Images/T_SurveilSparrow_BC.png";
        public const string ImgCodebook = "Assets/_Game/Art/Textures/Images/T_CodebookPage_BC.png";
        public const string ImgTeletype = "Assets/_Game/Art/Textures/Images/teletype_feed/T_TeletypeFeed_BC.png";
        public const string ImgRoster = "Assets/_Game/Art/Textures/Images/roster/T_RosterPage_FS_BC.png";
        public const string ImgTeamHollis = "Assets/_Game/Art/Textures/Images/team_id/T_TeamID_Hollis_BC.png";
        public const string ImgTeamSparrow = "Assets/_Game/Art/Textures/Images/team_id/T_TeamID_Sparrow_BC.png";
        public const string ImgTeamTinker = "Assets/_Game/Art/Textures/Images/team_id/T_TeamID_Tinker_BC.png";
        public const string ImgLeader5 = "Assets/_Game/Art/Textures/Images/leader/T_FilmLeader_5_BC.png";
        public const string ImgLeader4 = "Assets/_Game/Art/Textures/Images/leader/T_FilmLeader_4_BC.png";
        public const string ImgLeader3 = "Assets/_Game/Art/Textures/Images/leader/T_FilmLeader_3_BC.png";

        // ---- bodies and vehicles (WP05 builder, WP10 content)
        public const string Bodies = "Assets/_Game/Prefabs/Bodies";
        public const string BodyCalderBooth = "Assets/_Game/Prefabs/Bodies/PFB_Calder_Booth.prefab";
        public const string BodyInterrogator = "Assets/_Game/Prefabs/Bodies/PFB_Interrogator.prefab";
        public const string VehiclePrefabs = "Assets/_Game/Prefabs/Vehicles";
        /// <summary>WP10_LOOK FX prefabs (rotor downwash and shadow).</summary>
        public const string FxPrefabs = "Assets/_Game/Prefabs/FX";
        public const string ModelHelicopter = "Assets/_Game/Art/Models/Vehicles/helicopter/SM_Helicopter.fbx";
        public const string ModelHeliMainRotor = "Assets/_Game/Art/Models/Vehicles/helicopter/SM_HeliMainRotor.fbx";
        public const string ModelHeliTailRotor = "Assets/_Game/Art/Models/Vehicles/helicopter/SM_HeliTailRotor.fbx";

        // ---- fonts (WP08 OFL fonts; TMP font assets created by WP08/WP11)
        public const string Fonts = "Assets/_Game/Art/Fonts";

        // ---- dialogue
        public const string Dialogue = "Docs/DIALOGUE.json";
        public const string Envelopes = "Assets/_Game/Audio/Envelopes";
        public const string EnvelopesReady = "RawAssets/audio/ready/Assets/_Game/Audio/Envelopes";
    }
}
