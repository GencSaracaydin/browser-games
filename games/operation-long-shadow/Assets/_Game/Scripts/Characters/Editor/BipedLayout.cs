namespace LongShadow.Characters.Editor
{
    /// <summary>Hierarchy flavour of a Rocketbox (3ds Max Biped) skeleton.</summary>
    public enum BipedLayout
    {
        /// <summary>Not a Rocketbox Biped (no "Bip01 Pelvis").</summary>
        NotBiped = 0,
        /// <summary>
        /// Fixed offline by the content pipeline (Tools/characters/blender/rb_export.py): thighs under Bip01 Pelvis,
        /// clavicles under Bip01 Spine2, default node pose a T-pose. Imported as is.
        /// </summary>
        Fixed = 1,
        /// <summary>
        /// Raw Max export: thighs under Bip01 Spine, clavicles under Bip01 Neck, A-pose. RocketboxImporter re-parents the
        /// bones before the avatar is built and generates a T-pose skeleton for the avatar.
        /// </summary>
        Raw = 2,
    }
}
