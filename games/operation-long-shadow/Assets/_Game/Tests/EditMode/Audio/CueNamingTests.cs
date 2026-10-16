using LongShadow.Audio.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §4.2 naming: SFX_&lt;Group&gt;_&lt;Name&gt;_&lt;nn&gt;, _FAR distance takes, MUS_/AMB_/STG_ ids.</summary>
    public class CueNamingTests
    {
        private static CueFile P(string path)
        {
            Assert.That(CueNaming.TryParse(path, out var f), Is.True, path);
            return f;
        }

        [Test]
        public void VariantsAreStripped()
        {
            var f = P("Assets/_Game/Audio/SFX/Footsteps/SFX_FS_SAND_07.wav");
            Assert.That(f.CueId, Is.EqualTo("SFX_FS_SAND"));
            Assert.That(f.Variant, Is.EqualTo(7));
            Assert.That(f.Far, Is.False);
            Assert.That(f.Group, Is.EqualTo("SFX/Footsteps"));
            Assert.That(CueNaming.SfxGroup(f.Group), Is.EqualTo("Footsteps"));
        }

        [Test]
        public void FarTakesByNameOrFolder()
        {
            var a = P("Assets/_Game/Audio/SFX/Explosions/SFX_EXP_FRAG_FAR_02.wav");
            Assert.That(a.CueId, Is.EqualTo("SFX_EXP_FRAG"));
            Assert.That(a.Far, Is.True);
            Assert.That(a.Variant, Is.EqualTo(2));
            var b = P("Assets/_Game/Audio/SFX/Weapons/Far/SFX_WPN_AK_SHOT_FAR_01.wav");
            Assert.That(b.CueId, Is.EqualTo("SFX_WPN_AK_SHOT"));
            Assert.That(b.Far, Is.True);
            Assert.That(b.Group, Is.EqualTo("SFX/Weapons"));
        }

        [Test]
        public void SlicesAndSingleDigitsStayInTheId()
        {
            Assert.That(P("Assets/_Game/Audio/SFX/Weapons/SFX_WPN_AK_RELOAD_MAGOUT_01.wav").CueId, Is.EqualTo("SFX_WPN_AK_RELOAD_MAGOUT"));
            Assert.That(P("Assets/_Game/Audio/SFX/Synth/SFX_RADIO_CLICKS_3_02.wav").CueId, Is.EqualTo("SFX_RADIO_CLICKS_3"));
            Assert.That(P("Assets/_Game/Audio/Music/MUS_02_MARKET_HI.wav").CueId, Is.EqualTo("MUS_02_MARKET_HI"));
            Assert.That(P("Assets/_Game/Audio/Music/MUS_01_SESSION.wav").CueId, Is.EqualTo("MUS_01_SESSION"));
            Assert.That(P("Assets/_Game/Audio/SFX/SetPieces/SFX_HEARTBEAT.wav").Variant, Is.EqualTo(0));
            Assert.That(P("Assets/_Game/Audio/Ambience/AMB_CICADAS_02.wav").Root, Is.EqualTo("Ambience"));
        }

        [Test]
        public void NonCuesAreRejected()
        {
            Assert.That(CueNaming.TryParse("Assets/_Game/Audio/VO/DRY/VO_D001_DRY.wav", out _), Is.False);
            Assert.That(CueNaming.TryParse("Assets/_Game/Art/X/SFX_A_01.wav", out _), Is.False);
            Assert.That(CueNaming.TryParse(null, out _), Is.False);
            Assert.That(CueNaming.TryParse("Assets/_Game/Audio/SFX/UI/X.wav", out _), Is.False);
        }
    }
}
