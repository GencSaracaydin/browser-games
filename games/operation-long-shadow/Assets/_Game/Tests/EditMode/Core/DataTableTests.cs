using System.Text.RegularExpressions;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LongShadow.Tests.EditMode.Core
{
    public class DataTableTests
    {
        [Test]
        public void DifficultyMatchesGdd16()
        {
            var r = DifficultyProfile.For(Difficulty.Regular);
            Assert.That(r.PlayerHp, Is.EqualTo(100f));
            Assert.That(r.EnemyDamageMultiplier, Is.EqualTo(1.0f));
            Assert.That(r.RegenDelay, Is.EqualTo(4.0f));
            Assert.That(r.RegenRate, Is.EqualTo(50f));
            Assert.That(r.LastStandImmunity, Is.EqualTo(0.8f));
            Assert.That(r.AttackTokens, Is.EqualTo(3));
            Assert.That(r.MarksmanDamagePerHit, Is.EqualTo(70f));
            var rec = DifficultyProfile.For(Difficulty.Recruit);
            Assert.That(rec.EnemyDamageMultiplier, Is.EqualTo(0.5f));
            Assert.That(rec.LegionGrenadeInterval, Is.EqualTo(90f));
            Assert.That(rec.EnemyAccuracyMultiplier, Is.EqualTo(0.7f));
            var hard = DifficultyProfile.For(Difficulty.Hardened);
            Assert.That(hard.GreycoatGrenadeTrigger, Is.EqualTo(5f));
            Assert.That(hard.LegionAkHitsToDown, Is.EqualTo(4));
            var vet = DifficultyProfile.For(Difficulty.Veteran);
            Assert.That(vet.LastStandImmunity, Is.EqualTo(0f));
            Assert.That(vet.RegenRate, Is.EqualTo(35f));
            Assert.That(vet.EnemyDamageMultiplier, Is.EqualTo(2.2f));
            Assert.That(vet.AttackTokens, Is.EqualTo(5));
            // Consistency: AK 20 dmg x multiplier x hits >= 100 HP (GDD §14.1 + §16).
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                var p = DifficultyProfile.For(d);
                Assert.That(20f * p.EnemyDamageMultiplier * p.LegionAkHitsToDown, Is.GreaterThanOrEqualTo(100f), d.ToString());
            }
        }

        [Test]
        public void GameSettingsDefaultsMatchGdd11()
        {
            var s = new GameSettings();
            Assert.That(s.MouseSensitivity, Is.EqualTo(3f));
            Assert.That(s.AdsSensitivity, Is.EqualTo(0.85f));
            Assert.That(s.FovHorizontal, Is.EqualTo(90f));
            Assert.IsTrue(s.TranslateEnemyChatter);
            Assert.IsTrue(s.Subtitles);
            Assert.That(s.Difficulty, Is.EqualTo(Difficulty.Regular));
            // 3.0 sens gives 9 degrees per 100 CSS px (GDD §11).
            Assert.That(100f * s.MouseSensitivity * GameSettings.DegreesPerPixelPerSens, Is.EqualTo(9f).Within(1e-4f));
        }

        [Test]
        public void GameSettingsSanitizeAndJson()
        {
            var s = new GameSettings { MouseSensitivity = 50f, FovHorizontal = 10f, RenderScale = 0.1f, Brightness = float.NaN,
                                       Difficulty = (Difficulty)42 };
            s.Sanitize();
            Assert.That(s.MouseSensitivity, Is.EqualTo(10f));
            Assert.That(s.FovHorizontal, Is.EqualTo(75f));
            Assert.That(s.RenderScale, Is.EqualTo(0.75f));
            Assert.That(s.Brightness, Is.EqualTo(0f));
            Assert.That(s.Difficulty, Is.EqualTo(Difficulty.Regular));
            var t = new GameSettings { InvertY = true, Difficulty = Difficulty.Veteran, SubtitleSize = SubtitleSize.Large };
            var back = GameSettings.FromJson(t.ToJson());
            Assert.IsTrue(back.InvertY);
            Assert.That(back.Difficulty, Is.EqualTo(Difficulty.Veteran));
            Assert.That(back.SubtitleSize, Is.EqualTo(SubtitleSize.Large));
            LogAssert.Expect(LogType.Warning, new Regex("GameSettings JSON invalid"));
            var bad = GameSettings.FromJson("{oops");
            Assert.That(bad.MouseSensitivity, Is.EqualTo(3f));
        }

        [Test]
        public void TeamsAndHitboxDefaults()
        {
            Assert.IsTrue(Teams.AreHostile(Team.Player, Team.Legion));
            Assert.IsTrue(Teams.AreHostile(Team.Greycoat, Team.Squad));
            Assert.IsFalse(Teams.AreHostile(Team.Player, Team.Squad));
            Assert.IsFalse(Teams.AreHostile(Team.Legion, Team.Greycoat));
            Assert.IsFalse(Teams.AreHostile(Team.None, Team.Legion));
            Assert.That(Hitbox.DefaultMultiplier(HitZone.Head), Is.EqualTo(2.0f));
            Assert.That(Hitbox.DefaultMultiplier(HitZone.Limb), Is.EqualTo(0.85f));
            Assert.That(Hitbox.DefaultMultiplier(HitZone.Torso), Is.EqualTo(1f));
        }

        [TestCase("PM_Sand", SurfaceType.Sand)]
        [TestCase("M_ConcreteWall007", SurfaceType.Concrete)]
        [TestCase("CorrugatedSteel005", SurfaceType.Metal)]
        [TestCase("SaltPan", SurfaceType.SaltCrust)]
        [TestCase("Plaster_Olive", SurfaceType.Plaster)]
        [TestCase("Unknown", SurfaceType.Default)]
        public void SurfaceFromName(string name, SurfaceType expected) => Assert.That(SurfaceTag.FromName(name), Is.EqualTo(expected));
    }
}
