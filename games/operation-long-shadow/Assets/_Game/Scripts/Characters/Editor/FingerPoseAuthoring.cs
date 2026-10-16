using System.Collections.Generic;
using LongShadow.Characters.Animation;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// The five finger poses of the Hands layer (TECH_PLAN §7.8 "Finger poses authored by FingerPoseAuthoring as Humanoid
    /// muscle curves": A_Pose_RifleGrip, A_Pose_PistolGrip, A_Pose_Fist, A_Pose_Relaxed, A_Pose_FlatHands). The content
    /// pipeline also bakes them as FBX clips in Art/Animation/Clips/Poses (posed on the Rocketbox finger bones); those win in
    /// <see cref="ClipResolver"/>. This tool writes the muscle-curve versions to Art/Animation/Poses/*.anim so the Hands
    /// layer always has a pose. Re-running overwrites the curves in place (GUIDs kept).
    /// </summary>
    public static class FingerPoseAuthoring
    {
        /// <summary>(Re)builds the five pose clips and returns them by pose.</summary>
        public static Dictionary<HandPose, AnimationClip> BuildAll()
        {
            var result = new Dictionary<HandPose, AnimationClip>();
            BuilderUtil.EnsureFolder(CharacterPaths.PosesFolder);
            foreach (HandPose pose in System.Enum.GetValues(typeof(HandPose)))
            {
                var name = ClipKeys.ForHandPose(pose);
                var path = CharacterPaths.PosesFolder + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                var created = clip == null;
                if (created) clip = new AnimationClip { name = name, frameRate = 30f };
                else clip.ClearCurves();
                ProceduralClipFactory.FillHandPose(clip, pose);
                if (created) AssetDatabase.CreateAsset(clip, path);
                else EditorUtility.SetDirty(clip);
                result[pose] = clip;
            }
            AssetDatabase.SaveAssets();
            LSLog.Info(LogCategory.Characters, "FingerPoseAuthoring: " + result.Count + " finger pose clip(s) written to " + CharacterPaths.PosesFolder);
            return result;
        }
    }
}
