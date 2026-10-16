namespace LongShadow.Characters.Editor
{
    /// <summary>Where a clip file comes from (the folder below Art/Animation/Clips/).</summary>
    public enum ClipSource
    {
        /// <summary>Unknown folder: imported with its own avatar and heuristic settings.</summary>
        Other = 0,
        /// <summary>Meshy rifle clips baked on the Rocketbox skeleton (copy SK_Calder's avatar).</summary>
        Meshy = 1,
        /// <summary>HY-Motion signals baked on the Rocketbox skeleton (copy SK_Calder's avatar).</summary>
        HYMotion = 2,
        /// <summary>Rocketbox Biped clips baked on the fixed hierarchy (copy SK_Calder's avatar).</summary>
        Rocketbox = 3,
        /// <summary>Finger pose clips A_Pose_* on the Rocketbox skeleton (copy SK_Calder's avatar).</summary>
        Poses = 4,
        /// <summary>Quaternius Universal Animation Library 1 (own mannequin avatar).</summary>
        UAL1 = 5,
        /// <summary>Quaternius Universal Animation Library 2 (own mannequin avatar).</summary>
        UAL2 = 6,
        /// <summary>Unity Standard Assets Characters (own DefaultMale avatar).</summary>
        SAC = 7,
    }
}
