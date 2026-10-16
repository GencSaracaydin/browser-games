using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LongShadow.AI.Common;
using LongShadow.AI.Enemy;
using LongShadow.AI.Squad;
using LongShadow.Core.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>Every bark, callout and cue line the AI plays exists in Docs/DIALOGUE.json (GDD §14.4, §15).</summary>
    public class DialogueIdTests
    {
        private static HashSet<string> Ids()
        {
            var text = File.ReadAllText(Path.Combine(BuilderUtil.ProjectRoot, "Docs", "DIALOGUE.json"));
            var ids = new HashSet<string>();
            foreach (Match m in Regex.Matches(text, "\"id\"\\s*:\\s*\"([^\"]+)\"")) ids.Add(m.Groups[1].Value);
            return ids;
        }

        [Test]
        public void BarksExist()
        {
            var ids = Ids();
            foreach (var id in BarkIds.All) Assert.IsTrue(ids.Contains(id), id);
            Assert.That(BarkIds.All.Length, Is.EqualTo(24), "BK_LEG_01-15 and BK_GRY_01-09");
        }

        [Test]
        public void CalloutsExist()
        {
            var ids = Ids();
            foreach (var id in CalloutIds.All) Assert.IsTrue(ids.Contains(id), id);
            foreach (var m in new[] { "HOL", "SPA", "TIN" })
                foreach (RelativeDirection d in System.Enum.GetValues(typeof(RelativeDirection)))
                    Assert.IsTrue(ids.Contains(CalloutIds.Direction(m, d)), m + " " + d);
        }

        [Test]
        public void CueLinesExist()
        {
            var ids = Ids();
            Assert.IsTrue(ids.Contains(TechnicalGunnerBehaviour.CueLine), "D028 cues the technical");
        }
    }
}
