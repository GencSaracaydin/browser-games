using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Screens;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>GDD §8.9: the dossier lines in their exact columns, with live stats.</summary>
    public sealed class DebriefModelTests
    {
        private static MissionStats GddExample() => new MissionStats
        {
            Kills = 44, Headshots = 17, ShotsFired = 100, ShotsHit = 38, GrenadesThrown = 6, Deaths = 2,
            MissionSeconds = 14 * 60 + 2, Intel = 3, OptionalIntel = 1,
        };

        [Test]
        public void StatLinesMatchTheGddLayout()
        {
            var m = new DebriefModel(GddExample(), true, true, true);
            var b = new TextBuffer();
            m.Write(DebriefModel.StatsLineA, b);
            Assert.AreEqual("MISSION TIME ....... 14:02      KILLS ........ 44     HEADSHOTS .... 17", b.ToString());
            m.Write(DebriefModel.StatsLineB, b);
            Assert.AreEqual("ACCURACY ........... 38 %       GRENADES ..... 6      DEATHS ....... 2", b.ToString());
            m.Write(DebriefModel.IntelLine, b);
            Assert.AreEqual("INTEL .............. LEDGERS [X]  REELS [X]  CODEBOOK [X]   OPTIONAL 1/2", b.ToString());
        }

        [Test]
        public void PersonnelLinesAndTheRetype()
        {
            var m = new DebriefModel(GddExample(), true, false, false);
            var b = new TextBuffer();
            m.Write(DebriefModel.HollisterLine, b);
            Assert.AreEqual("HOLLISTER, F. ...... KIA (HOSTILE FIRE)", b.ToString());
            Assert.AreEqual("HOSTILE", b.ToString().Substring(DebriefModel.StrikeStart, DebriefModel.StrikeLength));
            Assert.AreEqual(b.Length, DebriefModel.PersonnelBaseLength(DebriefModel.HollisterLine));
            m.Write(DebriefModel.HollisterLine, b, 1f, true);
            Assert.AreEqual("HOLLISTER, F. ...... KIA (HOSTILE FIRE) UNDER REVIEW", b.ToString());
            m.Write(DebriefModel.PersonnelFirst + 1, b);
            Assert.AreEqual("DUQUESNE, W. ....... RTB", b.ToString());
            m.Write(DebriefModel.PersonnelFirst + 2, b);
            Assert.AreEqual("CASTELLANOS, M. .... PENDING", b.ToString());
            m.Write(DebriefModel.PersonnelFirst + 3, b);
            Assert.AreEqual("CALDER, N. ......... ASSET STATUS: ACTIVE", b.ToString());
            m.Write(DebriefModel.IntelLine, b);
            StringAssert.Contains("LEDGERS [X]  REELS [ ]  CODEBOOK [ ]", b.ToString());
        }

        [Test]
        public void StatsRollUpFromZero()
        {
            var m = new DebriefModel(GddExample(), true, true, true);
            var b = new TextBuffer();
            m.Write(DebriefModel.StatsLineA, b, 0f);
            Assert.AreEqual("MISSION TIME ....... 00:00      KILLS ........ 0      HEADSHOTS .... 0", b.ToString());
            m.Write(DebriefModel.StatsLineA, b, 0.5f);
            StringAssert.Contains("KILLS ........ 22", b.ToString());
        }

        [Test]
        public void IntelFlagsFromStatsFollowTheBeatOrder()
        {
            var b = new TextBuffer();
            var stats = GddExample();
            stats.Intel = 2;
            DebriefModel.FromStats(stats).Write(DebriefModel.IntelLine, b);
            StringAssert.Contains("LEDGERS [X]  REELS [X]  CODEBOOK [ ]", b.ToString());
        }

        [Test]
        public void NameLengthsCoverTheNames()
        {
            Assert.AreEqual("HOLLISTER, F.".Length, DebriefModel.NameLength(DebriefModel.PersonnelFirst));
            Assert.AreEqual("CALDER, N.".Length, DebriefModel.NameLength(DebriefModel.PersonnelFirst + 3));
            Assert.AreEqual(0, DebriefModel.NameLength(DebriefModel.HeaderLine));
        }
    }
}
