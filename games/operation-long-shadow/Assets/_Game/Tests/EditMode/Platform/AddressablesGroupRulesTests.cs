using LongShadow.Core;
using LongShadow.Platform;
using LongShadow.Platform.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>Folder -> group mapping of TECH_PLAN §8.10 (pure part, no AssetDatabase).</summary>
    public class AddressablesGroupRulesTests
    {
        [Test]
        public void PersistentSceneGoesToCorePersistentAddressedByName()
        {
            Assert.That(AddressablesGroupRules.GroupFor("Assets/_Game/Scenes/S_Persistent.unity", out var a), Is.EqualTo("Core_Persistent"));
            Assert.That(a, Is.EqualTo(Sections.PersistentScene));
        }

        [Test]
        public void EverySectionSceneGetsItsOwnGroupAddressedBySceneName()
        {
            for (var i = 0; i < Sections.Count; i++)
            {
                var id = (SectionId)i;
                Assert.That(AddressablesGroupRules.GroupFor(Sections.ScenePath(id), out var a), Is.EqualTo(Sections.SceneName(id)));
                Assert.That(a, Is.EqualTo(Sections.AddressKey(id)));
            }
        }

        [Test]
        public void PlaceholdersStandInUnderTheRealAddress()
        {
            Assert.That(AddressablesGroupRules.GroupFor(SectionStreamer.PlaceholderPath("S0_Booth"), out var a), Is.EqualTo("S0_Booth"));
            Assert.That(a, Is.EqualTo("S0_Booth"));
            Assert.That(AddressablesGroupRules.GroupFor(SectionStreamer.PlaceholderPath(Sections.PersistentScene), out var p), Is.EqualTo("Core_Persistent"));
            Assert.That(p, Is.EqualTo(Sections.PersistentScene));
        }

        [TestCase("Assets/_Game/Scenes/S_Boot.unity")]
        [TestCase("Assets/_Game/Scenes/Dev/AIDevArena.unity")]
        [TestCase("Assets/_Game/Art/HDRI/EditorOnly/T_Sky_kiara_8_sunset_nosun_2k.exr")]
        [TestCase("Assets/_Game/Scripts/Platform/Bootstrap.cs")]
        [TestCase("Assets/Scenes/WebGLTest.unity")]
        public void NeverAddressable(string path)
        {
            Assert.IsNull(AddressablesGroupRules.GroupFor(path, out _));
        }

        [TestCase("Assets/_Game/Audio/VO/RADIO/VO_D015_RADIO.wav", "Audio_VO_A")]
        [TestCase("Assets/_Game/Audio/VO/DRY/VO_D050_DRY.wav", "Audio_VO_B")]
        [TestCase("Assets/_Game/Audio/VO/RADIO/VO_D120_RADIO.wav", "Audio_VO_C")]
        [TestCase("Assets/_Game/Audio/VO/INTERCOM/VO_D203_INTERCOM.wav", "Audio_VO_A")]
        [TestCase("Assets/_Game/Audio/VO/FAR/VO_BK_LEG_01_FAR_A.wav", "Audio_VO_A")]
        [TestCase("Assets/_Game/Audio/SFX/Weapons/SFX_Weapons_AK_Shot_01.wav", "Audio_SFX_A")]
        [TestCase("Assets/_Game/Audio/SFX/SetPieces/SFX_SetPieces_Awning_01.wav", "Audio_SFX_B")]
        [TestCase("Assets/_Game/Audio/Music/MUS_01_SESSION_A.wav", "Audio_Music_A")]
        [TestCase("Assets/_Game/Audio/Music/MUS_07_CLIMAX_A.wav", "Audio_Music_B")]
        [TestCase("Assets/_Game/Audio/Ambience/AMB_TOWN_DUSK.wav", "Audio_Amb")]
        [TestCase("Assets/_Game/Prefabs/Weapons/PF_WPN_Commando_FP.prefab", "Weapons_A")]
        [TestCase("Assets/_Game/Prefabs/Weapons/PF_WPN_AK_World.prefab", "Weapons_B")]
        [TestCase("Assets/_Game/Prefabs/Bodies/PFB_Calder_FP.prefab", "Player_FP")]
        [TestCase("Assets/_Game/Prefabs/Bodies/PFB_Hollis.prefab", "Chars_Squad")]
        [TestCase("Assets/_Game/Prefabs/Bodies/PFB_Legion_02.prefab", "Chars_Legion")]
        [TestCase("Assets/_Game/Prefabs/Enemies/PF_Greycoat_Rifleman.prefab", "Chars_Greycoat")]
        [TestCase("Assets/_Game/Data/Libraries/LIB_Audio.asset", "Core_Persistent")]
        [TestCase("Assets/_Game/Art/Shaders/SG_CRTScreen.shadergraph", "Core_Shaders")]
        [TestCase("Assets/_Game/Art/Textures/Images/IMG_MAP_SARDAHN.png", "Images")]
        [TestCase("Assets/_Game/Prefabs/Vehicles/PF_Technical.prefab", "Vehicles")]
        public void ContentGroups(string path, string group)
        {
            Assert.That(AddressablesGroupRules.GroupFor(path, out var address), Is.EqualTo(group));
            Assert.That(address, Is.EqualTo(path), "non-scene assets are addressed by their path");
        }

        [Test]
        public void HashedGroupsAreStableAndKnown()
        {
            var a = AddressablesGroupRules.GroupFor("Assets/_Game/Prefabs/Props/PF_Crate_01.prefab", out _);
            var b = AddressablesGroupRules.GroupFor("Assets/_Game/Prefabs/Props/PF_Crate_01.prefab", out _);
            Assert.That(a, Is.EqualTo(b));
            Assert.IsTrue(AddressablesGroupRules.IsManagedGroup(a), a);
            var env = AddressablesGroupRules.GroupFor("Assets/_Game/Art/Materials/Env/Sand01/M_Sand01.mat", out _);
            Assert.IsTrue(env.StartsWith("Env_Tex_") && AddressablesGroupRules.IsManagedGroup(env), env);
        }

        [Test]
        public void ManagedGroupNames()
        {
            Assert.IsTrue(AddressablesGroupRules.IsManagedGroup("Shared_Env_A"));
            Assert.IsTrue(AddressablesGroupRules.IsManagedGroup("S9_TownFar"));
            Assert.IsFalse(AddressablesGroupRules.IsManagedGroup("Default Local Group"));
            Assert.IsFalse(AddressablesGroupRules.IsManagedGroup(null));
        }
    }
}
