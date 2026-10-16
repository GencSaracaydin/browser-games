using LongShadow.Audio.Core;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §10.2 layering per weapon class.</summary>
    public class WeaponSoundProfileTests
    {
        [Test]
        public void ClassesGetTheirLayers()
        {
            var ak = WeaponSoundProfile.For(WeaponIds.AK);
            Assert.That(ak.Shot, Is.EqualTo("SFX_WPN_AK_SHOT"));
            Assert.That(ak.Body, Is.EqualTo(WeaponSoundProfile.BodyRifle));
            var rpk = WeaponSoundProfile.For(WeaponIds.RPK);
            Assert.That(rpk.Body, Is.EqualTo(WeaponSoundProfile.BodyHeavy));
            var hp = WeaponSoundProfile.For(WeaponIds.HushPuppy);
            Assert.That(hp.Suppressed, Is.True);
            Assert.That(hp.Body, Is.Null);
            Assert.That(hp.TailGain, Is.LessThan(0.5f));
            Assert.That(WeaponSoundProfile.For(WeaponIds.RPG7).Shot, Is.EqualTo("SFX_RPG_LAUNCH"));
            Assert.That(WeaponSoundProfile.For(WeaponIds.DShK).Mech, Is.Null, "AI-only weapons have no first-person mech layer");
        }
    }
}
