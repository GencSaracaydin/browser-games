using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// The three Humanoid avatar masks of the WP05 controllers (TECH_PLAN §7.8): <c>AM_UpperBody</c> (body, head, arms,
    /// fingers: additive hit reactions), <c>AM_Arms</c> (arms and fingers: weapon hold and arm actions over locomotion) and
    /// <c>AM_Hands</c> (fingers only: finger poses). Rebuilt in place (GUIDs kept).
    /// </summary>
    public static class AvatarMaskBuilder
    {
        /// <summary>Builds the masks and returns them (upper body, arms, hands).</summary>
        public static AvatarMask[] BuildAll()
        {
            return new[]
            {
                Build(CharacterPaths.UpperBodyMask, AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm,
                      AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
                      AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK),
                Build(CharacterPaths.ArmsMask, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers,
                      AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK),
                Build(CharacterPaths.HandsMask, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers),
            };
        }

        /// <summary>Creates or rewrites a mask with only <paramref name="active"/> parts enabled.</summary>
        public static AvatarMask Build(string path, params AvatarMaskBodyPart[] active)
        {
            BuilderUtil.EnsureFolder(CharacterPaths.MasksFolder);
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            var created = mask == null;
            if (created) mask = new AvatarMask();
            for (var p = AvatarMaskBodyPart.Root; p < AvatarMaskBodyPart.LastBodyPart; p++)
                mask.SetHumanoidBodyPartActive(p, System.Array.IndexOf(active, p) >= 0);
            mask.transformCount = 0;
            if (created) AssetDatabase.CreateAsset(mask, path);
            else EditorUtility.SetDirty(mask);
            return mask;
        }
    }
}
