using System.Collections.Generic;
using System.Reflection;
using LongShadow.Characters.Animation;
using LongShadow.Characters.Editor;
using LongShadow.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Characters
{
    public class ClipPipelineTests
    {
        [Test]
        public void SourceFromTheFolder()
        {
            Assert.That(ClipRules.SourceOf("Assets/_Game/Art/Animation/Clips/Meshy/A_Meshy_Idle.fbx"), Is.EqualTo(ClipSource.Meshy));
            Assert.That(ClipRules.SourceOf("Assets/_Game/Art/Animation/Clips/UAL2/UAL2_Standard_RM.fbx"), Is.EqualTo(ClipSource.UAL2));
            Assert.That(ClipRules.SourceOf("Assets/_Game/Art/Animation/Clips/SAC/m@WalkForwards.fbx"), Is.EqualTo(ClipSource.SAC));
            Assert.That(ClipRules.SourceOf("Assets/_Game/Art/Animation/Clips/Poses/A_Pose_Fist.fbx"), Is.EqualTo(ClipSource.Poses));
            Assert.That(ClipRules.SourceOf("Assets/Other/x.fbx"), Is.EqualTo(ClipSource.Other));
            Assert.That(ClipRules.CopiesReferenceAvatar(ClipSource.Meshy), Is.True);
            Assert.That(ClipRules.CopiesReferenceAvatar(ClipSource.UAL1), Is.False);
            Assert.That(ClipRules.IsClipModel("Assets/_Game/Art/Animation/Clips/Meshy/A_Meshy_Idle.fbx"), Is.True);
            Assert.That(ClipRules.IsClipModel("Assets/_Game/Art/Animation/Clips/Generated/PH_Stand.anim"), Is.False);
            Assert.That(ClipRules.IsRootMotionFile("Assets/_Game/Art/Animation/Clips/UAL1/UAL1_Standard_RM.fbx"), Is.True);
        }

        [Test]
        public void ClipNamesAreDeterministic()
        {
            Assert.That(ClipRules.ClipName(ClipSource.Meshy, "A_Meshy_Idle", "Take 001", 1, false), Is.EqualTo("A_Meshy_Idle"));
            Assert.That(ClipRules.ClipName(ClipSource.UAL1, "UAL1_Standard", "Armature|Idle_Loop", 43, false), Is.EqualTo("A_UAL1_Idle_Loop"));
            Assert.That(ClipRules.ClipName(ClipSource.UAL2, "UAL2_Standard_RM", "ClimbUp_1m", 43, true), Is.EqualTo("A_UAL2RM_ClimbUp_1m"));
            Assert.That(ClipRules.ClipName(ClipSource.SAC, "m@StrafeRight", "Take 001", 1, false), Is.EqualTo("A_SAC_StrafeRight"));
            Assert.That(ClipRules.ClipName(ClipSource.SAC, "A_SAC_StrafeRight", "Take 001", 1, false), Is.EqualTo("A_SAC_StrafeRight"));
            Assert.That(ClipRules.ClipName(ClipSource.UAL1, "A_UAL1_Library_RM", "Armature|Jump_Land", 43, true), Is.EqualTo("A_UAL1RM_Jump_Land"));
            Assert.That(ClipRules.ClipName(ClipSource.Other, "Clap", "Take 001", 1, false), Is.EqualTo("A_Other_Clap"));
        }

        [Test]
        public void AvatarSourcePerFile()
        {
            Assert.That(ClipRules.AvatarSourceFor("Assets/_Game/Art/Animation/Clips/Meshy/A_Meshy_Idle.fbx"), Is.EqualTo(CharacterPaths.ReferenceModel));
            Assert.That(ClipRules.AvatarSourceFor("Assets/_Game/Art/Animation/Clips/Poses/A_Pose_Fist.fbx"), Is.EqualTo(CharacterPaths.ReferenceModel));
            Assert.That(ClipRules.AvatarSourceFor("Assets/_Game/Art/Animation/Clips/SAC/A_SAC_Idle.fbx"), Is.EqualTo(CharacterPaths.SacRigModel));
            Assert.That(ClipRules.AvatarSourceFor(CharacterPaths.SacRigModel), Is.Null, "the SAC rig builds its own avatar");
            Assert.That(ClipRules.AvatarSourceFor("Assets/_Game/Art/Animation/Clips/UAL1/A_UAL1_Library.fbx"), Is.Null);
        }

        [Test]
        public void AliasesAreIdenticalCopies()
        {
            var defaults = new[] { new ModelImporterClipAnimation { takeName = "Take 001", name = "Take 001", firstFrame = 0, lastFrame = 120 } };
            var clips = ClipRules.Configure("Assets/_Game/Art/Animation/Clips/Meshy/A_Meshy_Alert.fbx", defaults);
            Assert.That(clips.Length, Is.EqualTo(3));
            Assert.That(clips[1].name, Is.EqualTo("A_Meshy_Alert_Arms"));
            Assert.That(clips[2].name, Is.EqualTo("A_Meshy_Alert_Perch"));
            Assert.That(clips[1].loopTime, Is.EqualTo(clips[0].loopTime));
            Assert.That(clips[1].mirror, Is.False);
        }

        [Test]
        public void SacTakesSkipTheirBindFrames()
        {
            var defaults = new[] { new ModelImporterClipAnimation { takeName = "Take 001", name = "Take 001", firstFrame = 0, lastFrame = 33 } };
            var clips = ClipRules.Configure("Assets/_Game/Art/Animation/Clips/SAC/A_SAC_StrafeRight.fbx", defaults);
            Assert.That(clips.Length, Is.EqualTo(2), "clip + mirrored left strafe");
            Assert.That(clips[0].firstFrame, Is.EqualTo(2f));
            Assert.That(clips[0].lastFrame, Is.EqualTo(32f));
            Assert.That(clips[0].keepOriginalPositionY, Is.True, "SAC height based upon Original (content measurement)");
            Assert.That(clips[1].mirror, Is.True);
        }

        [Test]
        public void SettingsComeFromTheTableThenHeuristics()
        {
            var walk = ClipRules.SettingFor("A_Meshy_WalkForwardWhileShootingInplace", ClipSource.Meshy, false);
            Assert.That(walk.Loop, Is.True);
            Assert.That(walk.RootMotion, Is.False);
            Assert.That(walk.FootL, Is.Not.Null.And.Not.Empty, "content-measured foot plants");
            var death = ClipRules.SettingFor("A_Meshy_ShotAndFallBackward", ClipSource.Meshy, false);
            Assert.That(death.Loop, Is.False);
            Assert.That(death.RootMotion, Is.True);
            var turn = ClipRules.SettingFor("A_Meshy_RifleAimTurnRight", ClipSource.Meshy, false);
            Assert.That(turn.BakeRotation, Is.False, "turn-in-place clips keep their root rotation out of the pose");
            var unknownLoop = ClipRules.SettingFor("A_UAL1_Swim_Idle_Loop", ClipSource.UAL1, false);
            Assert.That(unknownLoop.Loop, Is.True);
            var unknownHit = ClipRules.SettingFor("A_UAL1_Hit_Something", ClipSource.UAL1, false);
            Assert.That(unknownHit.Additive, Is.True);
            Assert.That(ClipRules.KeepsRootHeight("A_UAL2RM_ClimbUp_1m"), Is.True);
            Assert.That(ClipRules.KeepsRootHeight("A_Meshy_VaultWithRifle"), Is.False);
        }

        [Test]
        public void ConfigureSkipsTheTPoseAndAddsMirrors()
        {
            var defaults = new[]
            {
                new ModelImporterClipAnimation { takeName = "A_TPose", name = "A_TPose", firstFrame = 0, lastFrame = 1 },
                new ModelImporterClipAnimation { takeName = "Take 001", name = "Take 001", firstFrame = 0, lastFrame = 36 },
            };
            var clips = ClipRules.Configure("Assets/_Game/Art/Animation/Clips/Meshy/A_Meshy_WalkLeftWithGunInplace.fbx", defaults);
            Assert.That(clips.Length, Is.EqualTo(2));
            Assert.That(clips[0].name, Is.EqualTo("A_Meshy_WalkLeftWithGunInplace"));
            Assert.That(clips[0].mirror, Is.False);
            Assert.That(clips[1].name, Is.EqualTo("A_Meshy_WalkLeftWithGunInplace_Mirror"));
            Assert.That(clips[1].mirror, Is.True);
            Assert.That(clips[0].loopTime && clips[0].lockRootPositionXZ && clips[0].lockRootRotation && clips[0].lockRootHeightY, Is.True);
            // Mirrored clips swap the feet.
            var l0 = 0;
            foreach (var e in clips[0].events) if (e.stringParameter == AnimEventIds.FootL) l0++;
            var r1 = 0;
            foreach (var e in clips[1].events) if (e.stringParameter == AnimEventIds.FootR) r1++;
            Assert.That(r1, Is.EqualTo(l0));
            foreach (var e in clips[0].events)
            {
                Assert.That(e.functionName, Is.EqualTo("OnAnimEvent"));
                Assert.That(e.time, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SettingsTableIsConsistent()
        {
            var names = new HashSet<string>();
            foreach (var r in ClipSettingsTable.All)
            {
                Assert.That(names.Add(r.Name), Is.True, "duplicate row " + r.Name);
                Assert.That(r.Name.StartsWith("A_"), Is.True, r.Name);
                if (r.FootL != null) foreach (var t in r.FootL) Assert.That(t, Is.InRange(0f, 1f), r.Name);
                if (r.FootR != null) foreach (var t in r.FootR) Assert.That(t, Is.InRange(0f, 1f), r.Name);
            }
        }

        [Test]
        public void CatalogCandidatesExistInTheImportRules()
        {
            // Every Meshy/HY-Motion/Rocketbox candidate must be a clip the content pipeline makes (catches typos); mirrored
            // candidates need a mirror row.
            foreach (var key in ClipCatalog.Keys)
                foreach (var n in ClipCatalog.CandidatesFor(key))
                {
                    if (n.StartsWith("A_Pose_")) continue;
                    string suffix = null;
                    foreach (var sfx in new[] { "_Mirror", "_Arms", "_Perch" })
                        if (n.EndsWith(sfx)) suffix = sfx;
                    var baseName = suffix != null ? n.Substring(0, n.Length - suffix.Length) : n;
                    var curated = n.StartsWith("A_Meshy_") || n.StartsWith("A_HYMotion_") || n.StartsWith("A_Rocketbox_");
                    var found = ClipSettingsTable.TryGet(baseName, out var row);
                    if (curated) Assert.That(found, Is.True, key + " candidate " + n + " is not a known clip");
                    if (found && suffix == "_Mirror") Assert.That(row.Mirror, Is.True, n + " has no mirror row");
                    if (found && (suffix == "_Arms" || suffix == "_Perch"))
                        Assert.That(row.Aliases, Does.Contain(suffix), n + " has no alias row");
                }
        }

        [Test]
        public void CatalogCoversEverySlot()
        {
            var keys = new HashSet<string>(ClipCatalog.Keys);
            foreach (var f in typeof(ClipKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string))
                    Assert.That(keys.Contains((string)f.GetRawConstantValue()), Is.True, "ClipCatalog lacks ClipKeys." + f.Name);
            foreach (var k in new[] { ClipKeys.WalkFwd, ClipKeys.WalkBack, ClipKeys.WalkLeft, ClipKeys.WalkRight, ClipKeys.JogFwd, ClipKeys.Sprint,
                                      ClipKeys.CrouchFwd, ClipKeys.CrouchLeft })
                Assert.That(ClipCatalog.NominalVelocity(k).magnitude, Is.GreaterThan(0.5f), k);
            Assert.That(ClipCatalog.NominalVelocity(ClipKeys.WalkLeft).x, Is.LessThan(0f));
            Assert.That(ClipCatalog.NominalVelocity(ClipKeys.WalkBack).y, Is.LessThan(0f));
            Assert.That(ClipCatalog.VariantFor(ControllerRole.Civilian, ClipKeys.WalkFwd), Is.Not.Null);
            Assert.That(ClipCatalog.VariantFor(ControllerRole.Squad, ClipKeys.WalkFwd), Is.Null);
            Assert.That(ClipCatalog.VariantFor(ControllerRole.Squad, ClipKeys.Idle), Is.Not.Null);
        }

        [Test]
        public void ResolverPrefersRoleVariantsAndFallsBack()
        {
            var r = new ClipResolver();
            var alert = new AnimationClip { name = "A_Meshy_Alert" };
            var strafeIdle = new AnimationClip { name = "A_SAC_StrafeIdle" };
            var rb = new AnimationClip { name = "A_Rocketbox_IdleNeutral01" };
            try
            {
                r.Add(alert, "x");
                r.Add(rb, "y");
                r.Add(strafeIdle, "z");
                Assert.That(r.Resolve(ClipKeys.Idle), Is.SameAs(alert));
                Assert.That(r.Resolve(ClipKeys.Idle, ControllerRole.Civilian), Is.SameAs(rb));
                Assert.That(r.Resolve(ClipKeys.Idle, ControllerRole.Squad), Is.SameAs(strafeIdle), "relaxed squad idle");
                Assert.That(r.Resolve(ClipKeys.ArmsLowReady), Is.SameAs(alert), "alias missing: falls back to the shared clip");
                Assert.That(r.Resolve(ClipKeys.Prone), Is.Null);
                Assert.That(r.Find("A_Meshy_Alert"), Is.SameAs(alert));
            }
            finally
            {
                Object.DestroyImmediate(alert);
                Object.DestroyImmediate(rb);
                Object.DestroyImmediate(strafeIdle);
            }
        }

        [Test]
        public void MuscleAttributeNames()
        {
            Assert.That(MuscleCurves.AttributeName("Left Index 1 Stretched"), Is.EqualTo("LeftHand.Index.1 Stretched"));
            Assert.That(MuscleCurves.AttributeName("Right Thumb Spread"), Is.EqualTo("RightHand.Thumb.Spread"));
            Assert.That(MuscleCurves.AttributeName("Left Upper Leg Front-Back"), Is.EqualTo("Left Upper Leg Front-Back"));
            Assert.That(MuscleCurves.IsMuscle("Left Upper Leg Front-Back"), Is.True);
            Assert.That(MuscleCurves.IsMuscle("Left Index 1 Stretched"), Is.True);
        }

        [Test]
        public void PlaceholderClipsUseOnlyRealMuscles()
        {
            foreach (PlaceholderKind kind in System.Enum.GetValues(typeof(PlaceholderKind)))
            {
                var clip = new AnimationClip();
                try
                {
                    ProceduralClipFactory.Fill(clip, kind);
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    Assert.That(bindings.Length, Is.GreaterThan(7), kind.ToString());
                    Assert.That(clip.isHumanMotion, Is.True, kind + " must be a Humanoid clip");
                }
                finally
                {
                    Object.DestroyImmediate(clip);
                }
            }
            foreach (HandPose pose in System.Enum.GetValues(typeof(HandPose)))
            {
                var clip = new AnimationClip();
                try
                {
                    ProceduralClipFactory.FillHandPose(clip, pose);
                    Assert.That(AnimationUtility.GetCurveBindings(clip).Length, Is.EqualTo(40), pose + ": 5 fingers x 4 muscles x 2 hands");
                }
                finally
                {
                    Object.DestroyImmediate(clip);
                }
            }
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }
    }
}
