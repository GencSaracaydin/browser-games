using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Menus;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>Every GDD §11 option exists, round-trips through GameSettings and never leaves its range.</summary>
    public sealed class OptionsModelTests
    {
        [Test]
        public void EveryGddOptionIsInTheMenu()
        {
            var ids = new HashSet<string>();
            foreach (var o in OptionsModel.All) Assert.IsTrue(ids.Add(o.Id), "duplicate " + o.Id);
            string[] required =
            {
                "sens", "ads", "invert", "fov", "master", "music", "effects", "voice", "subtitles", "subtitleSize", "translate",
                "perf", "renderScale", "motionBlur", "grain", "brightness", "fullscreen", "difficulty",
            };
            foreach (var id in required) Assert.IsTrue(ids.Contains(id), "missing option " + id);
            Assert.Greater(OptionsModel.Controls.GetLength(0), 10, "controls list");
        }

        [Test]
        public void EveryOptionRoundTripsAndDoesNotMutateTheSource()
        {
            foreach (var o in OptionsModel.All)
            {
                var source = new GameSettings();
                var before = source.ToJson();
                var current = o.Get(source);
                var target = o.Kind == OptionKind.Slider ? o.Max : o.Choices.Length - 1;
                if (System.Math.Abs(current - target) < 0.01f) target = o.Kind == OptionKind.Slider ? o.Min : 0;
                var changed = OptionsModel.With(source, o, target);
                Assert.AreEqual(before, source.ToJson(), o.Id + " mutated the current settings");
                Assert.AreEqual(target, o.Get(changed), 0.011f, o.Id + " did not round-trip");
                Assert.AreNotEqual(before, changed.ToJson(), o.Id + " changed nothing");
            }
        }

        [Test]
        public void SliderRangesAreTheGameSettingsRanges()
        {
            Assert.AreEqual(GameSettings.MinSensitivity, OptionsModel.Find("sens").Min);
            Assert.AreEqual(GameSettings.MaxSensitivity, OptionsModel.Find("sens").Max);
            Assert.AreEqual(GameSettings.MinFov, OptionsModel.Find("fov").Min);
            Assert.AreEqual(GameSettings.MaxFov, OptionsModel.Find("fov").Max);
            Assert.AreEqual(GameSettings.MinBrightness, OptionsModel.Find("brightness").Min);
            Assert.AreEqual(GameSettings.MaxBrightness, OptionsModel.Find("brightness").Max);
            Assert.AreEqual(GameSettings.MinAdsSens, OptionsModel.Find("ads").Min);
        }

        [Test]
        public void RenderScaleChoicesMapToAutoAnd75To100()
        {
            var s = new GameSettings();
            Assert.AreEqual(0f, OptionsModel.RenderScaleIndex(s), "AUTO by default (D21)");
            OptionsModel.SetRenderScaleIndex(s, 1);
            Assert.IsFalse(s.RenderScaleAuto);
            Assert.AreEqual(0.75f, s.RenderScale, 1e-4f);
            OptionsModel.SetRenderScaleIndex(s, 6);
            Assert.AreEqual(1f, s.RenderScale, 1e-4f);
            Assert.AreEqual(6f, OptionsModel.RenderScaleIndex(s));
            OptionsModel.SetRenderScaleIndex(s, 0);
            Assert.IsTrue(s.RenderScaleAuto);
        }

        [Test]
        public void Formatting()
        {
            var b = new TextBuffer();
            OptionsModel.Find("sens").Format(3f, b);
            Assert.AreEqual("3.0", b.ToString());
            b.Clear();
            OptionsModel.Find("fov").Format(90f, b);
            Assert.AreEqual("90°", b.ToString());
            b.Clear();
            OptionsModel.Find("brightness").Format(0.1f, b);
            Assert.AreEqual("+0.10 EV", b.ToString());
            b.Clear();
            OptionsModel.Find("master").Format(80f, b);
            Assert.AreEqual("80 %", b.ToString());
        }

        [Test]
        public void EveryOptionIsOnAPage()
        {
            var total = 0;
            foreach (var p in OptionsModel.Pages) total += OptionsModel.CountOn(p);
            Assert.AreEqual(OptionsModel.All.Length, total);
        }
    }
}
